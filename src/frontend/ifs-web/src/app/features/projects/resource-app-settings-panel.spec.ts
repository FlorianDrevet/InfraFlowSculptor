import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
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
import { ComponentsService } from '../../core/projects/components.service';
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
        appSettingResourcesLoadError: 'Le chargement des options de référence a échoué.',
        appSettingReferenceUnavailable: 'Une ressource sélectionnée n’est plus disponible.',
        appSettingConflict: 'Ce paramètre a changé dans un autre onglet. Vos saisies sont conservées. Annulez puis rouvrez la modification pour charger la version à jour.',
        retry: 'Réessayer',
        noAppSettings: 'Aucun paramètre.',
        cancel: 'Annuler',
        save: 'Enregistrer',
        saving: 'Enregistrement…',
        settingName: 'Nom',
        settingNameFormat: 'Format du nom du paramètre.',
        savingAppSetting: 'Enregistrement…',
        settingSourceLabel: 'Source',
        settingSourceResource: 'Ressource source',
        settingOutput: 'Sortie',
        selectResource: 'Choisir une ressource',
        selectOutput: 'Choisir une sortie',
        settingLiteralValue: 'Valeur',
        settingForceNonSecret: 'Je confirme que la valeur littérale et ses surcharges ne sont pas des secrets.',
        settingForceNonSecretHelp: 'Cette déclaration est journalisée.',
        settingSecretSuspect: 'Cette valeur ressemble à un secret. Confirmez si elle n’en est pas un.',
        convertSuspectToPipelineSecret: 'Convertir en secret de pipeline',
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
  it('refreshes available vaults before opening a new setting form', async () => {
    const appResource: ResourceResponse = {
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
    const keyVault: ResourceResponse = {
      ...appResource,
      id: secondVaultId,
      type: 'KeyVault',
      logicalName: 'main',
    };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [appResource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    let projectComponents: ComponentResponse[] = [component];
    const listComponents = vi.fn(() => of(projectComponents));

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings: vi.fn(() => of([])) } },
        { provide: ComponentsService, useValue: { list: listComponents } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: vi.fn(() => of({ outputs: [] })) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', appResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(listComponents).toHaveBeenCalledOnce());
    expect(fixture.componentInstance['keyVaults']()).toHaveLength(0);
    await vi.waitFor(() => expect(fixture.componentInstance['outputOptionsLoading']()).toBe(false));

    await fixture.componentInstance['startCreate']();
    fixture.componentInstance['updateName']('PORT');
    fixture.detectChanges();
    const addButton = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Ajouter un paramètre'));
    expect(addButton?.disabled).toBe(true);
    await fixture.componentInstance['startCreate']();
    expect(fixture.componentInstance['name']()).toBe('PORT');
    fixture.componentInstance['updateSourceKind']('KeyVaultSecret');
    expect(fixture.componentInstance['hasUnavailableReference']()).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Une ressource sélectionnée n’est plus disponible.');
    fixture.componentInstance['cancelForm']();

    projectComponents = [{ ...component, resources: [appResource, keyVault] }];
    await fixture.componentInstance['startCreate']();
    fixture.detectChanges();

    await vi.waitFor(() => expect(listComponents).toHaveBeenCalledTimes(3));
    expect(fixture.componentInstance['keyVaults']()).toHaveLength(1);
    expect(fixture.componentInstance['keyVaults']()[0]?.logicalName).toBe('main');
  });

  it('keeps referenced resources visible and blocks secret settings until resource options reload', async () => {
    const appResource: ResourceResponse = {
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
    const vault: ResourceResponse = { ...appResource, id: vaultId, type: 'KeyVault', logicalName: 'main' };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [appResource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const coreComponent = {
      ...component,
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      name: 'Core',
      code: 'core',
      resources: [vault],
    };
    let projectComponents: ComponentResponse[] = [component, coreComponent];
    const listComponents = vi.fn(() => of(projectComponents));
    const setting: AppSettingResponse = {
      id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
      identityResourceId: null,
      isImplicit: false,
      keyVaultResourceId: vaultId,
      literalValue: null,
      name: 'Payments__ApiKey',
      origin: null,
      outputName: null,
      overrides: null,
      pipelineVariableName: 'MAIN_PAYMENTS_API_KEY',
      secretFeedKind: 'PipelineSecret',
      secretName: 'payments-api-key',
      secretValueDeclaredNonSecret: false,
      sourceKind: 'KeyVaultSecret',
      sourceResourceId: null,
      version: 1,
      warnings: [],
    };
    const updateAppSetting = vi.fn(() => of(undefined));
    const catalogType = vi
      .fn()
      .mockReturnValueOnce(throwError(() => new Error('catalogue unavailable')))
      .mockReturnValue(of({ outputs: [] }));

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings: vi.fn(() => of([setting])), updateAppSetting } },
        { provide: ComponentsService, useValue: { list: listComponents } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: catalogType } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', appResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    const componentState = fixture.componentInstance as unknown as {
      canSubmit: () => boolean;
      keyVaultResourceId: () => string;
      keyVaults: () => ResourceResponse[];
      loading: () => boolean;
      outputOptionsError: () => boolean;
      retryOutputOptions: () => Promise<void>;
      save: () => Promise<void>;
    };
    await vi.waitFor(() => expect(componentState.loading()).toBe(false));
    await vi.waitFor(() => expect(componentState.outputOptionsError()).toBe(true));

    const host = fixture.nativeElement as HTMLElement;
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Modifier'))
      ?.click();
    fixture.detectChanges();

    expect(host.textContent).toContain('Le chargement des options de référence a échoué.');
    expect(componentState.keyVaults().map((resource) => resource.logicalName)).toContain('main');
    expect(componentState.keyVaultResourceId()).toBe(vaultId);
    expect(componentState.canSubmit()).toBe(false);
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);

    await componentState.retryOutputOptions();
    fixture.detectChanges();
    expect(componentState.outputOptionsError()).toBe(false);
    expect(componentState.canSubmit()).toBe(true);
    expect(host.querySelector('select[name="keyVaultResourceId"] option[value="' + vaultId + '"]')).not.toBeNull();

    projectComponents = [component];
    await componentState.save();
    fixture.detectChanges();
    expect(updateAppSetting).not.toHaveBeenCalled();
    expect(componentState.keyVaults()).toHaveLength(0);
    expect(componentState.keyVaultResourceId()).toBe(vaultId);
    expect(componentState.canSubmit()).toBe(false);
    expect(host.textContent).toContain('Une ressource sélectionnée n’est plus disponible.');
  });

  it('disables add and edit actions while resource options are loading', async () => {
    const appResource: ResourceResponse = {
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
    const vault: ResourceResponse = { ...appResource, id: vaultId, type: 'KeyVault', logicalName: 'main' };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [appResource, vault],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const setting: AppSettingResponse = {
      id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
      identityResourceId: null,
      isImplicit: false,
      keyVaultResourceId: null,
      literalValue: '8080',
      name: 'PORT',
      origin: null,
      outputName: null,
      overrides: null,
      pipelineVariableName: null,
      secretFeedKind: null,
      secretName: null,
      secretValueDeclaredNonSecret: false,
      sourceKind: 'Literal',
      sourceResourceId: null,
      version: 1,
      warnings: [],
    };
    const pendingDescriptor = new Subject<{ outputs: unknown[] }>();

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings: vi.fn(() => of([setting])) } },
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([component])) } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: vi.fn(() => pendingDescriptor.asObservable()) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', appResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    const componentState = fixture.componentInstance as unknown as {
      loading: () => boolean;
      outputOptionsLoading: () => boolean;
    };
    await vi.waitFor(() => expect(componentState.loading()).toBe(false));
    await vi.waitFor(() => expect(componentState.outputOptionsLoading()).toBe(true));
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const addButton = [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Ajouter un paramètre'))!;
    const editButton = [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'Modifier')!;
    expect(addButton.disabled).toBe(true);
    expect(editButton.disabled).toBe(true);

    pendingDescriptor.next({ outputs: [] });
    pendingDescriptor.complete();
    await vi.waitFor(() => expect(componentState.outputOptionsLoading()).toBe(false));
    fixture.detectChanges();
    expect(addButton.disabled).toBe(false);
    expect(editButton.disabled).toBe(false);
  });

  it('keeps an edit open when a second create request is made', async () => {
    const appResource: ResourceResponse = {
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
    const vault: ResourceResponse = { ...appResource, id: vaultId, type: 'KeyVault', logicalName: 'main' };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [appResource, vault],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const setting: AppSettingResponse = {
      id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
      identityResourceId: null,
      isImplicit: false,
      keyVaultResourceId: null,
      literalValue: '8080',
      name: 'PORT',
      origin: null,
      outputName: null,
      overrides: null,
      pipelineVariableName: null,
      secretFeedKind: null,
      secretName: null,
      secretValueDeclaredNonSecret: false,
      sourceKind: 'Literal',
      sourceResourceId: null,
      version: 1,
      warnings: [],
    };
    const catalogType = vi.fn(() => of({ outputs: [] }));
    const createAppSetting = vi.fn(() => of(undefined));
    const updateAppSetting = vi.fn(() => of(undefined));

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings: vi.fn(() => of([setting])), createAppSetting, updateAppSetting } },
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([component])) } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: catalogType } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', appResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    const componentState = fixture.componentInstance as unknown as {
      canSubmit: () => boolean;
      editing: () => AppSettingResponse | null;
      formOpen: () => boolean;
      cancelForm: () => void;
      startCreate: () => Promise<void>;
      startEdit: (value: AppSettingResponse) => void;
    };
    await vi.waitFor(() => expect(catalogType).toHaveBeenCalled());

    componentState.startEdit(setting);
    expect(componentState.editing()).toBe(setting);
    expect(componentState.canSubmit()).toBe(true);
    await componentState.startCreate();
    expect(componentState.editing()).toBe(setting);
    expect(componentState.formOpen()).toBe(true);
    expect(componentState.canSubmit()).toBe(true);
    expect(createAppSetting).not.toHaveBeenCalled();
    expect(updateAppSetting).not.toHaveBeenCalled();

    componentState.cancelForm();
    expect(componentState.formOpen()).toBe(false);
    expect(createAppSetting).not.toHaveBeenCalled();
    expect(updateAppSetting).not.toHaveBeenCalled();
  });

  it('preserves the draft and blocks stale updates after a version conflict', async () => {
    const appResource: ResourceResponse = {
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
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [appResource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const setting: AppSettingResponse = {
      id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
      identityResourceId: null,
      isImplicit: false,
      keyVaultResourceId: null,
      literalValue: '8080',
      name: 'PORT',
      origin: null,
      outputName: null,
      overrides: null,
      pipelineVariableName: null,
      secretFeedKind: null,
      secretName: null,
      secretValueDeclaredNonSecret: false,
      sourceKind: 'Literal',
      sourceResourceId: null,
      version: 1,
      warnings: [],
    };
    const latestSetting: AppSettingResponse = { ...setting, literalValue: '9090', version: 2 };
    const appSettings = vi.fn()
      .mockReturnValueOnce(of([setting]))
      .mockReturnValue(of([latestSetting]));
    const updateAppSetting = vi.fn(() => throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'Changed elsewhere.')));

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings, updateAppSetting } },
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([component])) } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: vi.fn(() => of({ outputs: [] })) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', appResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(appSettings).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['outputOptionsLoading']()).toBe(false));

    const state = fixture.componentInstance as unknown as {
      canSubmit: () => boolean;
      cancelForm: () => void;
      editing: () => AppSettingResponse | null;
      formOpen: () => boolean;
      literalValue: () => string;
      name: () => string;
      save: () => Promise<void>;
      settings: () => AppSettingResponse[];
      settingConflict: () => boolean;
      startEdit: (value: AppSettingResponse) => void;
      updateLiteralValue: (value: string) => void;
    };
    state.startEdit(setting);
    state.updateLiteralValue('1234');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const editButton = [...host.querySelectorAll<HTMLButtonElement>('li button')]
      .find((button) => button.textContent?.includes('Modifier'));
    expect(editButton?.disabled).toBe(true);

    state.startEdit(latestSetting);
    expect(state.editing()).toBe(setting);
    expect(state.literalValue()).toBe('1234');

    await state.save();
    await vi.waitFor(() => expect(appSettings).toHaveBeenCalledTimes(2));
    fixture.detectChanges();
    expect(updateAppSetting).toHaveBeenCalledOnce();
    expect(state.formOpen()).toBe(true);
    expect(state.settingConflict()).toBe(true);
    expect(state.settings()).toEqual([latestSetting]);
    expect(state.name()).toBe('PORT');
    expect(state.literalValue()).toBe('1234');
    expect(state.canSubmit()).toBe(false);
    expect(host.textContent).toContain('Ce paramètre a changé dans un autre onglet.');

    await state.save();
    expect(updateAppSetting).toHaveBeenCalledOnce();

    state.cancelForm();
    state.startEdit(latestSetting);
    expect(state.settingConflict()).toBe(false);
    expect(state.editing()).toBe(latestSetting);
    expect(state.literalValue()).toBe('9090');
  });

  it('offers output sources from every component in the project', async () => {
    const appResource: ResourceResponse = {
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
    const localIdentity: ResourceResponse = { ...appResource, id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd', type: 'UserAssignedIdentity', logicalName: 'ordersIdentity' };
    const remoteIdentity: ResourceResponse = { ...appResource, id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', type: 'UserAssignedIdentity', logicalName: 'sharedIdentity' };
    const sqlServer: ResourceResponse = { ...appResource, id: vaultId, type: 'SqlServer', logicalName: 'orders' };
    const keyVault: ResourceResponse = {
      ...appResource,
      id: secondVaultId,
      type: 'KeyVault',
      logicalName: 'main',
    };
    const applicationComponent = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [appResource, localIdentity],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const dataComponent = {
      ...applicationComponent,
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      name: 'Data',
      code: 'data',
      resources: [sqlServer, remoteIdentity],
    };
    const coreComponent = {
      ...applicationComponent,
      id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
      name: 'Core',
      code: 'core',
      resources: [keyVault],
    };
    const listComponents = vi.fn(() => of([applicationComponent, dataComponent, coreComponent]));

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings: vi.fn(() => of([])) } },
        { provide: ComponentsService, useValue: { list: listComponents } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        {
          provide: CatalogService,
          useValue: {
            type: vi.fn((_version: string, type: string) =>
              of(type === 'SqlServer' ? { outputs: [{ name: 'fullyQualifiedDomainName', sensitive: false }] } : { outputs: [] }),
            ),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', applicationComponent);
    fixture.componentRef.setInput('resource', appResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(listComponents).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['outputChoices']()).toHaveLength(1));

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button')?.click();
    await vi.waitFor(() => expect(fixture.componentInstance['formOpen']()).toBe(true));
    fixture.componentInstance['updateSourceKind']('ResourceOutput');
    fixture.detectChanges();

    const sourceOptions = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLSelectElement>(
        'select[name="sourceResourceId"] option',
      ),
    ].map((option) => option.textContent?.trim());
    expect(sourceOptions).toContain('data / orders · SqlServer');

    fixture.componentInstance['sourceResourceId'].set(vaultId);
    expect(fixture.componentInstance['hasUnavailableReference']()).toBe(false);
    fixture.detectChanges();
    const outputOptions = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLSelectElement>('select[name="outputName"] option'),
    ].map((option) => option.textContent?.trim());
    expect(outputOptions).toContain('fullyQualifiedDomainName · data / orders · SqlServer');

    fixture.componentInstance['updateSourceKind']('KeyVaultSecret');
    fixture.componentInstance['updateName']('Payments__ApiKey');
    fixture.detectChanges();
    const keyVaultOptions = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLSelectElement>(
        'select[name="keyVaultResourceId"] option',
      ),
    ].map((option) => option.textContent?.trim());
    expect(keyVaultOptions).toContain('core / main · KeyVault');
    const identityOptions = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLSelectElement>(
        'select[name="identityResourceId"] option',
      ),
    ].map((option) => option.textContent?.trim());
    expect(identityOptions).toContain('orders / ordersIdentity · UserAssignedIdentity');
    expect(identityOptions).not.toContain('data / sharedIdentity · UserAssignedIdentity');
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[name="secretName"]',
    )?.value).toBe('payments-api-key');
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      'input[name="pipelineVariableName"]',
    )?.value).toBe('MAIN_PAYMENTS_API_KEY');
  });

  it('proposes pipeline-secret conversion when a literal looks like a secret', async () => {
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
    const vault: ResourceResponse = { ...resource, id: vaultId, type: 'KeyVault', logicalName: 'main' };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Orders',
      code: 'orders',
      resources: [resource, vault],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const appSettings = vi.fn(() => of([]));
    const createAppSetting = vi.fn(() => throwError(() => new ApiError(400, 'VAL-PAR-SECRET-SUSPECT', 'Secret-like value.')));
    const updateAppSetting = vi.fn();

    TestBed.configureTestingModule({
      imports: [ResourceAppSettingsPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceAppSettingsTranslations,
        }),
        { provide: ResourcesService, useValue: { appSettings, createAppSetting, updateAppSetting } },
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([component])) } },
        { provide: ProjectsService, useValue: { get: vi.fn(() => of({ catalogVersion: 'pilot' })) } },
        { provide: CatalogService, useValue: { type: vi.fn(() => of({ outputs: [] })) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceAppSettingsPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(appSettings).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['loading']()).toBe(false));
    await vi.waitFor(() => expect(fixture.componentInstance['keyVaults']()).toHaveLength(1));
    await vi.waitFor(() => expect(fixture.componentInstance['outputOptionsLoading']()).toBe(false));

    const host = fixture.nativeElement as HTMLElement;
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Ajouter un paramètre'))
      ?.click();
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance['formOpen']()).toBe(true));
    fixture.componentInstance['updateSourceKind']('Literal');
    fixture.componentInstance['updateName']('Test__Token');
    fixture.componentInstance['updateLiteralValue'](`ghp_${'a'.repeat(36)}`);
    fixture.detectChanges();
    host.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await vi.waitFor(() => expect(createAppSetting).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['secretSuspectError']()).toBe(true));
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(host.textContent).toContain('Convertir en secret de pipeline');
    });

    const convertButton = [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'Convertir en secret de pipeline');
    expect(convertButton).toBeTruthy();
    convertButton?.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance['sourceKind']()).toBe('KeyVaultSecret');
    expect(fixture.componentInstance['literalValue']()).toBe('');
    expect(fixture.componentInstance['secretName']()).toBe('test-token');
    expect(fixture.componentInstance['pipelineVariableName']()).toBe('MAIN_TEST_TOKEN');
    expect(host.querySelector<HTMLInputElement>('input[name="literalValue"]')).toBeNull();
    expect(createAppSetting).toHaveBeenCalledOnce();
  });

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
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([componentWithTwoVaults])) } },
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
    await vi.waitFor(() => expect(fixture.componentInstance['outputOptionsLoading']()).toBe(false));

    const host = fixture.nativeElement as HTMLElement;
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Ajouter un paramètre'))
      ?.click();
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance['formOpen']()).toBe(true));

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
    await vi.waitFor(() => expect(fixture.componentInstance['formOpen']()).toBe(true));

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
