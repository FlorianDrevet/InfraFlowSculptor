import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  AppSettingRequest,
  AppSettingResponse,
  AppSettingUpdateRequest,
  ComponentResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ResourceAppSettingsPanel } from './resource-app-settings-panel';

class ResourceAppSettingsTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        applicationSettingsHeading: 'Paramètres de l’application',
        applicationSettingsHelp: 'Configurez les paramètres.',
        addAppSetting: 'Ajouter un paramètre',
        editAppSetting: 'Modifier',
        deleteAppSetting: 'Supprimer',
        loadingAppSettings: 'Chargement.',
        noAppSettings: 'Aucun paramètre.',
        cancel: 'Annuler',
        save: 'Enregistrer',
        saving: 'Enregistrement…',
        settingName: 'Nom',
        settingNameFormat: 'Format du nom du paramètre.',
        savingAppSetting: 'Enregistrement…',
        settingSourceLabel: 'Source',
        settingLiteralValue: 'Valeur',
        settingForceNonSecret: 'Je confirme que la valeur littérale et ses surcharges ne sont pas des secrets.',
        settingForceNonSecretHelp: 'Cette déclaration est journalisée.',
        settingSecretSuspect: 'Cette valeur ressemble à un secret. Confirmez si elle n’en est pas un.',
        settingSource: { Literal: 'Valeur', ResourceOutput: 'Sortie', KeyVaultSecret: 'Secret Key Vault' },
        settingKeyVault: 'Coffre',
        settingSecretName: 'Nom du secret',
        secretNameFormat: 'Format du nom du secret.',
        secretFeedKind: 'Alimentation du secret',
        pipelineSecret: 'Secret de pipeline',
        managedOutsideIfs: 'Géré hors d’IFS',
        pipelineVariableName: 'Variable de pipeline',
        pipelineVariableNameFormat: 'Format du nom de variable.',
        pipelineSecretValueNeverShown: 'La valeur n’est jamais affichée.',
        settingIdentity: 'Identité',
        defaultIdentity: 'Identité par défaut',
      },
    });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceId = '99999999-9999-4999-8999-999999999999';
const vaultId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const secondVaultId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';

