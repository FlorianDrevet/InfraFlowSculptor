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
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  CatalogResourceTypeSummaryResponse,
  ComponentResponse,
  EffectiveValueTargetResponse,
  ProjectNamingPreviewItemResponse,
  ProjectSummaryResponse,
  ResourceResponse,
} from '../../core/api/generated';
import type { JsonElement } from '../../core/api/generated/models/json-element';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';
import { DsResourceIcon } from '../../ds/resource-icon/resource-icon';
import { GeneratedNames } from '../../shared/generated-names/generated-names';
import type { GeneratedNameRow } from '../../shared/generated-names/generated-name-row';
import { ResourceLinksPanel } from './resource-links-panel';
import { ResourceIdentityPanel } from './resource-identity-panel';
import { ResourceAppSettingsPanel } from './resource-app-settings-panel';
import { ResourceApplicationPanel } from './resource-application-panel';
import { ResourceNetworkPanel } from './resource-network-panel';
import { PropertyGrid } from '../../shared/property-grid/property-grid';
import type { PropertyGridUpdate } from '../../shared/property-grid/property-grid-update';
import {
  createClearOverrideRequest,
  createPropertyGridRows,
  parseResourceTypeProperties,
  type ResourcePropertyDefinition,
} from '../../shared/property-grid/property-grid.model';

