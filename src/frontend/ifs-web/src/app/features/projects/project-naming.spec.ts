import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectNaming } from './project-naming';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement du projet…',
        loadError: 'Impossible de charger le projet.',
        namingTitle: 'Nommage',
        namingSave: 'Enregistrer',
        namingDescription:
          "Un seul calcul, côté serveur, pour l'écran, la validation et le code produit.",
        namingPreviewTitle: 'Aperçu en direct · {{count}} ressources',
        namingTemplatesTitle: 'Gabarits du projet',
        namingTypeTemplate: 'Gabarit pour un type',
        namingAbbreviationLabel: 'Abréviation de {{type}}',
        namingRemoveTemplate: 'Retirer le gabarit de {{type}}',
        namingRemove: 'Retirer',
        namingTypeLabel: 'Type',
        namingTemplateValue: 'Gabarit',
        namingAddTemplate: 'Ajouter',
        namingOriginProjectDefault: 'Défaut du projet',
        namingTarget: 'Cible',
        namingResource: 'Ressource',
        namingAzureName: 'Nom Azure',
        namingLength: 'Long.',
        namingOrigin: 'Origine',
        namingDefaultTemplate: 'Gabarit par défaut',
        namingResourceGroup: 'ResourceGroup',
        namingStorageAccount: 'StorageAccount',
        namingAbbreviations: 'Abréviations',
        namingCatalogDefault: 'Par défaut : celles du catalogue (Cloud Adoption Framework)',
        namingCatalogOrigin: 'Catalogue',
        namingProjectOrigin: 'Projet',
        namingPublishedTitle: 'Les gabarits de nommage sont verrouillés après publication',
        namingPublishedDescription:
          'Aucun changement de nom publié ne peut être enregistré depuis cet écran.',
        namingProjectRefreshError:
          'Enregistrement réussi, mais le projet n’a pas pu être actualisé. Réessayez.',
        retry: 'Réessayer',
      },
    });
  }
}