describe('ResourceAppSettingsPanel', () => {
  it('sets defaults when switching to Key Vault and preserves custom names across name and vault changes', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      type: 'ContainerApp',
      logicalName: 'api',
      description: '',
      tags: {},
      version: 1,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      defaultIdentityResourceId: null,
      children: [],
    };
    const vault: ResourceResponse = { ...resource, id: vaultId, type: 'KeyVault', logicalName: 'vault-main' };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'API',
      code: 'api',
      resources: [resource, vault],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const secondVault: ResourceResponse = { ...resource, id: secondVaultId, type: 'KeyVault', logicalName: 'vault-backup' };
    const componentWithTwoVaults = { ...component, resources: [resource, vault, secondVault] };
    const literalSetting: AppSettingResponse = {
      id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
      identityResourceId: null,
      isImplicit: false,
      keyVaultResourceId: null,
      literalValue: 'password=example',
      name: 'PORT',
      origin: null,
      outputName: null,
      overrides: { '11111111-1111-4111-8111-111111111111': 'region=west' },
      pipelineVariableName: null,
      secretFeedKind: null,
      secretName: null,
      secretValueDeclaredNonSecret: true,
      sourceKind: 'Literal',
      sourceResourceId: null,
      version: 1,
      warnings: [],
    };
    const outsideSetting: AppSettingResponse = {
      ...literalSetting,
      id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
      name: 'OUTSIDE_SECRET',
      keyVaultResourceId: vaultId,
      literalValue: null,
      secretFeedKind: 'ManagedOutsideIfs',
      secretName: 'outside-secret',
      sourceKind: 'KeyVaultSecret',
    };
    const appSettings = vi.fn(() => of([literalSetting, outsideSetting]));
    const createAppSetting = vi.fn((...args: [string, string, string, AppSettingRequest]) => {
      const request = args[3];
      return request.sourceKind === 'Literal' && !request.forceNonSecret
        ? throwError(() => new ApiError(400, 'VAL-PAR-SECRET-SUSPECT', 'Secret-like value.'))
        : of(request);
    });
    const updateAppSetting = vi.fn((...args: [string, string, string, string, AppSettingUpdateRequest]) => of(args[4]));

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings, createAppSetting, updateAppSetting } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: vi.fn(() => of({ outputs: [] })) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', componentWithTwoVaults);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(appSettings).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['loading']()).toBe(false));

    const host = fixture.nativeElement as HTMLElement;
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Ajouter un paramètre'))
      ?.click();
    fixture.detectChanges();

    const input = (name: string): HTMLInputElement =>
      host.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
    const select = async (name: 'sourceKind' | 'keyVaultResourceId', value: string): Promise<void> => {
      if (name === 'sourceKind')
        fixture.componentInstance['updateSourceKind'](value as 'Literal' | 'KeyVaultSecret');
      else fixture.componentInstance['updateKeyVault'](value);
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const enter = (name: string, value: string): void => {
      input(name).value = value;
      input(name).dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
    };

    await select('sourceKind', 'Literal');
    enter('settingName', 'PUBLIC_TOKEN');
    enter('literalValue', 'password=example');
    expect(host.querySelector<HTMLInputElement>('input[name="forceNonSecret"]')).toBeNull();
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await vi.waitFor(() => expect(createAppSetting).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(host.querySelector<HTMLInputElement>('input[name="forceNonSecret"]')).not.toBeNull());
    const createNonSecretCheckbox = host.querySelector<HTMLInputElement>('input[name="forceNonSecret"]')!;
    expect(createNonSecretCheckbox.checked).toBe(false);
    expect(host.textContent).toContain('Cette valeur ressemble à un secret.');
    createNonSecretCheckbox.click();
    fixture.detectChanges();
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await vi.waitFor(() => expect(createAppSetting).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(fixture.componentInstance['saving']()).toBe(false));
    expect(createAppSetting.mock.calls[0][3].forceNonSecret).toBe(false);
    expect(createAppSetting.mock.calls[1][3].forceNonSecret).toBe(true);

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Ajouter un paramètre'))
      ?.click();
    fixture.detectChanges();

    await select('sourceKind', 'Literal');
    enter('settingName', 'API_TOKEN');
    await select('sourceKind', 'KeyVaultSecret');

    expect(input('secretName').value).toBe('api-token');
    expect(input('pipelineVariableName').value).toBe('VAULT_MAIN_API_TOKEN');

    enter('settingName', 'A'.repeat(256));
    expect(input('secretName').value).toHaveLength(127);
    expect(input('pipelineVariableName').value).toHaveLength(128);
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(false);
    enter('settingName', 'API_TOKEN');
    enter('settingName', 'API TOKEN');
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    enter('settingName', '1PORT');
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    enter('settingName', 'API_TOKEN');

    await select('keyVaultResourceId', secondVaultId);
    expect(input('pipelineVariableName').value).toBe('VAULT_BACKUP_API_TOKEN');

    expect(input('secretName').value).toBe('api-token');

    enter('secretName', 'custom-secret-name');
    enter('pipelineVariableName', 'CUSTOM_API_TOKEN');
    enter('settingName', 'API_TOKEN_V2');
    await select('keyVaultResourceId', vaultId);

    expect(input('secretName').value).toBe('custom-secret-name');
    expect(input('pipelineVariableName').value).toBe('CUSTOM_API_TOKEN');

    enter('secretName', 'INVALID_NAME');
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    enter('secretName', 'S'.repeat(128));
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    enter('secretName', 'custom-secret-name');

    enter('pipelineVariableName', '');
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    host.querySelector('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(createAppSetting).toHaveBeenCalledTimes(2);

    enter('pipelineVariableName', 'INVALID-NAME');
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
    enter('pipelineVariableName', 'A'.repeat(129));
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);

    enter('pipelineVariableName', 'CUSTOM_API_TOKEN');
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await vi.waitFor(() => expect(createAppSetting).toHaveBeenCalledTimes(3));
    await vi.waitFor(() => expect(fixture.componentInstance['saving']()).toBe(false));
    const request = createAppSetting.mock.calls[2][3];
    expect(request.name).toBe('API_TOKEN_V2');
    expect(request.sourceKind).toBe('KeyVaultSecret');
    expect(request.keyVaultResourceId).toBe(vaultId);
    expect(request.secretName).toBe('custom-secret-name');
    expect(request.secretFeedKind).toBe('PipelineSecret');
    expect(request.pipelineVariableName).toBe('CUSTOM_API_TOKEN');
    expect(request.identityResourceId).toBeNull();

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Modifier'))
      ?.click();
    fixture.detectChanges();
    expect(fixture.componentInstance['formOpen']()).toBe(true);
    expect(fixture.componentInstance['name']()).toBe('PORT');
    expect(fixture.componentInstance['sourceKind']()).toBe('Literal');
    expect(fixture.componentInstance['saving']()).toBe(false);
    expect(fixture.componentInstance['canSubmit']()).toBe(true);
    expect(host.querySelector<HTMLInputElement>('input[name="forceNonSecret"]')?.checked).toBe(true);
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await vi.waitFor(() => expect(updateAppSetting).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['formOpen']()).toBe(false));
    await vi.waitFor(() => expect(fixture.componentInstance['saving']()).toBe(false));
    const updateRequest = updateAppSetting.mock.calls[0][4];
    expect(updateRequest.sourceKind).toBe('Literal');
    expect(updateRequest.overrides).toEqual(literalSetting.overrides);
    expect(updateRequest.forceNonSecret).toBe(true);

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Modifier'))
      ?.click();
    fixture.detectChanges();
    enter('literalValue', '8080');
    expect(host.querySelector<HTMLInputElement>('input[name="forceNonSecret"]')).toBeNull();
    fixture.detectChanges();
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await vi.waitFor(() => expect(updateAppSetting).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(fixture.componentInstance['formOpen']()).toBe(false));
    await vi.waitFor(() => expect(fixture.componentInstance['saving']()).toBe(false));
    const sanitizedUpdateRequest = updateAppSetting.mock.calls[1][4];
    expect(sanitizedUpdateRequest.literalValue).toBe('8080');
    expect(sanitizedUpdateRequest.overrides).toEqual(literalSetting.overrides);
    expect(sanitizedUpdateRequest.forceNonSecret).toBe(false);

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Modifier'))
      ?.click();
    fixture.detectChanges();
    await select('sourceKind', 'KeyVaultSecret');
    expect(fixture.componentInstance['keyVaultResourceId']()).toBe(vaultId);
    expect(input('secretName').value).toBe('port');
    expect(input('pipelineVariableName').value).toBe('VAULT_MAIN_PORT');

    await select('keyVaultResourceId', secondVaultId);
    enter('settingName', 'API_PORT');
    expect(input('secretName').value).toBe('api-port');
    expect(input('pipelineVariableName').value).toBe('VAULT_BACKUP_API_PORT');

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Annuler'))
      ?.click();
    fixture.detectChanges();
    const outsideRow = [...host.querySelectorAll('li')]
      .find((row) => row.textContent?.includes('OUTSIDE_SECRET'))!;
    [...outsideRow.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Modifier'))
      ?.click();
    fixture.detectChanges();
    fixture.componentInstance['updateSecretFeedKind']('PipelineSecret');
    fixture.detectChanges();
    expect(input('pipelineVariableName').value).toBe('VAULT_MAIN_OUTSIDE_SECRET');
  });
});
