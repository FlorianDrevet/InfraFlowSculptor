import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  OnDestroy,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  CatalogResourceTypeSummaryResponse,
  NamingOverrideRequest,
  ProjectNamingPreviewItemResponse,
  ProjectNamingPreviewRequest,
  ProjectNamingPreviewResponse,
  ProjectNamingSettingsResponse,
  ProjectNamingSettingsRequest,
  ProjectSummaryResponse,
} from '../../core/api/generated';
import { CatalogService } from '../../core/catalog/catalog.service';
import { DsBanner } from '../../ds/banner/banner';
import { DsButton } from '../../ds/button/button';
import { DsPanel } from '../../ds/panel/panel';
import { DsSegmented } from '../../ds/segmented/segmented';
import type { SegmentedOption } from '../../ds/segmented/segmented';
import { DsTextField } from '../../ds/text-field/text-field';
import { ProjectsService } from '../../core/projects/projects.service';

type NamingAbbreviationRow = {
  type: string;
  label: string;
  abbreviation: string;
  source: 'catalogue' | 'project';
};

@Component({
  selector: 'app-project-naming',
  imports: [DsBanner, DsButton, DsPanel, DsSegmented, DsTextField, TranslocoPipe],
  templateUrl: './project-naming.html',
  styleUrl: './project-naming.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectNaming implements OnDestroy {
  readonly projectId = input.required<string>();
  private readonly projects = inject(ProjectsService);
  private readonly catalog = inject(CatalogService);

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly settings = signal<ProjectNamingSettingsResponse | null>(null);
  protected readonly catalogTypes = signal<CatalogResourceTypeSummaryResponse[]>([]);
  protected readonly preview = signal<ProjectNamingPreviewResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly previewLoading = signal(false);
  protected readonly previewError = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal(false);
  protected readonly defaultTemplate = signal('');
  protected readonly resourceGroupTemplate = signal('');
  protected readonly typeOverrides = signal<NamingOverrideRequest[]>([]);
  protected readonly selectedTarget = signal<string | undefined>(undefined);
  protected readonly addTemplateFormOpen = signal(false);
  protected readonly newTemplateType = signal('');
  protected readonly newTemplateValue = signal('');

  protected readonly targetOptions = computed<SegmentedOption[]>(() => {
    const targets = new Map<string, string>();
    for (const item of this.preview()?.items ?? []) {
      targets.set(item.targetCode, item.targetCode);
    }
    return [...targets.values()].map((value) => ({ value, label: value }));
  });

  protected readonly visibleItems = computed(() => {
    const items = this.preview()?.items ?? [];
    const target = this.selectedTarget();
    return target ? items.filter((item) => item.targetCode === target) : items;
  });

  protected readonly visibleIssues = computed(() => {
    const issues = this.preview()?.issues ?? [];
    const target = this.selectedTarget();
    return target ? issues.filter((issue) => issue.targetId === null || issue.targetId === this.targetId(target)) : issues;
  });

  protected readonly abbreviationRows = computed<NamingAbbreviationRow[]>(() => {
    const usedTypes = new Set((this.preview()?.items ?? []).map((item) => item.resourceType));
    const projectOverrides = new Map(
      this.typeOverrides()
        .filter((override) => override.abbreviation?.trim())
        .map((override) => [override.type.toLowerCase(), override.abbreviation!.trim()]),
    );
    return this.catalogTypes()
      .filter((type) => usedTypes.has(type.type))
      .map((type) => {
        const projectAbbreviation = projectOverrides.get(type.type.toLowerCase());
      return {
          type: type.type,
          label: type.labelFr,
          abbreviation: projectAbbreviation ?? type.abbreviation,
        source: projectAbbreviation ? ('project' as const) : ('catalogue' as const),
        };
      });
  });

  protected readonly templateRows = computed(() =>
    this.typeOverrides().filter((override) => override.template?.trim()),
  );

  protected readonly availableTemplateTypes = computed(() => {
    const configured = new Set(
      this.typeOverrides()
        .filter((override) => override.template?.trim())
        .map((override) => override.type.toLowerCase()),
    );
    return this.catalogTypes().filter((type) => !configured.has(type.type.toLowerCase()));
  });

  protected readonly isDirty = computed(() => {
    const current = this.settings();
    const normalizeOverrides = (overrides: readonly NamingOverrideRequest[]) => overrides
      .map((override) => [
        override.type.trim().toLowerCase(),
        override.template?.trim() ?? '',
        override.abbreviation?.trim().toLowerCase() ?? '',
      ].join('\u001f'))
      .sort()
      .join('\u001e');
    return !!current && (
      current.defaultTemplate !== this.defaultTemplate()
      || current.resourceGroupTemplate !== this.resourceGroupTemplate()
      || normalizeOverrides(current.typeOverrides) !== normalizeOverrides(this.typeOverrides())
    );
  });

  private previewTimeout: ReturnType<typeof setTimeout> | undefined;
  private previewRevision = 0;

  constructor() {
    effect(() => {
      void this.load(this.projectId());
    });
  }

  ngOnDestroy(): void {
    if (this.previewTimeout) {
      clearTimeout(this.previewTimeout);
    }
  }

  protected async retry(): Promise<void> {
    await this.load(this.projectId());
  }

  protected updateDefaultTemplate(value: string): void {
    this.defaultTemplate.set(value);
    this.queuePreview();
  }

  protected updateResourceGroupTemplate(value: string): void {
    this.resourceGroupTemplate.set(value);
    this.queuePreview();
  }

  protected updateTypeTemplate(type: string, value: string): void {
    this.updateOverride(type, 'template', value);
  }

  protected updateAbbreviation(type: string, value: string): void {
    const defaultValue = this.catalogTypes().find((item) => item.type === type)?.abbreviation;
    const normalizedValue = value.trim().toLowerCase();
    this.updateOverride(
      type,
      'abbreviation',
      normalizedValue === defaultValue?.trim().toLowerCase() ? '' : normalizedValue,
    );
  }

  protected startAddingTemplate(): void {
    this.newTemplateType.set(this.availableTemplateTypes()[0]?.type ?? '');
    this.newTemplateValue.set('');
    this.addTemplateFormOpen.set(true);
  }

  protected updateNewTemplateType(event: Event): void {
    const element = event.target;
    if (element instanceof HTMLSelectElement) {
      this.newTemplateType.set(element.value);
    }
  }

  protected valueFromEvent(event: Event): string {
    const element = event.target;
    return element instanceof HTMLInputElement ? element.value : '';
  }

  protected addTemplate(): void {
    const type = this.newTemplateType();
    const template = this.newTemplateValue().trim();
    if (!type || !template) {
      return;
    }
    this.updateOverride(type, 'template', template);
    this.addTemplateFormOpen.set(false);
  }

  protected removeTemplate(type: string): void {
    this.updateOverride(type, 'template', '');
  }

  protected typeLabel(type: string): string {
    return this.catalogTypes().find((item) => item.type === type)?.labelFr ?? type;
  }

  protected resourceLabel(item: ProjectNamingPreviewItemResponse): string {
    return `${item.componentCode} / ${this.typeAbbreviation(item.resourceType)} ${item.logicalName}`;
  }

  protected typeAbbreviation(type: string): string {
    const override = this.typeOverrides().find((item) => item.type.toLowerCase() === type.toLowerCase());
    return override?.abbreviation?.trim()
      || this.catalogTypes().find((item) => item.type === type)?.abbreviation
      || type;
  }

  protected originKey(item: ProjectNamingPreviewItemResponse): string {
    switch (item.explanation?.level) {
      case 'Forced':
        return 'project.namingOriginForced';
      case 'ProjectType':
        return 'project.namingOriginProjectType';
      case 'ComponentType':
        return 'project.namingOriginComponentType';
      case 'ComponentDefault':
        return 'project.namingOriginComponentDefault';
      case 'ExistingResource':
        return 'project.namingOriginExisting';
      default:
        return 'project.namingOriginProjectDefault';
    }
  }

  protected hasError(item: ProjectNamingPreviewItemResponse): boolean {
    return (this.preview()?.issues ?? []).some(
      (issue) => issue.severity === 'Error' && issue.resourceId === item.resourceId && issue.targetId === item.targetId,
    );
  }

  protected async save(): Promise<void> {
    const current = this.settings();
    const projectId = this.projectId();
    if (!current || current.isPublished || this.saving() || !this.isDirty()) {
      return;
    }

    this.saving.set(true);
    this.saveError.set(false);
    try {
      const request: ProjectNamingSettingsRequest = {
        preset: current.preset,
        defaultTemplate: this.defaultTemplate(),
        resourceGroupTemplate: this.resourceGroupTemplate(),
        typeOverrides: this.typeOverrides(),
        expectedVersion: current.version,
      };
      const saved = await firstValueFrom(this.projects.saveNamingSettings(projectId, request));
      this.settings.set(saved);
      this.typeOverrides.set(saved.typeOverrides.map((override) => ({ ...override })));
      const updatedProject = await firstValueFrom(this.projects.get(projectId));
      this.project.set(updatedProject);
      this.projects.notifyProjectUpdated(updatedProject);
      this.queuePreview();
    } catch {
      this.saveError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(projectId: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, settings] = await Promise.all([
        firstValueFrom(this.projects.get(projectId)),
        firstValueFrom(this.projects.namingSettings(projectId)),
      ]);
      const catalogTypes = await firstValueFrom(this.catalog.types(project.catalogVersion));
      if (this.projectId() !== projectId) {
        return;
      }
      this.project.set(project);
      this.settings.set(settings);
      this.catalogTypes.set(catalogTypes);
      this.defaultTemplate.set(settings.defaultTemplate);
      this.resourceGroupTemplate.set(settings.resourceGroupTemplate);
      this.typeOverrides.set(settings.typeOverrides.map((override) => ({ ...override })));
      await this.loadPreview(projectId);
    } catch {
      if (this.projectId() === projectId) {
        this.loadError.set(true);
      }
    } finally {
      if (this.projectId() === projectId) {
        this.loading.set(false);
      }
    }
  }

  private updateOverride(type: string, field: 'template' | 'abbreviation', value: string): void {
    const normalizedValue = value.trim();
    const overrides = [...this.typeOverrides()];
    const index = overrides.findIndex((override) => override.type.toLowerCase() === type.toLowerCase());
    const existing = index >= 0 ? overrides[index] : { type, template: null, abbreviation: null };
    const updated: NamingOverrideRequest = {
      ...existing,
      [field]: normalizedValue || null,
    };
    if (!updated.template && !updated.abbreviation) {
      if (index >= 0) {
        overrides.splice(index, 1);
      }
    } else if (index >= 0) {
      overrides[index] = updated;
    } else {
      overrides.push(updated);
    }
    this.typeOverrides.set(overrides);
    this.queuePreview();
  }

  private queuePreview(): void {
    if (this.previewTimeout) {
      clearTimeout(this.previewTimeout);
    }
    const projectId = this.projectId();
    this.previewTimeout = setTimeout(() => void this.loadPreview(projectId), 250);
  }

  private async loadPreview(projectId: string): Promise<void> {
    const settings = this.settings();
    if (!settings) {
      return;
    }
    const revision = ++this.previewRevision;
    const request: ProjectNamingPreviewRequest = {
      preset: settings.preset,
      defaultTemplate: this.defaultTemplate(),
      resourceGroupTemplate: this.resourceGroupTemplate(),
      typeOverrides: this.typeOverrides(),
    };
    this.previewLoading.set(true);
    this.previewError.set(false);
    try {
      const preview = await firstValueFrom(this.projects.previewNaming(projectId, request));
      if (revision === this.previewRevision && this.projectId() === projectId) {
        this.preview.set(preview);
        const selected = this.selectedTarget();
        if (!selected || !preview.items.some((item) => item.targetCode === selected)) {
          this.selectedTarget.set(preview.items[0]?.targetCode);
        }
      }
    } catch {
      if (revision === this.previewRevision) {
        this.previewError.set(true);
      }
    } finally {
      if (revision === this.previewRevision) {
        this.previewLoading.set(false);
      }
    }
  }

  private targetId(targetCode: string): string | undefined {
    return this.preview()?.items.find((item) => item.targetCode === targetCode)?.targetId;
  }
}
