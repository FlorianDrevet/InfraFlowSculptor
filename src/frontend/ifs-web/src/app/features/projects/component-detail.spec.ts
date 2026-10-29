import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ComponentResponse,
  ResourceDeletionImpactResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ComponentDetail } from './component-detail';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      component: {
        allEnvironments: 'Tous les environnements',
        backToComponents: 'Composants',
        dependenciesHeading: 'Dépendances',
        dependsOn: 'Dépend de',
        loading: 'Chargement du composant…',
        loadError: 'Le composant n’a pas pu être chargé.',
        modePerEnvironment: 'Par environnement',
        resourcesHeading: 'Ressources',
        settingsLabel: 'Réglages',
        usedBy: 'Utilisé par',
      },
      resource: {
        add: 'Ajouter la ressource',
        analysisStillPending: 'Analyse encore partielle',
        cancel: 'Annuler',
        confirmDeleteByName: 'Tapez {name} pour confirmer',
        delete: 'Supprimer du modèle',
        deletedWithResource: 'Supprimé avec elle',
        deleteAlreadyRemoved: 'Cette ressource a déjà été supprimée par une autre action.',
        deleteSuccess: 'Ressource supprimée du modèle.',
        deleteConflict: 'La ressource a changé. Vérifiez l’impact recalculé.',
        deleteDetachesAzureResource: 'La ressource sera détachée.',
        deleteError: 'La ressource n’a pas pu être supprimée.',
        deletionInProgress: 'Suppression en cours…',
        deleteTitle: 'Supprimer {name} du modèle ?',
        impactLoadError: 'L’impact n’a pas pu être recalculé.',
        loadingImpact: 'Calcul de l’impact…',
        noCalculatedImpacts: 'Aucun impact calculé.',
        pendingIncomingLinks: 'Liaisons entrantes',
      },
      resourceGroup: {
        heading: 'Groupes de ressources',
        add: 'Ajouter un groupe de ressources',
        empty: 'Aucun groupe de ressources.',
        resourcesHeading: 'Ressources',
        noResources: 'Aucune ressource dans ce composant.',
        dependenciesHeading: 'Dépendances',
        dependsOn: 'Dépend de',
        usedBy: 'Utilisé par',
        noDependencies: 'Aucun composant',
      },
    });
  }
}

const testProjectId = '77777777-7777-4777-8777-777777777777';
const testComponentId = '88888888-8888-4888-8888-888888888888';
const testResourceId = '99999999-9999-4999-8999-999999999999';

function createTestResource(): ResourceResponse {
  return {
    id: testResourceId,
    resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    type: 'ApplicationInsights',
    logicalName: 'main',
    description: '',
    tags: {},
    version: 8,
    isExisting: false,
    properties: {},
    overrides: {},
    presences: {},
    existingIds: {},
    forcedNames: {},
    children: [],
  };
}

function createTestComponent(resource = createTestResource()): ComponentResponse {
  return {
    id: testComponentId,
    projectId: testProjectId,
    version: 4,
    name: 'Core',
    code: 'core',
    description: '',
    defaultLogAnalyticsResourceId: null,
    mode: 'PerEnvironment',
    targetsAllEnvironments: true,
    targetEnvironmentIds: [],
    singleTarget: null,
    removedResourceBehavior: 'Detach',
    deletionProtectionEnabled: true,
    firstPublishedAt: null,
    defaultNameTemplate: null,
    namingOverrides: [],
    tags: {},
    resourceGroups: [],
    resources: [resource],
  };
}

