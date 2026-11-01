import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  CatalogResourceTypeSummaryResponse,
  ComponentResponse,
  JsonElement,
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
import { DsCidrField } from '../../ds/cidr-field/cidr-field';
import { DsResourceIcon } from '../../ds/resource-icon/resource-icon';
import { GeneratedNames } from '../../shared/generated-names/generated-names';
import type { GeneratedNameRow } from '../../shared/generated-names/generated-name-row';
import {
  isPropertyEditable,
  parseResourceTypeProperties,
  type ResourcePropertyDefinition,
} from '../../shared/property-grid/property-grid.model';

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
    DsCidrField,
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
export class ResourceCreate implements OnChanges, OnInit, OnDestroy {
  readonly orgId = input.required<string>();
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  readonly type = input.required<string>();
  readonly existing = input<string | boolean>('');

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly typeSummary = signal<CatalogResourceTypeSummaryResponse | null>(null);
  protected readonly propertyDefinitions = signal<ResourcePropertyDefinition[]>([]);
  protected readonly initialProperties = signal<Record<string, JsonElement>>({});
  protected readonly initialPropertyErrors = signal<Record<string, string>>({});
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
  protected readonly createdNavigationFailed = signal(false);
  protected readonly partialCreateCleanupFailed = signal(false);
  protected readonly partialCreateCleanupRetryFailed = signal(false);
  protected readonly partialCreateResource = signal<ResourceResponse | null>(null);
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
  private readonly transloco = inject(TranslocoService);
  private readonly projects = inject(ProjectsService);
  private readonly components = inject(ComponentsService);
  private readonly catalog = inject(CatalogService);
  private readonly resources = inject(ResourcesService);
  private previewTimer: ReturnType<typeof setTimeout> | null = null;
  private previewSequence = 0;
  private loadSequence = 0;
  private initialized = false;

  ngOnChanges(): void {
    if (this.initialized) {
      this.conflict.set(false);
      this.conflictVersions.set(null);
      void this.load();
    }
  }

  ngOnInit(): void {
    this.initialized = true;
    void this.load();
  }

