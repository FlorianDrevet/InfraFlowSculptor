import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  CatalogResourceTypeSummaryResponse,
  ComponentResponse,
  ProjectNamingPreviewItemResponse,
  ProjectEnvironmentResponse,
  ProjectSummaryResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';
import { DsResourceIcon } from '../../ds/resource-icon/resource-icon';
import { GeneratedNames } from '../../shared/generated-names/generated-names';
import type { GeneratedNameRow } from '../../shared/generated-names/generated-name-row';

interface ResourceTargetOption {
  id: string;
  code: string;
  region: string;
}

@Component({
  selector: 'app-resource-create',
  imports: [
    CdkTrapFocus,
    DsButton,
    DsResourceIcon,
    FormsModule,
    GeneratedNames,
    RouterLink,
    TranslocoPipe,
  ],
  templateUrl: './resource-create.html',
  styleUrl: './resource-create.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceCreate implements OnInit {
  readonly orgId = input.required<string>();
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  readonly type = input.required<string>();
  readonly existing = input<string | boolean>('');

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly typeSummary = signal<CatalogResourceTypeSummaryResponse | null>(null);
  protected readonly targets = signal<ResourceTargetOption[]>([]);
  protected readonly logicalName = signal('');
  protected readonly description = signal('');
  protected readonly resourceGroupId = signal('');
  protected readonly existingIds = signal<Record<string, string>>({});
  protected readonly targetPresence = signal<Record<string, boolean>>({});
  protected readonly generatedNames = signal<GeneratedNameRow[]>([]);
  protected readonly generatedNamesLoading = signal(false);
  protected readonly generatedNamesError = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly canEditModel = signal(false);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly saveErrorKey = signal('resource.saveError');
  protected readonly conflict = signal(false);
  protected readonly conflictVersions = signal<{ current: string; submitted: string } | null>(null);
  protected readonly existingMode = computed(
    () => this.existing() === true || this.existing() === 'true',
  );
  protected readonly allExistingIdsProvided = computed(() => {
    const ids = this.targets().map((target) => this.existingIds()[target.id]?.trim() ?? '');
    return ids.length > 0 && ids.every((resourceId) => resourceId.length > 0);
  });

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly projects = inject(ProjectsService);
  private readonly components = inject(ComponentsService);
  private readonly catalog = inject(CatalogService);
  private readonly resources = inject(ResourcesService);
  private previewTimer: ReturnType<typeof setTimeout> | null = null;
  private previewSequence = 0;

  ngOnInit(): void {
    void this.load();
  }

  protected updateLogicalName(value: string): void {
    this.logicalName.set(value);
    this.queueNamePreview();
  }

  protected updateDescription(value: string): void {
    this.description.set(value);
  }

  protected updateResourceGroup(value: string): void {
    this.resourceGroupId.set(value);
    this.queueNamePreview();
  }

  protected updateExistingId(targetId: string, value: string): void {
    this.existingIds.update((current) => ({ ...current, [targetId]: value }));
  }

  protected targetIsPresent(targetId: string): boolean {
    return this.targetPresence()[targetId] ?? true;
  }

  protected updatePresence(targetId: string, isPresent: boolean): void {
    this.targetPresence.update((current) => ({ ...current, [targetId]: isPresent }));
  }

  protected async create(): Promise<void> {
    const component = this.component();
    const project = this.project();
    const resourceGroupId = this.resourceGroupId();
    const logicalName = this.logicalName().trim();
    if (
      !component ||
      !project ||
      !this.canEditModel() ||
      !resourceGroupId ||
      !logicalName ||
      this.saving()
    ) {
      return;
    }
    if (this.existingMode() && !this.allExistingIdsProvided()) return;

    this.saving.set(true);
    this.saveError.set(false);
    this.saveErrorKey.set('resource.saveError');
    this.conflict.set(false);
    this.conflictVersions.set(null);
    const submittedVersion = String(component.version);
    let created: ResourceResponse | null = null;
    try {
      created = await firstValueFrom(
        this.resources.add(this.projectId(), component.id, {
          description: this.description().trim(),
          expectedVersion: component.version,
          isExisting: this.existingMode(),
          logicalName,
          properties: {},
          resourceGroupId,
          tags: {},
          type: this.type(),
        }),
      );

      if (this.existingMode()) {
        for (const target of this.targets()) {
          const resourceId = this.existingIds()[target.id]?.trim();
          if (resourceId) {
            created = await firstValueFrom(
              this.resources.setExistingResourceId(this.projectId(), component.id, created.id, {
                expectedVersion: created.version,
                resourceId,
                targetId: target.id,
              }),
            );
          }
        }
      } else {
        for (const target of this.targets()) {
          if (!this.targetIsPresent(target.id)) {
            created = await firstValueFrom(
              this.resources.setPresence(this.projectId(), component.id, created.id, {
                expectedVersion: created.version,
                isPresent: false,
                targetId: target.id,
              }),
            );
          }
        }
      }

      await this.router.navigate([
        '/o',
        this.orgId(),
        'projects',
        this.projectId(),
        'components',
        component.id,
        'resources',
        created.id,
      ]);
    } catch (error) {
      const versionConflict = error instanceof ApiError && error.status === 409;
      this.conflict.set(versionConflict);
      this.saveError.set(true);
      this.saveErrorKey.set(
        error instanceof ApiError && error.code === 'RESOURCE_EXISTING_TYPE_MISMATCH'
          ? 'resource.existingTypeMismatch'
          : error instanceof ApiError && error.code === 'RESOURCE_EXISTING_ID_INVALID'
            ? 'resource.invalidExistingId'
            : 'resource.saveError',
      );
      if (created) {
        try {
          await firstValueFrom(
            this.resources.delete(this.projectId(), component.id, created.id, {
              expectedVersion: created.version,
            }),
          );
        } catch {
          // Preserve the original validation error; the resource remains visible if cleanup conflicts.
        }
      }
      if (versionConflict || created) {
        try {
          const latest = await firstValueFrom(this.components.get(this.projectId(), component.id));
          this.component.set(latest);
          if (versionConflict) {
            this.conflictVersions.set({
              current: String(latest.version),
              submitted: submittedVersion,
            });
          }
        } catch {
          if (versionConflict) {
            this.conflictVersions.set({ current: '—', submitted: submittedVersion });
          }
        }
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected resumeCreate(): void {
    this.conflict.set(false);
    this.conflictVersions.set(null);
    void this.create();
  }

  protected abandonCreate(): void {
    void this.router.navigate(['../'], { relativeTo: this.route });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, component, environments] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.components.get(this.projectId(), this.componentId())),
        firstValueFrom(this.projects.environments(this.projectId())),
      ]);
      const [types, permissions] = await Promise.all([
        firstValueFrom(this.catalog.types(project.catalogVersion)),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      const options = this.targetsFor(component, environments);
      this.project.set(project);
      this.component.set(component);
      this.typeSummary.set(types.find((item) => item.type === this.type()) ?? null);
      this.canEditModel.set(permissions.permissions.includes('model.edit'));
      this.targets.set(options);
      this.resourceGroupId.set(component.resourceGroups[0]?.id ?? '');
      this.existingIds.set(Object.fromEntries(options.map((target) => [target.id, ''])));
      this.targetPresence.set(Object.fromEntries(options.map((target) => [target.id, true])));
      this.queueNamePreview();
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private queueNamePreview(): void {
    if (this.previewTimer) clearTimeout(this.previewTimer);
    const sequence = ++this.previewSequence;
    const logicalName = this.logicalName().trim();
    const component = this.component();
    const resourceGroupId = this.resourceGroupId();
    if (this.existingMode() || !logicalName || !component || !resourceGroupId) {
      this.generatedNames.set([]);
      this.generatedNamesLoading.set(false);
      return;
    }

    this.generatedNamesLoading.set(true);
    this.generatedNamesError.set(false);
    this.previewTimer = setTimeout(() => {
      void this.loadNamePreview(sequence, logicalName, component.id, resourceGroupId);
    }, 180);
  }

  private async loadNamePreview(
    sequence: number,
    logicalName: string,
    componentId: string,
    resourceGroupId: string,
  ): Promise<void> {
    try {
      const preview = await firstValueFrom(
        this.resources.previewName(this.projectId(), componentId, {
          logicalName,
          resourceGroupId,
          type: this.type(),
        }),
      );
      if (sequence !== this.previewSequence) return;
      const targetsById = new Map(this.targets().map((target) => [target.id, target]));
      this.generatedNames.set(this.previewRows(preview.items, targetsById));
      this.generatedNamesError.set(preview.issues.length > 0 && preview.items.length === 0);
    } catch {
      if (sequence === this.previewSequence) this.generatedNamesError.set(true);
    } finally {
      if (sequence === this.previewSequence) this.generatedNamesLoading.set(false);
    }
  }

  private previewRows(
    items: ProjectNamingPreviewItemResponse[],
    targetsById: Map<string, ResourceTargetOption>,
  ): GeneratedNameRow[] {
    return items.flatMap((item) => {
      const target = targetsById.get(item.targetId);
      return target
        ? [
            {
              targetCode: item.targetCode,
              region: target.region,
              resourceGroupName: item.resourceGroupName,
              resourceName: item.resourceName,
            },
          ]
        : [];
    });
  }

  private targetsFor(
    component: ComponentResponse,
    environments: ProjectEnvironmentResponse[],
  ): ResourceTargetOption[] {
    if (component.singleTarget) {
      return [
        {
          id: component.singleTarget.id,
          code: component.singleTarget.code,
          region: component.singleTarget.region,
        },
      ];
    }

    const selected = new Set(component.targetEnvironmentIds);
    return environments
      .filter((environment) => component.targetsAllEnvironments || selected.has(environment.id))
      .map((environment) => ({
        id: environment.id,
        code: environment.code,
        region: environment.region,
      }));
  }
}