async function renderComponentDetail(
  componentService: { get: ReturnType<typeof vi.fn> },
  resourceService: {
    deletionImpact: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  },
) {
  await TestBed.configureTestingModule({
    imports: [ComponentDetail],
    providers: [
      provideRouter(
        [
          {
            path: 'o/:orgId/projects/:projectId/components/:componentId',
            component: ComponentDetail,
          },
        ],
        withComponentInputBinding(),
      ),
      { provide: ComponentsService, useValue: componentService },
      {
        provide: ProjectsService,
        useValue: {
          environments: vi.fn().mockReturnValue(of([])),
          myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
        },
      },
      { provide: ResourcesService, useValue: resourceService },
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
  await harness.navigateByUrl(
    `/o/33333333-3333-4333-8333-333333333333/projects/${testProjectId}/components/${testComponentId}`,
    ComponentDetail,
  );
  await harness.fixture.whenStable();
  harness.fixture.detectChanges();
  return { harness, componentElement: harness.routeNativeElement! };
}

async function openTestDeleteDialog(
  harness: RouterTestingHarness,
  componentElement: HTMLElement,
): Promise<HTMLElement> {
  componentElement
    .querySelector<HTMLButtonElement>('.component-detail__resources app-ds-button button')!
    .click();
  await harness.fixture.whenStable();
  harness.fixture.detectChanges();
  return componentElement.querySelector<HTMLElement>('[role="dialog"]')!;
}

function confirmTestDeletion(harness: RouterTestingHarness, dialog: HTMLElement): void {
  const confirmation = dialog.querySelector<HTMLInputElement>(
    'input[name="deleteResourceConfirmation"]',
  )!;
  confirmation.value = 'main';
  confirmation.dispatchEvent(new Event('input', { bubbles: true }));
  harness.fixture.detectChanges();
  [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
    .find((button) => button.textContent?.trim() === 'Supprimer du modèle')!
    .click();
}

describe('ComponentDetail', () => {
  it('renders link impacts as messages instead of resource names', async () => {
    const message = 'Une liaison implicite disparaîtra au prochain déploiement.';
    const warning = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const impact = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [
        {
          code: 'IMPACT-LIA-LINK-IMPLICITE-RETIRE',
          kind: 'Impact',
          type: 'Impact',
          name: message,
          severity: 'Information',
          message,
          linkId: null,
          resourceId: testResourceId,
        },
        {
          code: 'IMPACT-LIA-LINK-IMPLICITE-RETIRE',
          kind: 'Impact',
          type: 'Impact',
          name: message,
          severity: 'Information',
          message,
          linkId: '77777777-7777-4777-8777-777777777777',
          resourceId: testResourceId,
        },
      ],
      pendingAreas: [],
    } as ResourceDeletionImpactResponse;
    const component = { get: vi.fn().mockReturnValue(of(createTestComponent())) };
    const resources = {
      deletionImpact: vi.fn().mockReturnValue(of(impact)),
      delete: vi.fn(),
    };
    const { harness, componentElement } = await renderComponentDetail(component, resources);

    const dialog = await openTestDeleteDialog(harness, componentElement);

    expect(dialog.textContent).toContain(message);
    expect(dialog.querySelector('code')).toBeNull();
    expect(dialog.textContent).not.toContain('Impact');
    expect(dialog.querySelectorAll('li')).toHaveLength(2);
    expect(warning.mock.calls.flat().join(' ')).not.toContain('NG0955');
    warning.mockRestore();
  });

  it('ignores an impact response that arrives after the dialog is cancelled and reopened', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const componentId = '88888888-8888-4888-8888-888888888888';
    const resourceId = '99999999-9999-4999-8999-999999999999';
    const resource = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'ApplicationInsights',
      logicalName: 'main',
      description: '',
      tags: {},
      version: 8,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      children: [],
    } as ResourceResponse;
    const component = {
      id: componentId,
      projectId,
      version: 4,
      name: 'Core',
      code: 'core',
      description: '',
      defaultLogAnalyticsResourceId: null,
      mode: 'PerEnvironment',
      targetsAllEnvironments: true,
      targetEnvironmentIds: [],
      singleTarget: null,
      removedResourceBehavior: 'Detach',
      deletionProtectionEnabled: true,
      firstPublishedAt: null,
      defaultNameTemplate: null,
      namingOverrides: [],
      tags: {},
      resourceGroups: [],
      resources: [resource],
    } as ComponentResponse;
    const staleImpact = {
      resourceVersion: 3,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    } as ResourceDeletionImpactResponse;
    const currentImpact = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    } as ResourceDeletionImpactResponse;
    const firstImpactRequest = new Subject<ResourceDeletionImpactResponse>();
    const componentService = { get: vi.fn().mockReturnValue(of(component)) };
    const projectService = {
      environments: vi.fn().mockReturnValue(of([])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
    };
    const resourceService = {
      deletionImpact: vi
        .fn()
        .mockReturnValueOnce(firstImpactRequest)
        .mockReturnValueOnce(of(currentImpact)),
      delete: vi.fn().mockReturnValue(of({})),
    };

    await TestBed.configureTestingModule({
      imports: [ComponentDetail],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/components/:componentId',
              component: ComponentDetail,
            },
          ],
          withComponentInputBinding(),
        ),
        { provide: ComponentsService, useValue: componentService },
        { provide: ProjectsService, useValue: projectService },
        { provide: ResourcesService, useValue: resourceService },
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
    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/${projectId}/components/${componentId}`,
      ComponentDetail,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const componentElement = harness.routeNativeElement!;
    const openDeleteButton = () =>
      componentElement
        .querySelector<HTMLButtonElement>('.component-detail__resources app-ds-button button')!
        .click();
    openDeleteButton();
    harness.fixture.detectChanges();
    const firstDialog = componentElement.querySelector<HTMLElement>('[role="dialog"]')!;
    [...firstDialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Annuler')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    expect(document.activeElement).toBe(componentElement.querySelector('#resources-heading'));

    openDeleteButton();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    firstImpactRequest.next(staleImpact);
    firstImpactRequest.complete();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const dialog = componentElement.querySelector<HTMLElement>('[role="dialog"]')!;
    const confirmation = dialog.querySelector<HTMLInputElement>(
      'input[name="deleteResourceConfirmation"]',
    )!;
    confirmation.value = 'main';
    confirmation.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Supprimer du modèle')!
      .click();
    await harness.fixture.whenStable();

    expect(resourceService.delete).toHaveBeenCalledWith(projectId, componentId, resourceId, {
      expectedVersion: currentImpact.resourceVersion,
    });
  });

  it('announces a concurrent removal and prevents dismissal while delete is pending', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const componentId = '88888888-8888-4888-8888-888888888888';
    const resourceId = '99999999-9999-4999-8999-999999999999';
    const resource = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'ApplicationInsights',
      logicalName: 'main',
      description: '',
      tags: {},
      version: 8,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      children: [],
    } as ResourceResponse;
    const component = {
      id: componentId,
      projectId,
      version: 4,
      name: 'Core',
      code: 'core',
      description: '',
      defaultLogAnalyticsResourceId: null,
      mode: 'PerEnvironment',
      targetsAllEnvironments: true,
      targetEnvironmentIds: [],
      singleTarget: null,
      removedResourceBehavior: 'Detach',
      deletionProtectionEnabled: true,
      firstPublishedAt: null,
      defaultNameTemplate: null,
      namingOverrides: [],
      tags: {},
      resourceGroups: [],
      resources: [resource],
    } as ComponentResponse;
    const impact = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    } as ResourceDeletionImpactResponse;
    const deleteRequest = new Subject<void>();
    const componentService = {
      get: vi
        .fn()
        .mockReturnValueOnce(of(component))
        .mockReturnValueOnce(
          of({
            ...component,
            resources: [],
          }),
        ),
    };
    const projectService = {
      environments: vi.fn().mockReturnValue(of([])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
    };
    const resourceService = {
      deletionImpact: vi.fn().mockReturnValue(of(impact)),
      delete: vi.fn().mockReturnValue(deleteRequest),
    };

    await TestBed.configureTestingModule({
      imports: [ComponentDetail],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/components/:componentId',
              component: ComponentDetail,
            },
          ],
          withComponentInputBinding(),
        ),
        { provide: ComponentsService, useValue: componentService },
        { provide: ProjectsService, useValue: projectService },
        { provide: ResourcesService, useValue: resourceService },
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
    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/${projectId}/components/${componentId}`,
      ComponentDetail,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const componentElement = harness.routeNativeElement!;
    componentElement
      .querySelector<HTMLButtonElement>('.component-detail__resources app-ds-button button')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const dialog = componentElement.querySelector<HTMLElement>('[role="dialog"]')!;
    const confirmation = dialog.querySelector<HTMLInputElement>(
      'input[name="deleteResourceConfirmation"]',
    )!;
    confirmation.value = 'main';
    confirmation.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Supprimer du modèle')!
      .click();
    harness.fixture.detectChanges();
    expect(resourceService.delete).toHaveBeenCalledTimes(1);
    expect(dialog.querySelector('[role="status"]')?.textContent).toContain('Suppression en cours…');
    const dismissButton = [
      ...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button'),
    ].find((button) => button.textContent?.trim() === 'Annuler')!;
    expect(dismissButton.disabled).toBe(true);
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    harness.fixture.detectChanges();
    expect(componentElement.querySelector('[role="dialog"]')).not.toBeNull();

    deleteRequest.error(new ApiError(409, 'RESOURCE_VERSION_CONFLICT', 'The resource changed.'));
    await vi.waitFor(() => expect(componentService.get).toHaveBeenCalledTimes(2));
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(resourceService.deletionImpact).toHaveBeenCalledTimes(1);
    expect(componentElement.querySelector('[role="dialog"]')).toBeNull();
    expect(componentElement.querySelector('[role="status"]')?.textContent).toContain(
      'Cette ressource a déjà été supprimée par une autre action.',
    );
    expect(document.activeElement).toBe(componentElement.querySelector('[role="status"]'));
  });

  it('keeps deletion open and disabled when refreshing after a conflict fails', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const componentId = '88888888-8888-4888-8888-888888888888';
    const resourceId = '99999999-9999-4999-8999-999999999999';
    const resource = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'ApplicationInsights',
      logicalName: 'main',
      description: '',
      tags: {},
      version: 8,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      children: [],
    } as ResourceResponse;
    const component = {
      id: componentId,
      projectId,
      version: 4,
      name: 'Core',
      code: 'core',
      description: '',
      defaultLogAnalyticsResourceId: null,
      mode: 'PerEnvironment',
      targetsAllEnvironments: true,
      targetEnvironmentIds: [],
      singleTarget: null,
      removedResourceBehavior: 'Detach',
      deletionProtectionEnabled: true,
      firstPublishedAt: null,
      defaultNameTemplate: null,
      namingOverrides: [],
      tags: {},
      resourceGroups: [],
      resources: [resource],
    } as ComponentResponse;
    const impact = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    } as ResourceDeletionImpactResponse;
    const componentService = {
      get: vi
        .fn()
        .mockReturnValueOnce(of(component))
        .mockReturnValueOnce(throwError(() => new Error('temporary read failure'))),
    };
    const projectService = {
      environments: vi.fn().mockReturnValue(of([])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
    };
    const resourceService = {
      deletionImpact: vi.fn().mockReturnValue(of(impact)),
      delete: vi
        .fn()
        .mockReturnValue(
          throwError(() => new ApiError(409, 'RESOURCE_VERSION_CONFLICT', 'The resource changed.')),
        ),
    };

    await TestBed.configureTestingModule({
      imports: [ComponentDetail],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/components/:componentId',
              component: ComponentDetail,
            },
          ],
          withComponentInputBinding(),
        ),
        { provide: ComponentsService, useValue: componentService },
        { provide: ProjectsService, useValue: projectService },
        { provide: ResourcesService, useValue: resourceService },
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
    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/${projectId}/components/${componentId}`,
      ComponentDetail,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const componentElement = harness.routeNativeElement!;
    componentElement
      .querySelector<HTMLButtonElement>('.component-detail__resources app-ds-button button')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const dialog = componentElement.querySelector<HTMLElement>('[role="dialog"]');
    expect(dialog).not.toBeNull();
    const confirmation = dialog!.querySelector<HTMLInputElement>(
      'input[name="deleteResourceConfirmation"]',
    )!;
    confirmation.value = 'main';
    confirmation.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    const confirmButton = [
      ...dialog!.querySelectorAll<HTMLButtonElement>('app-ds-button button'),
    ].find((button) => button.textContent?.trim() === 'Supprimer du modèle');
    expect(confirmButton).toBeDefined();
    confirmButton!.click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(componentService.get).toHaveBeenCalledTimes(2);
    expect(resourceService.deletionImpact).toHaveBeenCalledTimes(1);
    expect(componentElement.querySelector('[role="dialog"]')).not.toBeNull();
    expect(componentElement.querySelector('[role="alert"]')?.textContent).toContain(
      'L’impact n’a pas pu être recalculé.',
    );
    expect(document.activeElement).toBe(confirmation);
    expect(componentElement.querySelectorAll('[role="dialog"] app-ds-button button').length).toBe(
      1,
    );
  });

  it('closes the delete dialog and announces when the impact preview returns not found', async () => {
    const component = createTestComponent();
    const componentService = { get: vi.fn().mockReturnValue(of(component)) };
    const resourceService = {
      deletionImpact: vi
        .fn()
        .mockReturnValue(throwError(() => new ApiError(404, 'RESOURCE_NOT_FOUND', 'Missing.'))),
      delete: vi.fn(),
    };
    const { harness, componentElement } = await renderComponentDetail(
      componentService,
      resourceService,
    );

    componentElement
      .querySelector<HTMLButtonElement>('.component-detail__resources app-ds-button button')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(componentElement.querySelector('[role="dialog"]')).toBeNull();
    expect(componentElement.querySelector('[role="status"]')?.textContent).toContain(
      'Cette ressource a déjà été supprimée par une autre action.',
    );
    expect(document.activeElement).toBe(componentElement.querySelector('[role="status"]'));
    expect(componentService.get).toHaveBeenCalledTimes(2);
  });

  it('closes the delete dialog and announces when deletion returns not found', async () => {
    const component = createTestComponent();
    const impact: ResourceDeletionImpactResponse = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    };
    const componentService = { get: vi.fn().mockReturnValue(of(component)) };
    const resourceService = {
      deletionImpact: vi.fn().mockReturnValue(of(impact)),
      delete: vi
        .fn()
        .mockReturnValue(throwError(() => new ApiError(404, 'RESOURCE_NOT_FOUND', 'Missing.'))),
    };
    const { harness, componentElement } = await renderComponentDetail(
      componentService,
      resourceService,
    );

    const dialog = await openTestDeleteDialog(harness, componentElement);
    confirmTestDeletion(harness, dialog);
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(componentElement.querySelector('[role="dialog"]')).toBeNull();
    expect(componentElement.querySelector('[role="status"]')?.textContent).toContain(
      'Cette ressource a déjà été supprimée par une autre action.',
    );
    expect(document.activeElement).toBe(componentElement.querySelector('[role="status"]'));
    expect(componentService.get).toHaveBeenCalledTimes(2);
  });

  it('announces successful deletion and moves focus to the result', async () => {
    const component = createTestComponent();
    const impact: ResourceDeletionImpactResponse = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    };
    const componentService = {
      get: vi
        .fn()
        .mockReturnValueOnce(of(component))
        .mockReturnValueOnce(of({ ...component, resources: [] })),
    };
    const resourceService = {
      deletionImpact: vi.fn().mockReturnValue(of(impact)),
      delete: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, componentElement } = await renderComponentDetail(
      componentService,
      resourceService,
    );

    const dialog = await openTestDeleteDialog(harness, componentElement);
    confirmTestDeletion(harness, dialog);
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(componentElement.querySelector('[role="dialog"]')).toBeNull();
    expect(componentElement.querySelector('[role="status"]')?.textContent).toContain(
      'Ressource supprimée du modèle.',
    );
    expect(document.activeElement).toBe(componentElement.querySelector('[role="status"]'));
    expect(componentService.get).toHaveBeenCalledTimes(2);
  });

  it('allows dismissal while a non-conflict delete failure reloads the component', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const componentId = '88888888-8888-4888-8888-888888888888';
    const resourceId = '99999999-9999-4999-8999-999999999999';
    const resource = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'ApplicationInsights',
      logicalName: 'main',
      description: '',
      tags: {},
      version: 8,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      children: [],
    } as ResourceResponse;
    const component = {
      id: componentId,
      projectId,
      version: 4,
      name: 'Core',
      code: 'core',
      description: '',
      defaultLogAnalyticsResourceId: null,
      mode: 'PerEnvironment',
      targetsAllEnvironments: true,
      targetEnvironmentIds: [],
      singleTarget: null,
      removedResourceBehavior: 'Detach',
      deletionProtectionEnabled: true,
      firstPublishedAt: null,
      defaultNameTemplate: null,
      namingOverrides: [],
      tags: {},
      resourceGroups: [],
      resources: [resource],
    } as ComponentResponse;
    const impact = {
      resourceVersion: 8,
      status: 'Partial',
      severity: 'Information',
      items: [],
      pendingAreas: ['IncomingLinks'],
    } as ResourceDeletionImpactResponse;
    const refreshedComponent = new Subject<ComponentResponse>();
    const componentService = {
      get: vi.fn().mockReturnValueOnce(of(component)).mockReturnValueOnce(refreshedComponent),
    };
    const projectService = {
      environments: vi.fn().mockReturnValue(of([])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
    };
    const resourceService = {
      deletionImpact: vi.fn().mockReturnValue(of(impact)),
      delete: vi
        .fn()
        .mockReturnValue(
          throwError(() => new ApiError(500, 'RESOURCE_DELETE_FAILED', 'Temporary failure.')),
        ),
    };

    await TestBed.configureTestingModule({
      imports: [ComponentDetail],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/components/:componentId',
              component: ComponentDetail,
            },
          ],
          withComponentInputBinding(),
        ),
        { provide: ComponentsService, useValue: componentService },
        { provide: ProjectsService, useValue: projectService },
        { provide: ResourcesService, useValue: resourceService },
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
    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/${projectId}/components/${componentId}`,
      ComponentDetail,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const componentElement = harness.routeNativeElement!;
    componentElement
      .querySelector<HTMLButtonElement>('.component-detail__resources app-ds-button button')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    const dialog = componentElement.querySelector<HTMLElement>('[role="dialog"]')!;
    const confirmation = dialog.querySelector<HTMLInputElement>(
      'input[name="deleteResourceConfirmation"]',
    )!;
    confirmation.value = 'main';
    confirmation.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Supprimer du modèle')!
      .click();
    await vi.waitFor(() => expect(componentService.get).toHaveBeenCalledTimes(2));
    harness.fixture.detectChanges();

    expect(componentElement.querySelector('[role="dialog"]')).toBe(dialog);
    expect(componentElement.querySelector('[role="status"]')?.textContent).toContain(
      'Chargement du composant…',
    );
    expect(dialog.querySelector('[role="status"]')?.textContent).toContain('Calcul de l’impact…');
    expect(
      dialog.querySelector<HTMLInputElement>('input[name="deleteResourceConfirmation"]')?.readOnly,
    ).toBe(true);
    expect(
      [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')].find((button) =>
        button.textContent?.trim().startsWith('Annuler'),
      )?.disabled,
    ).toBe(false);

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    harness.fixture.detectChanges();
    expect(componentElement.querySelector('[role="dialog"]')).toBeNull();

    refreshedComponent.next(component);
    refreshedComponent.complete();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(componentElement.querySelector('[role="dialog"]')).toBeNull();
  });
});