describe('ProjectNaming', () => {
  it('loads the server preview and locks published settings', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const nextProjectId = '77777777-7777-4777-8777-777777777778';
    const pendingProject = new Subject<{
      id: string;
      version: number;
      code: string;
      catalogVersion: string;
    }>();
    const pendingSettings = new Subject<{
      projectId: string;
      version: number;
      preset: string;
      defaultTemplate: string;
      resourceGroupTemplate: string;
      typeOverrides: unknown[];
      isPublished: boolean;
    }>();
    const projects = {
      get: vi.fn((id: string) =>
        id === nextProjectId
          ? pendingProject.asObservable()
          : of({ id, version: 2, code: 'shop', catalogVersion: '2026.10-pilot' }),
      ),
      namingSettings: vi.fn((id: string) =>
        id === nextProjectId
          ? pendingSettings.asObservable()
          : of({
          projectId: id,
          version: 2,
          preset: 'Compact',
          defaultTemplate: '{abbr}-{project}-{name}-{env}',
          resourceGroupTemplate: 'rg-{project}-{component}-{name}-{env}',
          typeOverrides: [],
          isPublished: true,
            }),
      ),
      saveNamingSettings: vi.fn(() => of(null)),
      previewNaming: vi.fn().mockReturnValue(
        of({
          items: [
            {
              componentId: '88888888-8888-4888-8888-888888888888',
              componentCode: 'core',
              resourceId: '99999999-9999-4999-8999-999999999999',
              resourceType: 'KeyVault',
              logicalName: 'main',
              targetId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
              targetCode: 'dev',
              resourceName: 'kv-shop-main-dev',
              resourceGroupName: 'rg-shop-core-main-dev',
              nameLength: 16,
              maximumLength: 24,
              isExisting: false,
              explanation: {
                level: 'ProjectDefault',
                template: '{abbr}-{project}-{name}-{env}',
                rawName: 'kv-shop-main-dev',
                finalName: 'kv-shop-main-dev',
                transformations: [],
              },
              resourceGroupExplanation: null,
            },
          ],
          issues: [],
        }),
      ),
      notifyProjectUpdated: vi.fn(),
    };
    const catalog = {
      types: vi.fn().mockReturnValue(
        of([
          {
            type: 'KeyVault',
            labelFr: 'Coffre de clés',
            labelEn: 'Key Vault',
            abbreviation: 'kv',
            category: 'security',
            icon: 'key-vault',
          },
        ]),
      ),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectNaming],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId/naming', component: ProjectNaming }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: projects },
        { provide: CatalogService, useValue: catalog },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    }).compileComponents();

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/o/org/projects/${projectId}/naming`, ProjectNaming);
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(catalog.types).toHaveBeenCalledWith('2026.10-pilot');
    await vi.waitFor(() => expect(projects.previewNaming).toHaveBeenCalled());
    expect(harness.routeNativeElement?.textContent).toContain('kv-shop-main-dev');
    expect(harness.routeNativeElement?.textContent).toContain('Coffre de clés');
    expect(harness.routeNativeElement?.textContent).toContain(
      'Les gabarits de nommage sont verrouillés après publication',
    );
    expect(harness.routeNativeElement?.querySelector('input')?.disabled).toBe(true);

    const component = harness.routeDebugElement?.componentInstance as ProjectNaming;
    const componentState = component as unknown as {
      settings: {
        (): {
          projectId: string;
          version: number;
          preset: string;
          defaultTemplate: string;
          resourceGroupTemplate: string;
          typeOverrides: unknown[];
          isPublished: boolean;
        } | null;
        set(value: {
          projectId: string;
          version: number;
          preset: string;
          defaultTemplate: string;
          resourceGroupTemplate: string;
          typeOverrides: unknown[];
          isPublished: boolean;
        }): void;
      };
      defaultTemplate: { set(value: string): void };
      save: () => Promise<void>;
      loading: () => boolean;
      preview: () => unknown;
      selectedTarget: () => string | undefined;
    };
    const loadedSettings = componentState.settings();
    expect(loadedSettings).not.toBeNull();
    componentState.settings.set({ ...loadedSettings!, isPublished: false });
    componentState.defaultTemplate.set('pending-{name}');
    const previewCallsBeforeNavigation = projects.previewNaming.mock.calls.length;

    await harness.navigateByUrl(`/o/org/projects/${nextProjectId}/naming`, ProjectNaming);
    harness.fixture.detectChanges();
    expect(componentState.loading()).toBe(true);
    expect(componentState.preview()).toBeNull();
    expect(componentState.selectedTarget()).toBeUndefined();
    expect(
      (harness.routeNativeElement?.querySelector('app-ds-button button') as HTMLButtonElement | null)
        ?.disabled,
    ).toBe(true);

    await componentState.save();
    expect(projects.saveNamingSettings).not.toHaveBeenCalled();

    pendingProject.next({
      id: nextProjectId,
      version: 2,
      code: 'shop-next',
      catalogVersion: '2026.10-pilot',
    });
    pendingProject.complete();
    pendingSettings.next({
      projectId: nextProjectId,
      version: 3,
      preset: 'Compact',
      defaultTemplate: '{abbr}-{project}-{name}-{env}',
      resourceGroupTemplate: 'rg-{project}-{component}-{name}-{env}',
      typeOverrides: [],
      isPublished: false,
    });
    pendingSettings.complete();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    await new Promise<void>((resolve) => setTimeout(resolve, 300));

    expect(projects.get).toHaveBeenCalledTimes(2);
    expect(projects.get).toHaveBeenLastCalledWith(nextProjectId);
    expect(projects.namingSettings).toHaveBeenLastCalledWith(nextProjectId);
    expect(projects.previewNaming).toHaveBeenCalledTimes(previewCallsBeforeNavigation + 1);
    expect(projects.previewNaming).toHaveBeenLastCalledWith(
      nextProjectId,
      expect.objectContaining({ defaultTemplate: '{abbr}-{project}-{name}-{env}' }),
    );
  });

  it('locks naming inputs while saving and distinguishes a saved change from a failed refresh', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const project = { id: projectId, version: 2, code: 'shop', catalogVersion: '2026.10-pilot' };
    const initialSettings = {
      projectId,
      version: 2,
      preset: 'Compact',
      defaultTemplate: '{abbr}-{project}-{name}-{env}',
      resourceGroupTemplate: 'rg-{project}-{component}-{name}-{env}',
      typeOverrides: [{ type: 'KeyVault', template: 'kv-{project}-{name}', abbreviation: 'kv' }],
      isPublished: false,
    };
    const savedSettings = {
      ...initialSettings,
      version: 3,
      defaultTemplate: 'new-{name}',
      resourceGroupTemplate: 'new-rg-{name}',
    };
    const pendingSave = new Subject<typeof savedSettings>();
    const projects = {
      get: vi
        .fn()
        .mockReturnValueOnce(of(project))
        .mockReturnValueOnce(throwError(() => new Error('temporary read failure')))
        .mockReturnValue(of(project)),
      namingSettings: vi
        .fn()
        .mockReturnValueOnce(of(initialSettings))
        .mockReturnValue(of(savedSettings)),
      saveNamingSettings: vi.fn(() => pendingSave.asObservable()),
      previewNaming: vi.fn(() => of({
        items: [{
          componentId: '88888888-8888-4888-8888-888888888888',
          componentCode: 'core',
          resourceId: '99999999-9999-4999-8999-999999999999',
          resourceType: 'KeyVault',
          logicalName: 'main',
          targetId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
          targetCode: 'dev',
          resourceName: 'kv-shop-main-dev',
          resourceGroupName: 'rg-shop-core-main-dev',
          nameLength: 16,
          maximumLength: 24,
          isExisting: false,
          explanation: {
            level: 'ProjectDefault',
            template: '{abbr}-{project}-{name}-{env}',
            rawName: 'kv-shop-main-dev',
            finalName: 'kv-shop-main-dev',
            transformations: [],
          },
          resourceGroupExplanation: null,
        }],
        issues: [],
      })),
      notifyProjectUpdated: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [ProjectNaming],
      providers: [
        { provide: ProjectsService, useValue: projects },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([
              { type: 'KeyVault', labelFr: 'Coffre de clés', labelEn: 'Key Vault', abbreviation: 'kv', category: 'security', icon: 'key-vault' },
              { type: 'ContainerApp', labelFr: 'Application conteneur', labelEn: 'Container App', abbreviation: 'ca', category: 'compute', icon: 'container' },
            ])),
          },
        },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    });

    const fixture = TestBed.createComponent(ProjectNaming);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as {
      defaultTemplate: { (): string; set(value: string): void };
      loading: () => boolean;
      isDirty: () => boolean;
      resourceGroupTemplate: { (): string; set(value: string): void };
      newTemplateValue: { set(value: string): void };
      projectRefreshError: () => boolean;
      retryProjectRefresh: () => Promise<void>;
      save: () => Promise<void>;
      saveError: () => boolean;
      startAddingTemplate: () => void;
    };
    await vi.waitFor(() => expect(component.loading()).toBe(false));
    fixture.detectChanges();

    component.defaultTemplate.set(' new-{name} ');
    component.resourceGroupTemplate.set(' new-rg-{name} ');
    component.startAddingTemplate();
    component.newTemplateValue.set('app-{name}');
    const savePromise = component.save();
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector<HTMLInputElement>('input[name="defaultTemplate"]')?.disabled).toBe(true);
    expect(host.querySelector<HTMLInputElement>('input[name="resourceGroupTemplate"]')?.disabled).toBe(true);
    expect(host.querySelector<HTMLInputElement>('input[name="template-KeyVault"]')?.disabled).toBe(true);
    expect(host.querySelector<HTMLInputElement>('input[id="abbreviation-KeyVault"]')?.disabled).toBe(true);
    expect(host.querySelector<HTMLSelectElement>('#naming-template-type')?.disabled).toBe(true);
    expect([...host.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Retirer')?.disabled).toBe(true);
    expect([...host.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Ajouter')?.disabled).toBe(true);

    pendingSave.next(savedSettings);
    pendingSave.complete();
    await savePromise;
    fixture.detectChanges();

    expect(component.saveError()).toBe(false);
    expect(component.defaultTemplate()).toBe('new-{name}');
    expect(component.resourceGroupTemplate()).toBe('new-rg-{name}');
    expect(component.isDirty()).toBe(false);
    expect(component.projectRefreshError()).toBe(true);
    expect(host.textContent).toContain(
      'Enregistrement réussi, mais le projet n’a pas pu être actualisé. Réessayez.',
    );

    component.defaultTemplate.set('draft-after-save-{name}');
    await component.retryProjectRefresh();
    fixture.detectChanges();
    expect(component.projectRefreshError()).toBe(false);
    expect(component.saveError()).toBe(false);
    expect(component.defaultTemplate()).toBe('draft-after-save-{name}');
    expect(component.isDirty()).toBe(true);
  });

  it('clears a stale project refresh state after navigating to another project', async () => {
    const firstProjectId = '77777777-7777-4777-8777-777777777777';
    const secondProjectId = '77777777-7777-4777-8777-777777777778';
    const firstProject = {
      id: firstProjectId,
      version: 2,
      code: 'shop',
      catalogVersion: '2026.10-pilot',
    };
    const secondProject = {
      id: secondProjectId,
      version: 4,
      code: 'other',
      catalogVersion: '2026.10-pilot',
    };
    const settings = {
      projectId: firstProjectId,
      version: 2,
      preset: 'Compact',
      defaultTemplate: '{abbr}-{project}-{name}-{env}',
      resourceGroupTemplate: 'rg-{project}-{component}-{name}-{env}',
      typeOverrides: [],
      isPublished: false,
    };
    const pendingRefresh = new Subject<typeof firstProject>();
    const projects = {
      get: vi.fn().mockReturnValueOnce(of(firstProject)),
      namingSettings: vi.fn(() => of(settings)),
      saveNamingSettings: vi.fn(),
      previewNaming: vi.fn(() => of({ items: [], issues: [] })),
      notifyProjectUpdated: vi.fn(),
    };
    const catalog = { types: vi.fn(() => of([])) };

    TestBed.configureTestingModule({
      imports: [ProjectNaming],
      providers: [
        { provide: ProjectsService, useValue: projects },
        { provide: CatalogService, useValue: catalog },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    });

    const fixture = TestBed.createComponent(ProjectNaming);
    fixture.componentRef.setInput('projectId', firstProjectId);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as {
      loading: () => boolean;
      project: () => { id: string } | null;
      projectRefreshLoading: () => boolean;
      retryProjectRefresh: () => Promise<void>;
    };
    await vi.waitFor(() => expect(component.loading()).toBe(false));

    projects.get
      .mockReturnValueOnce(pendingRefresh.asObservable())
      .mockReturnValue(of(secondProject));
    const refreshRequest = component.retryProjectRefresh();
    expect(component.projectRefreshLoading()).toBe(true);

    fixture.componentRef.setInput('projectId', secondProjectId);
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.project()?.id).toBe(secondProjectId));
    await vi.waitFor(() => expect(component.loading()).toBe(false));
    expect(component.projectRefreshLoading()).toBe(false);

    pendingRefresh.next(firstProject);
    pendingRefresh.complete();
    await refreshRequest;
    expect(component.project()?.id).toBe(secondProjectId);
    expect(component.projectRefreshLoading()).toBe(false);
  });
});
