import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
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
import { CatalogService } from '../../core/catalog/catalog.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';

interface ResourceOutputChoice {
  resourceId: string;
  resourceName: string;
  outputName: string;
}

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
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly actionError = signal(false);
  protected readonly formOpen = signal(false);
  protected readonly editing = signal<AppSettingResponse | null>(null);
  protected readonly name = signal('');
  protected readonly sourceKind = signal<AppSettingSourceKind>('KeyVaultSecret');
  protected readonly literalValue = signal('');
  protected readonly sourceResourceId = signal('');
  protected readonly outputName = signal('');
  protected readonly keyVaultResourceId = signal('');
  protected readonly secretName = signal('');
  protected readonly pipelineVariableName = signal('');
  protected readonly identityResourceId = signal('');
  protected readonly secretFeedKind = signal<'PipelineSecret' | 'ManagedOutsideIfs'>('PipelineSecret');

  protected readonly keyVaults = computed(() => this.component().resources.filter((resource) => resource.type === 'KeyVault'));
  protected readonly identities = computed(() => this.component().resources.filter((resource) => resource.type === 'UserAssignedIdentity'));
  protected readonly outputResources = computed(() => this.component().resources.filter((resource) =>
    this.outputChoices().some((choice) => choice.resourceId === resource.id),
  ));
  protected readonly selectedOutputs = computed(() => this.outputChoices().filter((choice) => choice.resourceId === this.sourceResourceId()));
  protected readonly canSubmit = computed(() => this.name().trim().length > 0 && !this.saving()
    && (this.sourceKind() !== 'KeyVaultSecret' || (this.keyVaultResourceId().length > 0 && this.secretName().trim().length > 0))
    && (this.sourceKind() !== 'ResourceOutput' || (this.sourceResourceId().length > 0 && this.outputName().length > 0)));

  private readonly resources = inject(ResourcesService);
  private readonly projects = inject(ProjectsService);
  private readonly catalog = inject(CatalogService);
  private readonly transloco = inject(TranslocoService);

  ngOnInit(): void {
    const defaultVault = this.keyVaults()[0];
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

  protected startCreate(): void {
    this.editing.set(null);
    this.resetForm();
    this.formOpen.set(true);
    this.actionError.set(false);
  }

  protected startEdit(setting: AppSettingResponse): void {
    this.editing.set(setting);
    this.name.set(setting.name);
    this.sourceKind.set(setting.sourceKind);
    this.literalValue.set(setting.literalValue ?? '');
    this.sourceResourceId.set(setting.sourceResourceId ?? '');
    this.outputName.set(setting.outputName ?? '');
    this.keyVaultResourceId.set(setting.keyVaultResourceId ?? '');
    this.secretName.set(setting.secretName ?? '');
    this.pipelineVariableName.set(setting.pipelineVariableName ?? '');
    this.identityResourceId.set(setting.identityResourceId ?? '');
    this.secretFeedKind.set(setting.secretFeedKind === 'ManagedOutsideIfs' ? 'ManagedOutsideIfs' : 'PipelineSecret');
    this.formOpen.set(true);
    this.actionError.set(false);
  }

  protected cancelForm(): void {
    if (this.saving()) return;
    this.formOpen.set(false);
    this.editing.set(null);
    this.actionError.set(false);
  }

  protected updateName(value: string): void {
    this.name.set(value);
    if (!this.editing() && this.sourceKind() === 'KeyVaultSecret') {
      const vault = this.keyVaults().find((item) => item.id === this.keyVaultResourceId());
      if (vault) this.pipelineVariableName.set(this.defaultPipelineName(vault.logicalName, value));
      this.secretName.set(this.defaultSecretName(value));
    }
  }

  protected updateSourceKind(value: AppSettingSourceKind): void {
    this.sourceKind.set(value);
    this.actionError.set(false);
  }

  protected updateKeyVault(value: string): void {
    this.keyVaultResourceId.set(value);
    const vault = this.keyVaults().find((item) => item.id === value);
    if (vault && !this.editing()) this.pipelineVariableName.set(this.defaultPipelineName(vault.logicalName, this.name()));
  }

  protected async save(): Promise<void> {
    if (!this.canSubmit() || !this.canEdit()) return;
    this.saving.set(true);
    this.actionError.set(false);
    const body = this.createRequest();
    try {
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
    } catch {
      this.actionError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  protected async delete(setting: AppSettingResponse): Promise<void> {
    if (!this.canEdit() || this.saving()) return;
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
      forceNonSecret: false,
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
      overrides: null,
    };
  }

  private resetForm(): void {
    this.name.set('');
    this.literalValue.set('');
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
    try {
      const project = await firstValueFrom(this.projects.get(this.projectId()));
      const types = [...new Set(this.component().resources.map((resource) => resource.type))];
      const descriptors = await Promise.all(types.map((type) => firstValueFrom(this.catalog.type(project.catalogVersion, type))));
      const byType = new Map(types.map((type, index) => [type, descriptors[index]]));
      const choices = this.component().resources.flatMap((resource) => {
        const descriptor = byType.get(resource.type);
        if (!descriptor || typeof descriptor !== 'object' || Array.isArray(descriptor)) return [];
        const outputs = descriptor['outputs'];
        if (!Array.isArray(outputs)) return [];
        return outputs.flatMap((output): ResourceOutputChoice[] => {
          if (typeof output !== 'object' || output === null || Array.isArray(output)) return [];
          const outputName = output['name'];
          return typeof outputName === 'string' && output['sensitive'] !== true
            ? [{ resourceId: resource.id, resourceName: resource.logicalName, outputName }]
            : [];
        });
      });
      this.outputChoices.set(choices);
    } catch {
      this.outputChoices.set([]);
    }
  }

  private defaultSecretName(name: string): string {
    return name.trim().replace(/([a-z0-9])([A-Z])/g, '$1-$2').replace(/[^a-zA-Z0-9]+/g, '-').replace(/^-|-$/g, '').toLowerCase();
  }

  private defaultPipelineName(vaultName: string, settingName: string): string {
    return `${vaultName}_${settingName}`
      .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
      .replace(/[^a-zA-Z0-9_]+/g, '_')
      .replace(/_+/g, '_')
      .replace(/^_|_$/g, '')
      .toUpperCase();
  }
}