@Component({
  selector: 'app-resource-editor',
  imports: [
    CdkTrapFocus,
    DsButton,
    DsResourceIcon,
    FormsModule,
    GeneratedNames,
    PropertyGrid,
    ResourceLinksPanel,
    ResourceIdentityPanel,
    ResourceAppSettingsPanel,
    ResourceApplicationPanel,
    ResourceNetworkPanel,
    RouterLink,
    TranslocoPipe,
  ],
  templateUrl: './resource-editor.html',
  styleUrl: './resource-editor.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceEditor implements OnInit {
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  readonly resourceId = input.required<string>();
  readonly tab = input('general');

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly resource = signal<ResourceResponse | null>(null);
  protected readonly resourceType = signal<CatalogResourceTypeSummaryResponse | null>(null);
  protected readonly applicationSettingsAvailable = signal(false);
  protected readonly applicationAvailable = computed(
    () => this.resource()?.type === 'ContainerApp',
  );
  protected readonly networkAvailable = signal(false);
  protected readonly effectiveValues = signal<EffectiveValueTargetResponse[]>([]);
  protected readonly definitions = signal<ResourcePropertyDefinition[]>([]);
  protected readonly existingIdDrafts = signal<Record<string, string>>({});
  protected readonly forcedNameDrafts = signal<Record<string, string>>({});
  protected readonly showForcedNameEditor = signal(false);
  protected readonly descriptionDraft = signal('');
  protected readonly tagsDraft = signal('');
  protected readonly tagError = signal(false);
  protected readonly generatedNames = signal<GeneratedNameRow[]>([]);
  protected readonly generatedNamesLoading = signal(false);
  protected readonly generatedNamesError = signal(false);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly canEditModel = signal(false);
  protected readonly actionError = signal(false);
  protected readonly conflict = signal<{
    label: string;
    original: string;
    saved: string;
    submitted: string;
    baseVersion: string;
    currentVersion: string;
  } | null>(null);
  private readonly pendingConflictAction = signal<{
    action: (resource: ResourceResponse) => Promise<void>;
    submitted: string;
    original: string;
    label: string;
    saved: (resource: ResourceResponse) => string;
  } | null>(null);
  protected readonly rows = computed(() => {
    const resource = this.resource();
    return resource
      ? createPropertyGridRows(
          this.definitions(),
          this.propertiesRecord(resource.properties),
          this.effectiveValues(),
        )
      : [];
  });
  protected readonly networkRows = computed(() =>
    this.rows().filter(
      (row) => row.definition.name === 'exposure' || row.definition.itemsFormat === 'ipv4Cidr',
    ),
  );
  protected readonly activeTab = computed(() =>
    this.isSupportedTab(this.tab()) ? this.tab() : 'general',
  );

  private readonly projects = inject(ProjectsService);
  private readonly components = inject(ComponentsService);
  private readonly catalog = inject(CatalogService);
  private readonly resources = inject(ResourcesService);
  private readonly transloco = inject(TranslocoService);

  ngOnInit(): void {
    void this.load(true);
  }

  protected updateExistingId(targetId: string, value: string): void {
    this.existingIdDrafts.update((current) => ({ ...current, [targetId]: value }));
  }

  protected updateDescription(value: string): void {
    this.descriptionDraft.set(value);
  }

  protected updateTags(value: string): void {
    this.tagsDraft.set(value);
    this.tagError.set(false);
  }

  protected currentForcedName(targetId: string): string {
    return this.forcedNameDrafts()[targetId] ?? this.resource()?.forcedNames[targetId] ?? '';
  }

  protected updateForcedName(targetId: string, value: string): void {
    this.forcedNameDrafts.update((current) => ({ ...current, [targetId]: value }));
  }

  protected startForcedNameEdit(): void {
    this.showForcedNameEditor.set(true);
  }

  protected async saveForcedName(target: EffectiveValueTargetResponse): Promise<void> {
    const resource = this.resource();
    const name = this.currentForcedName(target.id).trim();
    if (!resource || !name || this.saving()) return;
    await this.runAction(
      (current) =>
        firstValueFrom(
          this.resources.setForcedName(this.projectId(), this.componentId(), current.id, {
            expectedVersion: current.version,
            name,
            targetId: target.id,
          }),
        ).then(() => undefined),
      {
        label: this.transloco.translate('resource.forcedName'),
        original: resource.forcedNames[target.id] ?? '',
        submitted: name,
        saved: (current) => current.forcedNames[target.id] ?? '',
      },
    );
  }

  protected canTogglePresence(target: EffectiveValueTargetResponse): boolean {
    return (
      this.canEditModel() &&
      !this.saving() &&
      (!this.resource()?.isExisting || !!this.currentExistingId(target.id))
    );
  }

  protected async saveMetadata(): Promise<void> {
    const resource = this.resource();
    if (!resource || this.saving()) return;
    const tags = this.parseTags(this.tagsDraft());
    if (!tags) {
      this.tagError.set(true);
      return;
    }

    const description = this.descriptionDraft().trim();
    const original = `${resource.description}\n${this.entriesText(resource.tags)}`;
    const submitted = `${description}\n${this.tagsDraft()}`;
    await this.runAction(
      (current) =>
        firstValueFrom(
          this.resources.updateMetadata(this.projectId(), this.componentId(), current.id, {
            description,
            expectedVersion: current.version,
            tags,
          }),
        ).then(() => undefined),
      {
        label: 'resource.metadata',
        original,
        submitted,
        saved: (current) => `${current.description}\n${this.entriesText(current.tags)}`,
      },
    );
  }

  protected async setPresence(target: EffectiveValueTargetResponse): Promise<void> {
    const nextPresence = !target.isPresent;
    const resource = this.resource();
    if (!resource || !this.canTogglePresence(target)) return;
    await this.runAction(
      (current) =>
        firstValueFrom(
          this.resources.setPresence(this.projectId(), this.componentId(), current.id, {
            expectedVersion: current.version,
            isPresent: nextPresence,
            targetId: target.id,
          }),
        ).then(() => undefined),
      {
        label: 'resource.presence',
        original: this.transloco.translate(
          target.isPresent ? 'resource.present' : 'resource.absent',
        ),
        submitted: this.transloco.translate(nextPresence ? 'resource.present' : 'resource.absent'),
        saved: () =>
          this.transloco.translate(
            this.effectiveValues().find((item) => item.id === target.id)?.isPresent
              ? 'resource.present'
              : 'resource.absent',
          ),
      },
    );
  }

  protected retryConflict(): void {
    const pending = this.pendingConflictAction();
    if (!pending) return;
    this.conflict.set(null);
    this.pendingConflictAction.set(null);
    void this.runAction(pending.action, {
      label: pending.label,
      original: pending.original,
      submitted: pending.submitted,
      saved: pending.saved,
    });
  }

  protected abandonConflict(): void {
    this.conflict.set(null);
    this.pendingConflictAction.set(null);
    this.actionError.set(false);
    void this.load(true);
  }

  protected async saveExistingId(targetId: string): Promise<void> {
    const resource = this.resource();
    const resourceId = this.existingIdDrafts()[targetId]?.trim();
    if (!resource || !resourceId || this.saving()) return;

    await this.runAction(
      (current) =>
        firstValueFrom(
          this.resources.setExistingResourceId(this.projectId(), this.componentId(), current.id, {
            expectedVersion: current.version,
            resourceId,
            targetId,
          }),
        ).then(() => undefined),
      {
        label: this.transloco.translate('resource.existingIds'),
        original: resource.existingIds[targetId] ?? '',
        submitted: resourceId,
        saved: (current) => current.existingIds[targetId] ?? '',
      },
    );
  }

  protected async updateProperty(update: PropertyGridUpdate): Promise<void> {
    const resource = this.resource();
    if (!resource || this.saving()) return;

    const action = (current: ResourceResponse): Promise<void> => {
      if (update.targetId) {
        return firstValueFrom(
          this.resources.setOverride(this.projectId(), this.componentId(), current.id, {
            expectedVersion: current.version,
            property: update.property,
            targetId: update.targetId,
            value: update.value,
          }),
        ).then(() => undefined);
      }
      return firstValueFrom(
        this.resources.setProperties(this.projectId(), this.componentId(), current.id, {
          expectedVersion: current.version,
          properties: { [update.property]: update.value },
        }),
      ).then(() => undefined);
    };
    await this.runAction(action, {
      label:
        this.definitions().find((item) => item.name === update.property)?.labels?.fr ??
        update.property,
      original: this.currentPropertyValue(resource, update.property, update.targetId),
      submitted: this.valueText(update.value),
      saved: (current) => this.currentPropertyValue(current, update.property, update.targetId),
    });
  }

  protected async clearOverride(property: string, targetId: string): Promise<void> {
    const resource = this.resource();
    if (!resource || this.saving()) return;
    const definition = this.definitions().find((item) => item.name === property);
    const row = this.rows().find((item) => item.definition.name === property);
    const cell = row?.targets.find((item) => item.targetId === targetId);
    const request = definition && cell ? createClearOverrideRequest(definition, cell) : undefined;
    if (!request) return;

    await this.runAction(
      (current) =>
        firstValueFrom(
          this.resources.clearOverride(this.projectId(), this.componentId(), current.id, {
            ...request,
            expectedVersion: current.version,
          }),
        ).then(() => undefined),
      {
        label: this.definitions().find((item) => item.name === property)?.labels?.fr ?? property,
        original: this.currentPropertyValue(resource, property, targetId),
        submitted: this.transloco.translate('resource.catalogDefault'),
        saved: (current) => this.currentPropertyValue(current, property, targetId),
      },
    );
  }

  protected targetLabel(target: EffectiveValueTargetResponse): string {
    return `${target.code} · ${target.region}`;
  }

  protected hasForcedNames(): boolean {
    return Object.keys(this.resource()?.forcedNames ?? {}).length > 0;
  }

  protected resourceTypeLabel(): string {
    return this.resourceType()?.labelFr ?? this.resource()?.type ?? '';
  }

  protected resourceGroupName(): string {
    const resource = this.resource();
    return (
      this.component()?.resourceGroups.find((group) => group.id === resource?.resourceGroupId)
        ?.name ?? this.transloco.translate('resource.resourceGroupMissing')
    );
  }

  protected currentExistingId(targetId: string): string {
    return this.existingIdDrafts()[targetId] ?? this.resource()?.existingIds[targetId] ?? '';
  }

  private async runAction(
    action: (resource: ResourceResponse) => Promise<void>,
    details: {
      label: string;
      original: string;
      submitted: string;
      saved: (resource: ResourceResponse) => string;
    },
  ): Promise<void> {
    const resource = this.resource();
    if (!resource) return;
    this.saving.set(true);
    this.actionError.set(false);
    this.conflict.set(null);
    try {
      await action(resource);
      await this.load(false);
    } catch (error) {
      this.actionError.set(true);
      const latest = await this.load(false);
      if (error instanceof ApiError && error.status === 409 && latest) {
        this.conflict.set({
          label: details.label,
          original: details.original,
          saved: details.saved(latest),
          submitted: details.submitted,
          baseVersion: String(resource.version),
          currentVersion: String(latest.version),
        });
        this.pendingConflictAction.set({ action, ...details });
      }
    } finally {
      this.saving.set(false);
    }
  }

  private async load(resetDrafts: boolean): Promise<ResourceResponse | null> {
    const initializeDrafts = resetDrafts || this.resource() === null;
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, component, effectiveValues, permissions] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.components.get(this.projectId(), this.componentId())),
        firstValueFrom(this.resources.effectiveValues(this.projectId(), this.resourceId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      const resource = component.resources.find((item) => item.id === this.resourceId()) ?? null;
      if (!resource) throw new Error('Resource not found in component response.');
      const [descriptor, resourceTypes] = await Promise.all([
        firstValueFrom(this.catalog.type(project.catalogVersion, resource.type)),
        firstValueFrom(this.catalog.types(project.catalogVersion)),
      ]);
      this.project.set(project);
      this.component.set(component);
      this.resource.set(resource);
      this.resourceType.set(resourceTypes.find((item) => item.type === resource.type) ?? null);
      this.applicationSettingsAvailable.set(this.applicationSettingsSupported(descriptor));
      this.networkAvailable.set(this.networkExposureSupported(descriptor));
      this.canEditModel.set(permissions.permissions.includes('model.edit'));
      this.effectiveValues.set(effectiveValues);
      this.definitions.set(parseResourceTypeProperties(descriptor));
      if (initializeDrafts) {
        this.existingIdDrafts.set(
          Object.fromEntries(
            effectiveValues.map((target) => [target.id, resource.existingIds[target.id] ?? '']),
          ),
        );
        this.forcedNameDrafts.set({ ...resource.forcedNames });
        this.showForcedNameEditor.set(Object.keys(resource.forcedNames).length > 0);
        this.descriptionDraft.set(resource.description);
        this.tagsDraft.set(this.entriesText(resource.tags));
      }
      void this.loadGeneratedNames(component, resource, effectiveValues);
      return resource;
    } catch {
      this.loadError.set(true);
      return null;
    } finally {
      this.loading.set(false);
    }
  }

  private async loadGeneratedNames(
    component: ComponentResponse,
    resource: ResourceResponse,
    targets: EffectiveValueTargetResponse[],
  ): Promise<void> {
    if (resource.isExisting) {
      this.generatedNames.set([]);
      this.generatedNamesLoading.set(false);
      this.generatedNamesError.set(false);
      return;
    }
    this.generatedNamesLoading.set(true);
    this.generatedNamesError.set(false);
    try {
      const preview = await firstValueFrom(
        this.resources.previewName(this.projectId(), component.id, {
          logicalName: resource.logicalName,
          resourceGroupId: resource.resourceGroupId,
          resourceId: resource.id,
          type: resource.type,
        }),
      );
      const targetsById = new Map(targets.map((target) => [target.id, target]));
      const rows = preview.items.flatMap((item: ProjectNamingPreviewItemResponse) => {
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
      this.generatedNames.set(rows);
      this.generatedNamesError.set(preview.issues.length > 0 && rows.length === 0);
    } catch {
      this.generatedNamesError.set(true);
    } finally {
      this.generatedNamesLoading.set(false);
    }
  }

  private currentPropertyValue(
    resource: ResourceResponse,
    property: string,
    targetId: string | null,
  ): string {
    if (targetId) {
      const target = this.effectiveValues().find((item) => item.id === targetId);
      const value = target?.properties.find((item) => item.name === property)?.value;
      return this.valueText(value);
    }
    const resourceProperties = this.propertiesRecord(resource.properties);
    if (Object.prototype.hasOwnProperty.call(resourceProperties, property)) {
      return this.valueText(resourceProperties[property]);
    }
    const defaultValue = this.definitions().find((item) => item.name === property)?.default;
    return this.valueText(defaultValue);
  }

  private parseTags(value: string): Record<string, string> | null {
    const tags: Record<string, string> = {};
    for (const line of value.split(/\r?\n/)) {
      if (!line.trim()) continue;
      const separator = line.indexOf('=');
      const key = separator > 0 ? line.slice(0, separator).trim() : '';
      if (!key || Object.prototype.hasOwnProperty.call(tags, key)) return null;
      tags[key] = line.slice(separator + 1).trim();
    }
    return tags;
  }

  private entriesText(entries: Record<string, string>): string {
    return Object.entries(entries)
      .map(([key, value]) => `${key}=${value}`)
      .join('\n');
  }

  private valueText(value: unknown): string {
    if (value === null || value === undefined)
      return this.transloco.translate('resource.valueAbsent');
    return typeof value === 'string' ? value : JSON.stringify(value);
  }

  private propertiesRecord(value: object): Record<string, JsonElement> {
    if (value === null || Array.isArray(value)) return {};
    return value as Record<string, JsonElement>;
  }

  private isSupportedTab(tab: string): boolean {
    if (tab === 'properties') return !this.resource()?.isExisting;
    if (tab === 'application') return this.applicationAvailable();
    if (tab === 'settings') return this.applicationSettingsAvailable();
    if (tab === 'network') return this.networkAvailable();
    return tab === 'general' || tab === 'links' || tab === 'identity';
  }

  private applicationSettingsSupported(descriptor: JsonElement): boolean {
    const destination = this.descriptorSectionValue(
      descriptor,
      'applicationSettings',
      'destination',
    );
    return destination !== null && destination !== 'none';
  }

  private networkExposureSupported(descriptor: JsonElement): boolean {
    const exposure = this.descriptorSection(descriptor, 'exposure');
    const modes = exposure?.['modes'];
    return Array.isArray(modes) && modes.some((mode) => mode === 'Public' || mode === 'Restricted');
  }

  private descriptorSectionValue(
    descriptor: JsonElement,
    sectionName: string,
    propertyName: string,
  ): string | null {
    return (
      (this.descriptorSection(descriptor, sectionName)?.[propertyName] as string | undefined) ??
      null
    );
  }

  private descriptorSection(
    descriptor: JsonElement,
    sectionName: string,
  ): Record<string, JsonElement> | null {
    if (typeof descriptor !== 'object' || descriptor === null || Array.isArray(descriptor))
      return null;
    const section = descriptor[sectionName];
    return typeof section === 'object' && section !== null && !Array.isArray(section)
      ? section
      : null;
  }
}
