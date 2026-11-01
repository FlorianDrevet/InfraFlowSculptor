import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  AppSettingRequest,
  AppSettingResponse,
  AppSettingSourceKind,
  AppSettingUpdateRequest,
  ComponentResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';

interface ResourceOutputChoice {
  resourceId: string;
  componentCode: string;
  resourceName: string;
  resourceType: string;
  outputName: string;
}

interface ResourceOutputResource {
  resourceId: string;
  componentCode: string;
  resourceName: string;
  resourceType: string;
}

interface ProjectResourceChoice {
  id: string;
  componentId: string;
  componentCode: string;
  logicalName: string;
  type: string;
}

const PIPELINE_VARIABLE_NAME_PATTERN = /^[A-Za-z_][A-Za-z0-9_]{0,127}$/;
const SECRET_NAME_PATTERN = /^[A-Za-z0-9-]{1,127}$/;
const APP_SETTING_NAME_PATTERN = /^[A-Za-z_][A-Za-z0-9_.-]{0,255}$/;

@Component({
  selector: 'app-resource-app-settings-panel',
  imports: [DsButton, FormsModule, TranslocoPipe],
  templateUrl: './resource-app-settings-panel.html',
  styleUrl: './resource-app-settings-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceAppSettingsPanel implements OnInit {
  private readonly sourceKindTranslationKeys: Record<AppSettingSourceKind, string> = {
    Literal: 'resource.settingSource.Literal',
    ResourceOutput: 'resource.settingSource.ResourceOutput',
    KeyVaultSecret: 'resource.settingSource.KeyVaultSecret',
  };

  readonly projectId = input.required<string>();
  readonly component = input.required<ComponentResponse>();
  readonly resource = input.required<ResourceResponse>();
  readonly canEdit = input(false);

  protected readonly settings = signal<AppSettingResponse[]>([]);
  protected readonly outputChoices = signal<ResourceOutputChoice[]>([]);
  protected readonly projectResources = signal<ProjectResourceChoice[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly outputOptionsLoading = signal(false);
  protected readonly outputOptionsError = signal(false);
  protected readonly saving = signal(false);
  protected readonly actionError = signal(false);
  protected readonly settingConflict = signal(false);
  protected readonly secretDeclarationRequired = signal(false);
  protected readonly secretSuspectError = signal(false);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<AppSettingResponse | null>(null);
  protected readonly name = signal('');
  protected readonly sourceKind = signal<AppSettingSourceKind>('KeyVaultSecret');
  protected readonly literalValue = signal('');
  protected readonly overrides = signal<Record<string, string> | null>(null);
  protected readonly forceNonSecret = signal(false);
  protected readonly sourceResourceId = signal('');
  protected readonly outputName = signal('');
  protected readonly keyVaultResourceId = signal('');
  protected readonly secretName = signal('');
  protected readonly pipelineVariableName = signal('');
  protected readonly identityResourceId = signal('');
  protected readonly secretFeedKind = signal<'PipelineSecret' | 'ManagedOutsideIfs'>('PipelineSecret');

  protected readonly keyVaults = computed(() => this.projectResources().filter((resource) => resource.type === 'KeyVault'));
  protected readonly identities = computed(() => this.projectResources().filter((resource) =>
    resource.type === 'UserAssignedIdentity' && resource.componentId === this.component().id));
  protected readonly outputResources = computed(() => {
    const seen = new Set<string>();
    return this.outputChoices().flatMap((choice): ResourceOutputResource[] => {
      if (seen.has(choice.resourceId)) return [];
      seen.add(choice.resourceId);
      return [{
        resourceId: choice.resourceId,
        componentCode: choice.componentCode,
        resourceName: choice.resourceName,
        resourceType: choice.resourceType,
      }];
    });
  });
  protected readonly selectedOutputs = computed(() => this.outputChoices().filter((choice) => choice.resourceId === this.sourceResourceId()));
  protected readonly hasUnavailableReference = computed(() => {
    if (this.outputOptionsLoading() || this.outputOptionsError()) return false;
    if (this.sourceKind() === 'KeyVaultSecret') {
      const keyVaultResourceId = this.keyVaultResourceId();
      const identityResourceId = this.identityResourceId();
      return (keyVaultResourceId.length > 0
        && !this.keyVaults().some((vault) => vault.id === keyVaultResourceId))
        || (identityResourceId.length > 0
          && !this.identities().some((identity) => identity.id === identityResourceId));
    }
    if (this.sourceKind() === 'ResourceOutput') {
      const sourceResourceId = this.sourceResourceId();
      const outputName = this.outputName();
      return (sourceResourceId.length > 0
        && !this.outputResources().some((resource) => resource.resourceId === sourceResourceId))
        || (outputName.length > 0
          && !this.selectedOutputs().some((output) => output.outputName === outputName));
    }
    return false;
  });
  protected readonly canSubmit = computed(() => APP_SETTING_NAME_PATTERN.test(this.name().trim()) && !this.saving()
    && !this.settingConflict()
    && !this.outputOptionsLoading()
    && (this.sourceKind() === 'Literal' || (!this.outputOptionsLoading() && !this.outputOptionsError()))
    && !this.hasUnavailableReference()
    && (this.sourceKind() !== 'KeyVaultSecret' || (this.keyVaultResourceId().length > 0
      && SECRET_NAME_PATTERN.test(this.secretName().trim())
      && (this.secretFeedKind() !== 'PipelineSecret' || PIPELINE_VARIABLE_NAME_PATTERN.test(this.pipelineVariableName().trim()))))
    && (this.sourceKind() !== 'ResourceOutput' || (this.sourceResourceId().length > 0 && this.outputName().length > 0)));

  private readonly resources = inject(ResourcesService);
  private readonly componentsService = inject(ComponentsService);
  private readonly projects = inject(ProjectsService);
  private readonly catalog = inject(CatalogService);
  private readonly transloco = inject(TranslocoService);
  private secretNameCustomized = false;
  private pipelineVariableNameCustomized = false;
  private outputOptionsRequest: Promise<void> | undefined;
  private createRequestId = 0;

  ngOnInit(): void {
    const defaultVault = this.keyVaults()[0]
      ?? this.component().resources.find((resource) => resource.type === 'KeyVault');
    if (defaultVault) {
      this.keyVaultResourceId.set(defaultVault.id);
      this.pipelineVariableName.set(this.defaultPipelineName(defaultVault.logicalName, ''));
    } else {
      this.sourceKind.set('Literal');
    }
    const identityId = this.resource().defaultIdentityResourceId;
    if (identityId) this.identityResourceId.set(identityId);
    void this.load();
    void this.loadOutputs();
  }

  protected async startCreate(): Promise<void> {
    if (this.saving() || this.outputOptionsLoading() || this.formOpen()) return;
    const requestId = ++this.createRequestId;
    await this.loadOutputs();
    if (this.saving() || requestId !== this.createRequestId) return;
    this.editing.set(null);
    this.resetForm();
    this.formOpen.set(true);
    this.actionError.set(false);
    this.settingConflict.set(false);
  }

  protected async retryOutputOptions(): Promise<void> {
    if (this.saving() || this.outputOptionsLoading()) return;
    await this.loadOutputs();
  }

  protected startEdit(setting: AppSettingResponse): void {
    if (this.saving() || this.outputOptionsLoading() || this.formOpen()) return;
    this.createRequestId++;
    this.editing.set(setting);
    this.name.set(setting.name);
    this.sourceKind.set(setting.sourceKind);
    this.literalValue.set(setting.literalValue ?? '');
    this.overrides.set(setting.overrides);
    this.forceNonSecret.set(setting.secretValueDeclaredNonSecret);
    this.secretDeclarationRequired.set(setting.secretValueDeclaredNonSecret);
    this.sourceResourceId.set(setting.sourceResourceId ?? '');
    this.outputName.set(setting.outputName ?? '');
    this.keyVaultResourceId.set(setting.keyVaultResourceId ?? '');
    this.secretName.set(setting.secretName ?? '');
    this.pipelineVariableName.set(setting.pipelineVariableName ?? '');
    this.secretNameCustomized = Boolean(setting.secretName);
    this.pipelineVariableNameCustomized = Boolean(setting.pipelineVariableName);
    this.identityResourceId.set(setting.identityResourceId ?? '');
    this.secretFeedKind.set(setting.secretFeedKind === 'ManagedOutsideIfs' ? 'ManagedOutsideIfs' : 'PipelineSecret');
    this.formOpen.set(true);
    this.actionError.set(false);
    this.settingConflict.set(false);
    this.secretSuspectError.set(false);
  }

  protected cancelForm(): void {
    if (this.saving()) return;
    this.createRequestId++;
    this.formOpen.set(false);
    this.editing.set(null);
    this.actionError.set(false);
    this.settingConflict.set(false);
    this.secretSuspectError.set(false);
  }

  protected updateName(value: string): void {
    this.name.set(value);
    if (this.sourceKind() === 'KeyVaultSecret') {
      const vault = this.keyVaults().find((item) => item.id === this.keyVaultResourceId());
      if (vault && !this.pipelineVariableNameCustomized)
        this.pipelineVariableName.set(this.defaultPipelineName(vault.logicalName, value));
      if (!this.secretNameCustomized) this.secretName.set(this.defaultSecretName(value));
    }
  }

  protected updateLiteralValue(value: string): void {
    if (value !== this.literalValue()) {
      this.forceNonSecret.set(false);
      this.secretDeclarationRequired.set(false);
      this.secretSuspectError.set(false);
      this.actionError.set(false);
    }
    this.literalValue.set(value);
  }

  protected updateSecretName(value: string): void {
    this.secretName.set(value);
    this.secretNameCustomized = true;
  }

  protected updatePipelineVariableName(value: string): void {
    this.pipelineVariableName.set(value);
    this.pipelineVariableNameCustomized = true;
  }

  protected updateSecretFeedKind(value: 'PipelineSecret' | 'ManagedOutsideIfs'): void {
    this.secretFeedKind.set(value);
    if (value === 'PipelineSecret' && !this.pipelineVariableNameCustomized) {
      const vault = this.keyVaults().find((item) => item.id === this.keyVaultResourceId()) ?? this.keyVaults()[0];
      if (vault) {
        if (!this.keyVaultResourceId()) this.keyVaultResourceId.set(vault.id);
        this.pipelineVariableName.set(this.defaultPipelineName(vault.logicalName, this.name()));
      }
    }
  }

  protected updateSourceKind(value: AppSettingSourceKind): void {
    this.sourceKind.set(value);
    if (value === 'KeyVaultSecret') {
      const vault = this.keyVaults().find((item) => item.id === this.keyVaultResourceId()) ?? this.keyVaults()[0];
      if (vault && !this.keyVaultResourceId()) this.keyVaultResourceId.set(vault.id);
      if (vault && !this.pipelineVariableNameCustomized)
        this.pipelineVariableName.set(this.defaultPipelineName(vault.logicalName, this.name()));
      if (!this.secretNameCustomized) this.secretName.set(this.defaultSecretName(this.name()));
    }
    this.actionError.set(false);
    this.secretSuspectError.set(false);
  }

  protected convertSuspectToPipelineSecret(): void {
    if (!this.secretSuspectError() || this.keyVaults().length === 0) return;
    this.literalValue.set('');
    this.forceNonSecret.set(false);
    this.secretDeclarationRequired.set(false);
    this.updateSourceKind('KeyVaultSecret');
    this.secretFeedKind.set('PipelineSecret');
  }

  protected updateKeyVault(value: string): void {
    this.keyVaultResourceId.set(value);
    const vault = this.keyVaults().find((item) => item.id === value);
    if (vault && !this.pipelineVariableNameCustomized)
      this.pipelineVariableName.set(this.defaultPipelineName(vault.logicalName, this.name()));
  }

  protected async save(): Promise<void> {
    if (!this.canSubmit() || !this.canEdit()) return;
    this.saving.set(true);
    this.actionError.set(false);
    this.secretSuspectError.set(false);
    const body = this.createRequest();
    try {
      if (body.sourceKind !== 'Literal') {
        await this.loadOutputs();
        if (this.outputOptionsError() || this.hasUnavailableReference()) return;
      }
      const editing = this.editing();
      if (editing) {
        const update: AppSettingUpdateRequest = { ...body, expectedVersion: editing.version };
        await firstValueFrom(this.resources.updateAppSetting(
          this.projectId(), this.component().id, this.resource().id, editing.id, update,
        ));
      } else {
        await firstValueFrom(this.resources.createAppSetting(
          this.projectId(), this.component().id, this.resource().id, body,
        ));
      }
      this.formOpen.set(false);
      this.editing.set(null);
      await this.load();
    } catch (error) {
      if (error instanceof ApiError && error.status === 409 && error.code === 'CONFLICT_VERSION' && this.editing()) {
        this.settingConflict.set(true);
        await this.load();
      } else if (error instanceof ApiError && error.code === 'VAL-PAR-SECRET-SUSPECT'
        && this.sourceKind() === 'Literal') {
        this.secretDeclarationRequired.set(true);
        this.secretSuspectError.set(true);
      } else {
        this.actionError.set(true);
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected async delete(setting: AppSettingResponse): Promise<void> {
    if (!this.canEdit() || this.saving() || this.formOpen()) return;
    this.saving.set(true);
    this.actionError.set(false);
    try {
      await firstValueFrom(this.resources.deleteAppSetting(
        this.projectId(), this.component().id, this.resource().id, setting.id,
        { expectedVersion: setting.version },
      ));
      await this.load();
    } catch {
      this.actionError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  protected sourceLabel(kind: AppSettingSourceKind): string {
    return this.transloco.translate(this.sourceKindTranslationKeys[kind]);
  }

  protected pipelineVariableLabel(setting: AppSettingResponse): string | null {
    return setting.secretFeedKind === 'PipelineSecret' ? setting.pipelineVariableName : null;
  }

  private createRequest(): AppSettingRequest {
    const sourceKind = this.sourceKind();
    return {
      name: this.name().trim(),
      sourceKind,
      forceNonSecret: sourceKind === 'Literal' && this.forceNonSecret(),
      literalValue: sourceKind === 'Literal' ? this.literalValue() : null,
      sourceResourceId: sourceKind === 'ResourceOutput' ? this.sourceResourceId() || null : null,
      outputName: sourceKind === 'ResourceOutput' ? this.outputName() || null : null,
      keyVaultResourceId: sourceKind === 'KeyVaultSecret' ? this.keyVaultResourceId() || null : null,
      secretName: sourceKind === 'KeyVaultSecret' ? this.secretName().trim() || null : null,
      secretFeedKind: sourceKind === 'KeyVaultSecret' ? this.secretFeedKind() : null,
      pipelineVariableName: sourceKind === 'KeyVaultSecret' && this.secretFeedKind() === 'PipelineSecret'
        ? this.pipelineVariableName().trim() || null
        : null,
      identityResourceId: sourceKind === 'KeyVaultSecret' ? this.identityResourceId() || null : null,
      overrides: sourceKind === 'Literal' ? this.overrides() : null,
    };
  }

  private resetForm(): void {
    this.secretNameCustomized = false;
    this.pipelineVariableNameCustomized = false;
    this.name.set('');
    this.literalValue.set('');
    this.overrides.set(null);
    this.forceNonSecret.set(false);
    this.secretDeclarationRequired.set(false);
    this.secretSuspectError.set(false);
    this.sourceResourceId.set('');
    this.outputName.set('');
    this.secretName.set('');
    this.sourceKind.set(this.keyVaults().length ? 'KeyVaultSecret' : 'Literal');
    this.secretFeedKind.set('PipelineSecret');
    this.identityResourceId.set(this.resource().defaultIdentityResourceId ?? '');
    const vault = this.keyVaults()[0];
    this.keyVaultResourceId.set(vault?.id ?? '');
    this.pipelineVariableName.set(vault ? this.defaultPipelineName(vault.logicalName, '') : '');
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      this.settings.set(await firstValueFrom(this.resources.appSettings(
        this.projectId(), this.component().id, this.resource().id,
      )));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadOutputs(): Promise<void> {
    if (this.outputOptionsRequest) return this.outputOptionsRequest;
    this.outputOptionsLoading.set(true);
    this.outputOptionsError.set(false);
    const request = this.fetchOutputOptions().catch(() => {
      this.outputOptionsError.set(true);
    }).finally(() => {
      this.outputOptionsLoading.set(false);
      this.outputOptionsRequest = undefined;
    });
    this.outputOptionsRequest = request;
    return request;
  }

  private async fetchOutputOptions(): Promise<void> {
    const project = await firstValueFrom(this.projects.get(this.projectId()));
    const components = await firstValueFrom(this.componentsService.list(this.projectId()));
    const resources = components.flatMap((component) => component.resources.map((resource) => ({
      componentId: component.id,
      componentCode: component.code,
      resource,
    })));
    this.projectResources.set(resources.map(({ componentId, componentCode, resource }) => ({
      id: resource.id,
      componentId,
      componentCode,
      logicalName: resource.logicalName,
      type: resource.type,
    })));
    this.outputChoices.set([]);
    const types = [...new Set(resources.map(({ resource }) => resource.type))];
    const descriptors = await Promise.all(types.map((type) => firstValueFrom(this.catalog.type(project.catalogVersion, type))));
    const byType = new Map(types.map((type, index) => [type, descriptors[index]]));
    const choices = resources.flatMap(({ componentCode, resource }) => {
      const descriptor = byType.get(resource.type);
      if (!descriptor || typeof descriptor !== 'object' || Array.isArray(descriptor)) return [];
      const outputs = descriptor['outputs'];
      if (!Array.isArray(outputs)) return [];
      return outputs.flatMap((output): ResourceOutputChoice[] => {
        if (typeof output !== 'object' || output === null || Array.isArray(output)) return [];
        const outputName = output['name'];
        return typeof outputName === 'string' && output['sensitive'] !== true
          ? [{
            resourceId: resource.id,
            componentCode,
            resourceName: resource.logicalName,
            resourceType: resource.type,
            outputName,
          }]
          : [];
      });
    });
    this.outputChoices.set(choices);
  }

  private defaultSecretName(name: string): string {
    return name.trim().replace(/([a-z0-9])([A-Z])/g, '$1-$2').replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-|-$/g, '').toLowerCase().slice(0, 127);
  }

  private defaultPipelineName(vaultName: string, settingName: string): string {
    return `${vaultName}_${settingName}`
      .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
      .replace(/[^a-zA-Z0-9_]+/g, '_')
      .replace(/_+/g, '_')
      .replace(/^_|_$/g, '')
      .toUpperCase()
      .slice(0, 128);
  }
}