  ngOnDestroy(): void {
    this.loadSequence++;
    this.cancelNamePreview();
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

  protected initialPropertyDefinitions(): ResourcePropertyDefinition[] {
    const definitions = this.propertyDefinitions();
    const definitionsByName = new Map(
      definitions.map((definition) => [definition.name, definition]),
    );
    const values = new Map(
      definitions.map((definition) => [definition.name, this.initialPropertyValue(definition)]),
    );
    return definitions.filter(
      (definition) =>
        isPropertyEditable(definition) &&
        this.isInitialPropertyApplicable(definition, definitionsByName, values, new Set()),
    );
  }

  protected propertyLabel(definition: ResourcePropertyDefinition): string {
    const language = this.transloco.getActiveLang() === 'en' ? 'en' : 'fr';
    return definition.labels?.[language] ?? definition.labels?.fr ?? definition.name;
  }

  protected initialPropertyValue(definition: ResourcePropertyDefinition): JsonElement | null {
    return Object.prototype.hasOwnProperty.call(this.initialProperties(), definition.name)
      ? this.initialProperties()[definition.name]
      : (definition.default ?? null);
  }

  protected isBooleanDefinition(definition: ResourcePropertyDefinition): boolean {
    return definition.kind === 'boolean';
  }

  protected isNumericDefinition(definition: ResourcePropertyDefinition): boolean {
    return definition.kind === 'integer' || definition.kind === 'number';
  }

  protected isCidrList(definition: ResourcePropertyDefinition): boolean {
    return definition.kind === 'array' && definition.itemsFormat === 'ipv4Cidr';
  }

  protected initialPropertyError(definition: ResourcePropertyDefinition): string | null {
    return this.initialPropertyErrors()[definition.name] ?? null;
  }

  protected stringArray(value: JsonElement | null): string[] {
    return Array.isArray(value)
      ? value.filter((item): item is string => typeof item === 'string')
      : [];
  }

  protected formatInitialValue(value: JsonElement | null): string {
    if (value === null) return '';
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }

  protected serializeInitialValue(value: JsonElement | null): string {
    return value === null ? '__unset' : JSON.stringify(value);
  }

  protected formatInitialArray(value: JsonElement | null): string {
    return JSON.stringify(Array.isArray(value) ? value : []);
  }

  protected initialCidrFieldId(definition: ResourcePropertyDefinition): string {
    return `initial-property-${definition.name}`;
  }

  protected onInitialPropertyChange(event: Event, definition: ResourcePropertyDefinition): void {
    const control = event.target;
    if (control instanceof HTMLInputElement) {
      if (this.isBooleanDefinition(definition)) {
        this.clearInitialPropertyError(definition.name, control);
        this.setInitialProperty(definition, control.checked);
        return;
      }
      if (this.isNumericDefinition(definition)) {
        if (control.validity.badInput) {
          this.setInitialPropertyError(definition, 'resource.invalidInitialProperty', control);
          return;
        }
        if (control.value === '') {
          this.clearInitialProperty(definition.name);
          this.clearInitialPropertyError(definition.name, control);
          return;
        }
        const value = control.valueAsNumber;
        if (!Number.isFinite(value)) {
          this.setInitialPropertyError(definition, 'resource.invalidInitialProperty', control);
          return;
        }
        if (!this.validateInitialProperty(definition, value, control)) return;
        this.setInitialProperty(definition, value);
        return;
      }
      if (!this.validateInitialProperty(definition, control.value, control)) return;
      this.setInitialProperty(definition, control.value);
      return;
    }
    if (control instanceof HTMLTextAreaElement && definition.kind === 'array') {
      try {
        const value: unknown = JSON.parse(control.value);
        if (!Array.isArray(value)) throw new TypeError('Expected an array.');
        if (!this.validateInitialProperty(definition, value as JsonElement[], control)) return;
        this.setInitialProperty(definition, value as JsonElement[]);
      } catch {
        this.setInitialPropertyError(definition, 'resource.arrayInvalid', control);
      }
    }
  }

  protected onInitialOptionChange(definition: ResourcePropertyDefinition, value: string): void {
    if (value === '__unset') {
      this.clearInitialProperty(definition.name);
      return;
    }
    try {
      const parsed: unknown = JSON.parse(value);
      this.setInitialProperty(definition, parsed as JsonElement);
    } catch {
      // A malformed option is not a catalog value and must not be submitted.
    }
  }

  protected updateInitialCidrValue(definition: ResourcePropertyDefinition, value: string[]): void {
    if (this.initialPropertyValidationError(definition, value)) return;
    this.clearInitialPropertyError(definition.name);
    this.setInitialProperty(definition, value);
  }

  private setInitialProperty(definition: ResourcePropertyDefinition, value: JsonElement): void {
    if (
      definition.default !== undefined &&
      JSON.stringify(definition.default) === JSON.stringify(value)
    ) {
      this.initialProperties.update((current) => {
        const next = { ...current };
        delete next[definition.name];
        return next;
      });
      return;
    }
    this.initialProperties.update((current) => ({ ...current, [definition.name]: value }));
  }

  private clearInitialProperty(name: string): void {
    this.initialProperties.update((current) => {
      const next = { ...current };
      delete next[name];
      return next;
    });
  }

  private validateInitialProperty(
    definition: ResourcePropertyDefinition,
    value: JsonElement,
    control: HTMLInputElement | HTMLTextAreaElement,
  ): boolean {
    const errorKey = this.initialPropertyValidationError(definition, value);
    if (errorKey) {
      this.setInitialPropertyError(definition, errorKey, control);
      return false;
    }
    this.clearInitialPropertyError(definition.name, control);
    return true;
  }

  private initialPropertyValidationError(
    definition: ResourcePropertyDefinition,
    value: JsonElement,
  ): string | null {
    if (this.isNumericDefinition(definition)) {
      if (typeof value !== 'number') return 'resource.invalidInitialProperty';
      if (definition.kind === 'integer' && !Number.isInteger(value)) {
        return 'resource.invalidInitialProperty';
      }
      if (definition.minimum !== undefined && value < definition.minimum) {
        return 'resource.invalidInitialProperty';
      }
      if (definition.maximum !== undefined && value > definition.maximum) {
        return 'resource.invalidInitialProperty';
      }
      return null;
    }
    if (definition.kind === 'string' || definition.kind === 'reference') {
      if (typeof value !== 'string') return 'resource.invalidInitialProperty';
      if (definition.minLength !== undefined && value.length < definition.minLength) {
        return 'resource.invalidInitialProperty';
      }
      if (definition.maxLength !== undefined && value.length > definition.maxLength) {
        return 'resource.invalidInitialProperty';
      }
      if (definition.pattern !== undefined) {
        try {
          if (!new RegExp(definition.pattern).test(value)) return 'resource.invalidInitialProperty';
        } catch {
          return 'resource.invalidInitialProperty';
        }
      }
      return null;
    }
    if (definition.kind === 'array') {
      if (
        !Array.isArray(value) ||
        (definition.maxItems !== undefined && value.length > definition.maxItems)
      ) {
        return 'resource.invalidInitialProperty';
      }
      if (
        definition.itemsKind &&
        !value.every((item) => this.matchesArrayItemKind(item, definition))
      ) {
        return 'resource.invalidInitialProperty';
      }
    }
    return null;
  }

  private matchesArrayItemKind(
    value: JsonElement,
    definition: ResourcePropertyDefinition,
  ): boolean {
    switch (definition.itemsKind) {
      case 'boolean':
        return typeof value === 'boolean';
      case 'integer':
        return typeof value === 'number' && Number.isInteger(value);
      case 'number':
        return typeof value === 'number' && Number.isFinite(value);
      case 'string':
      case 'reference':
        return typeof value === 'string';
      case 'enum':
        return (
          definition.itemsValues?.some(
            (allowedValue) => JSON.stringify(allowedValue) === JSON.stringify(value),
          ) ?? false
        );
      case 'array':
        return Array.isArray(value);
      case 'object':
        return typeof value === 'object' && value !== null && !Array.isArray(value);
      default:
        return true;
    }
  }

  private setInitialPropertyError(
    definition: ResourcePropertyDefinition,
    errorKey: string,
    control: HTMLInputElement | HTMLTextAreaElement,
  ): void {
    this.initialPropertyErrors.update((current) => ({ ...current, [definition.name]: errorKey }));
    control.setCustomValidity(
      this.transloco.translate(errorKey, { property: this.propertyLabel(definition) }),
    );
    control.reportValidity();
  }

  private clearInitialPropertyError(
    name: string,
    control?: HTMLInputElement | HTMLTextAreaElement,
  ): void {
    if (control) control.setCustomValidity('');
    this.initialPropertyErrors.update((current) => {
      if (!(name in current)) return current;
      const next = { ...current };
      delete next[name];
      return next;
    });
  }

  private hasInvalidApplicableInitialProperty(): boolean {
    return this.initialPropertyDefinitions().some((definition) =>
      Object.prototype.hasOwnProperty.call(this.initialPropertyErrors(), definition.name),
    );
  }

  private applicableInitialProperties(): Record<string, JsonElement> {
    const applicable = new Set(
      this.initialPropertyDefinitions().map((definition) => definition.name),
    );
    return Object.fromEntries(
      Object.entries(this.initialProperties()).filter(([name]) => applicable.has(name)),
    );
  }

  private isInitialPropertyApplicable(
    definition: ResourcePropertyDefinition,
    definitions: Map<string, ResourcePropertyDefinition>,
    values: Map<string, JsonElement | null>,
    evaluationPath: Set<string>,
  ): boolean {
    const condition = definition.condition;
    if (!condition) return true;
    if (evaluationPath.has(definition.name)) return false;

    const prefix = condition.startsWith('requires:')
      ? 'requires:'
      : condition.startsWith('requiredWhen:')
        ? 'requiredWhen:'
        : null;
    if (!prefix) return false;

    const requirement = condition.slice(prefix.length);
    const equalsIndex = requirement.indexOf('=');
    const dependencyName = equalsIndex < 0 ? requirement : requirement.slice(0, equalsIndex);
    const dependency = definitions.get(dependencyName);
    if (!dependency) return false;

    evaluationPath.add(definition.name);
    const dependencyApplicable = this.isInitialPropertyApplicable(
      dependency,
      definitions,
      values,
      evaluationPath,
    );
    evaluationPath.delete(definition.name);
    if (!dependencyApplicable) return false;

    const value = values.get(dependencyName);
    if (value === null || value === undefined) return false;
    if (equalsIndex < 0) return true;

    const expected = requirement.slice(equalsIndex + 1);
    if (typeof value === 'number') return Number(expected) === value;
    if (typeof value === 'string' || typeof value === 'boolean') return String(value) === expected;
    return false;
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
      !component.resourceGroups.some((group) => group.id === resourceGroupId) ||
      !logicalName ||
      this.saving() ||
      this.createdNavigationFailed() ||
      this.partialCreateCleanupFailed()
    ) {
      return;
    }
    if (this.existingMode() && !this.allExistingIdsProvided()) return;
    if (!this.existingMode() && this.hasInvalidApplicableInitialProperty()) return;

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
          properties: this.existingMode() ? {} : this.applicableInitialProperties(),
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

      try {
        const navigated = await this.router.navigate([
          '/o',
          this.orgId(),
          'projects',
          this.projectId(),
          'components',
          component.id,
          'resources',
          created.id,
        ]);
        if (!navigated) this.createdNavigationFailed.set(true);
      } catch {
        this.createdNavigationFailed.set(true);
      }
    } catch (error) {
      const versionConflict =
        error instanceof ApiError && error.status === 409 && error.code === 'CONFLICT_VERSION';
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
          this.partialCreateCleanupFailed.set(true);
          this.partialCreateCleanupRetryFailed.set(false);
          this.partialCreateResource.set(created);
          this.conflict.set(false);
          this.conflictVersions.set(null);
          if (this.saveErrorKey() === 'resource.saveError') this.saveError.set(false);
        }
      }
      if (versionConflict || created) {
        try {
          const latest = await firstValueFrom(this.components.get(this.projectId(), component.id));
          this.component.set(latest);
          this.selectSurvivingResourceGroup(latest);
          if (this.partialCreateCleanupFailed()) {
            const partial = latest.resources.find((resource) => resource.id === created?.id);
            if (partial) {
              this.partialCreateResource.set(partial);
            } else {
              this.partialCreateResource.set(null);
              this.partialCreateCleanupFailed.set(false);
            }
          }
          if (versionConflict) {
            if (!this.partialCreateCleanupFailed()) {
              this.conflictVersions.set({
                current: String(latest.version),
                submitted: submittedVersion,
              });
            }
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
    void this.router.navigate(['../../../'], { relativeTo: this.route });
  }

  protected async retryPartialCreateCleanup(): Promise<void> {
    const pendingPartial = this.partialCreateResource();
    if (!pendingPartial || this.saving()) return;

    this.saving.set(true);
    this.partialCreateCleanupRetryFailed.set(false);
    try {
      const latest = await firstValueFrom(
        this.components.get(this.projectId(), this.componentId()),
      );
      this.component.set(latest);
      const partial = latest.resources.find((resource) => resource.id === pendingPartial.id);
      if (!partial) {
        this.partialCreateResource.set(null);
        this.partialCreateCleanupFailed.set(false);
        this.saveError.set(false);
        await this.load();
        return;
      }
      this.partialCreateResource.set(partial);

      await firstValueFrom(
        this.resources.delete(this.projectId(), this.componentId(), partial.id, {
          expectedVersion: partial.version,
        }),
      );
      this.partialCreateResource.set(null);
      this.partialCreateCleanupFailed.set(false);
      this.partialCreateCleanupRetryFailed.set(false);
      this.saveError.set(false);
      await this.load();
    } catch {
      this.partialCreateCleanupRetryFailed.set(true);
      try {
        const latest = await firstValueFrom(
          this.components.get(this.projectId(), this.componentId()),
        );
        this.component.set(latest);
        const currentPartial = latest.resources.find(
          (resource) => resource.id === pendingPartial.id,
        );
        if (currentPartial) {
          this.partialCreateResource.set(currentPartial);
        } else {
          this.partialCreateResource.set(null);
          this.partialCreateCleanupFailed.set(false);
          this.partialCreateCleanupRetryFailed.set(false);
          this.saveError.set(false);
          await this.load();
        }
      } catch {
        // Keep the cleanup warning and show that the retry could not be verified.
      }
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    const sequence = ++this.loadSequence;
    this.cancelNamePreview();
    this.loading.set(true);
    this.loadError.set(false);
    this.generatedNames.set([]);
    this.generatedNamesLoading.set(false);
    this.generatedNamesError.set(false);
    this.logicalName.set('');
    this.description.set('');
    this.saveError.set(false);
    this.createdNavigationFailed.set(false);
    try {
      const [project, component, environments] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.components.get(this.projectId(), this.componentId())),
        firstValueFrom(this.projects.environments(this.projectId())),
      ]);
      const [types, descriptor, permissions] = await Promise.all([
        firstValueFrom(this.catalog.types(project.catalogVersion)),
        firstValueFrom(this.catalog.type(project.catalogVersion, this.type())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      if (sequence !== this.loadSequence) return;
      const options = this.targetsFor(component, environments);
      this.project.set(project);
      this.component.set(component);
      this.typeSummary.set(types.find((item) => item.type === this.type()) ?? null);
      this.propertyDefinitions.set(parseResourceTypeProperties(descriptor));
      this.initialProperties.set({});
      this.initialPropertyErrors.set({});
      this.canEditModel.set(permissions.permissions.includes('model.edit'));
      this.targets.set(options);
      this.selectSurvivingResourceGroup(component);
      this.existingIds.set(Object.fromEntries(options.map((target) => [target.id, ''])));
      this.targetPresence.set(Object.fromEntries(options.map((target) => [target.id, true])));
      this.queueNamePreview();
    } catch {
      if (sequence === this.loadSequence) this.loadError.set(true);
    } finally {
      if (sequence === this.loadSequence) this.loading.set(false);
    }
  }

  private selectSurvivingResourceGroup(component: ComponentResponse): void {
    const currentId = this.resourceGroupId();
    if (component.resourceGroups.some((group) => group.id === currentId)) return;

    const fallbackId = component.resourceGroups[0]?.id ?? '';
    this.resourceGroupId.set(fallbackId);
    this.queueNamePreview();
  }

  private cancelNamePreview(): void {
    if (this.previewTimer) {
      clearTimeout(this.previewTimer);
      this.previewTimer = null;
    }
    this.previewSequence++;
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
      if (sequence === this.previewSequence) {
        this.generatedNames.set([]);
        this.generatedNamesError.set(true);
      }
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
