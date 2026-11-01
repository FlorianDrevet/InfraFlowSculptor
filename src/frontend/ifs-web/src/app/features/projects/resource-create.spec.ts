import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ComponentResponse,
  ProjectSummaryResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ApiError } from '../../core/http/api-error';
import { ResourceCreate } from './resource-create';

class ResourceCreateTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        initialProperties: 'Propriétés initiales',
        initialPropertiesHelp: 'Les autres propriétés gardent leurs valeurs par défaut.',
        valueAbsent: 'Valeur absente',
        cidrInputLabel: 'Plage CIDR IPv4',
        addRange: 'Ajouter une plage',
        removeRange: 'Retirer',
        cidrInvalid: 'Saisissez un réseau IPv4 CIDR canonique.',
        createdButNavigationFailed:
          'La ressource a été créée, mais son écran ne s’est pas ouvert. Revenez au composant et rechargez-le.',
        arrayInvalid: 'Saisissez un tableau JSON valide.',
        invalidInitialProperty:
          'La valeur de « {{property}} » ne respecte pas les contraintes du catalogue.',
        conflictTitle: 'Conflit de version',
        version: 'Version du composant',
        savedVersion: 'Enregistrée',
        submittedVersion: 'Soumise',
        createConflictSummary: 'Version envoyée {{submitted}}, version actuelle {{current}}.',
        createConflictHelp: 'Rechargez les changements avant de réessayer.',
        abandon: 'Abandonner',
        resume: 'Reprendre',
        saveError: 'La ressource n’a pas pu être enregistrée.',
        partialCreateCleanupFailed:
          'La suppression automatique n’a pas pu être confirmée. Vérifiez l’état de la ressource avant de continuer.',
        partialCreateCleanupRetryFailed:
          'La suppression n’a pas pu être confirmée. Vérifiez l’état de la ressource avant de réessayer.',
        retryPartialCreateCleanup: 'Réessayer la suppression',
      },
    });
  }
}

const organizationId = '33333333-3333-4333-8333-333333333333';
const projectId = '77777777-7777-4777-8777-777777777777';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceGroupId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const targetId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const createdResourceId = '99999999-9999-4999-8999-999999999999';

describe('ResourceCreate', () => {
  it('cancels a pending name preview when the creation screen is destroyed', async () => {
    vi.useFakeTimers();
    const previewName = vi.fn(() => of({ items: [], issues: [] }));
    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        { provide: ProjectsService, useValue: {} },
        { provide: ComponentsService, useValue: {} },
        { provide: CatalogService, useValue: {} },
        { provide: ResourcesService, useValue: { previewName } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    const singleTargetComponent = {
      id: componentId,
      singleTarget: { id: targetId, code: 'shared', region: 'francecentral' },
    } as unknown as ComponentResponse;
    const componentWithPreview = fixture.componentInstance as unknown as {
      logicalName: { set: (value: string) => void };
      component: { set: (value: ComponentResponse) => void };
      resourceGroupId: { set: (value: string) => void };
      queueNamePreview: () => void;
    };
    componentWithPreview.logicalName.set('kv-main');
    componentWithPreview.component.set(singleTargetComponent);
    componentWithPreview.resourceGroupId.set(resourceGroupId);
    componentWithPreview.queueNamePreview();

    fixture.destroy();
    await vi.advanceTimersByTimeAsync(200);
    vi.useRealTimers();

    expect(previewName).not.toHaveBeenCalled();
  });

  it('clears previously generated names when the latest preview fails', async () => {
    const previewName = vi
      .fn()
      .mockReturnValueOnce(
        of({
          items: [
            {
              targetId,
              targetCode: 'main',
              resourceGroupName: 'main',
              resourceName: 'kv-previous',
            },
          ],
          issues: [],
        }),
      )
      .mockImplementationOnce(() => throwError(() => new Error('Preview unavailable.')));
    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        { provide: ProjectsService, useValue: {} },
        { provide: ComponentsService, useValue: {} },
        { provide: CatalogService, useValue: {} },
        { provide: ResourcesService, useValue: { previewName } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('type', 'KeyVault');
    const componentWithPreview = fixture.componentInstance as unknown as {
      targets: { set: (value: Array<{ id: string; code: string; region: string }>) => void };
      generatedNames: () => unknown[];
      generatedNamesError: () => boolean;
      loadNamePreview: (
        sequence: number,
        logicalName: string,
        componentId: string,
        resourceGroupId: string,
      ) => Promise<void>;
    };
    componentWithPreview.targets.set([{ id: targetId, code: 'main', region: 'francecentral' }]);

    await componentWithPreview.loadNamePreview(0, 'previous', componentId, resourceGroupId);
    expect(componentWithPreview.generatedNames()).toHaveLength(1);
    await componentWithPreview.loadNamePreview(0, 'latest', componentId, resourceGroupId);

    expect(componentWithPreview.generatedNames()).toHaveLength(0);
    expect(componentWithPreview.generatedNamesError()).toBe(true);
  });

  it('closes the creation flow back to the component instead of the type picker', () => {
    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        { provide: ProjectsService, useValue: {} },
        { provide: ComponentsService, useValue: {} },
        { provide: CatalogService, useValue: {} },
        { provide: ResourcesService, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(ResourceCreate);
    const route = TestBed.inject(ActivatedRoute);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    const component = fixture.componentInstance as unknown as { abandonCreate: () => void };

    component.abandonCreate();

    expect(navigate).toHaveBeenCalledWith(['../../../'], { relativeTo: route });
  });

  it('reloads the resource descriptor when its route inputs change', async () => {
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
      singleTarget: { id: targetId, code: 'main', region: 'francecentral' },
    } as unknown as ComponentResponse;
    const catalogType = vi.fn((version: string, type: string) =>
      of({ properties: [{ name: `${version}-${type}`, kind: 'string', overridable: true }] }),
    );

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            environments: vi.fn(() => of([])),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([])),
            type: catalogType,
          },
        },
        {
          provide: ResourcesService,
          useValue: { previewName: vi.fn(() => of({ items: [], issues: [] })) },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    fixture.detectChanges();
    await vi.waitFor(() => expect(catalogType).toHaveBeenCalledTimes(1));

    fixture.componentRef.setInput('type', 'ContainerRegistry');
    fixture.detectChanges();
    await vi.waitFor(() => expect(catalogType).toHaveBeenCalledTimes(2));

    expect(catalogType).toHaveBeenLastCalledWith(project.catalogVersion, 'ContainerRegistry');
  });

  it('does not show a version conflict for another HTTP 409 response', async () => {
    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        { provide: ProjectsService, useValue: {} },
        { provide: ComponentsService, useValue: { get: vi.fn() } },
        { provide: CatalogService, useValue: {} },
        {
          provide: ResourcesService,
          useValue: {
            add: vi.fn(() =>
              throwError(
                () => new ApiError(409, 'RESOURCE_LOGICAL_NAME_DUPLICATE', 'Name already used.'),
              ),
            ),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    const componentWithCreate = fixture.componentInstance as unknown as {
      project: { set: (value: ProjectSummaryResponse) => void };
      component: { set: (value: ComponentResponse) => void };
      canEditModel: { set: (value: boolean) => void };
      logicalName: { set: (value: string) => void };
      resourceGroupId: { set: (value: string) => void };
      create: () => Promise<void>;
      conflict: () => boolean;
      saveError: () => boolean;
    };
    componentWithCreate.project.set({
      catalogVersion: '2026.10-pilot.2',
    } as ProjectSummaryResponse);
    componentWithCreate.component.set({
      id: componentId,
      version: 1,
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
    } as unknown as ComponentResponse);
    componentWithCreate.canEditModel.set(true);
    componentWithCreate.logicalName.set('kv-main');
    componentWithCreate.resourceGroupId.set(resourceGroupId);

    await componentWithCreate.create();

    expect(componentWithCreate.conflict()).toBe(false);
    expect(componentWithCreate.saveError()).toBe(true);
  });

  it('moves the selected resource group to a surviving group after a version conflict', async () => {
    const previousGroupId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
    const replacementGroupId = 'dddddddd-dddd-4ddd-8ddd-dddddddddddd';
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: previousGroupId, name: 'old' }],
    } as unknown as ComponentResponse;
    const latestComponent = {
      ...component,
      version: 2,
      resourceGroups: [{ id: replacementGroupId, name: 'replacement' }],
    } as unknown as ComponentResponse;
    const createdResource = {
      id: createdResourceId,
      version: 1,
      type: 'KeyVault',
      logicalName: 'kv-main',
    } as ResourceResponse;
    const addCalls: Array<[string, string, { resourceGroupId: string; expectedVersion: number }]> =
      [];
    const addResource = vi.fn(
      (
        requestedProjectId: string,
        requestedComponentId: string,
        request: { resourceGroupId: string; expectedVersion: number },
      ) => {
        addCalls.push([requestedProjectId, requestedComponentId, request]);
        return addCalls.length === 1
          ? throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'Component changed.'))
          : of(createdResource);
      },
    );
    const componentGet = vi.fn(() => of(latestComponent));

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        { provide: ProjectsService, useValue: {} },
        { provide: ComponentsService, useValue: { get: componentGet } },
        { provide: CatalogService, useValue: {} },
        {
          provide: ResourcesService,
          useValue: { add: addResource, previewName: vi.fn(() => of({ items: [], issues: [] })) },
        },
      ],
    });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    const componentWithCreate = fixture.componentInstance as unknown as {
      project: { set: (value: ProjectSummaryResponse) => void };
      component: { set: (value: ComponentResponse) => void };
      canEditModel: { set: (value: boolean) => void };
      logicalName: { set: (value: string) => void };
      resourceGroupId: { set: (value: string) => void };
      create: () => Promise<void>;
      resumeCreate: () => void;
    };
    componentWithCreate.project.set({
      catalogVersion: '2026.10-pilot.2',
    } as ProjectSummaryResponse);
    componentWithCreate.component.set(component);
    componentWithCreate.canEditModel.set(true);
    componentWithCreate.logicalName.set('kv-main');
    componentWithCreate.resourceGroupId.set(previousGroupId);

    await componentWithCreate.create();
    expect(componentGet).toHaveBeenCalledOnce();
    componentWithCreate.resumeCreate();
    await vi.waitFor(() => expect(addResource).toHaveBeenCalledTimes(2));

    expect(addCalls[1]?.[2]).toEqual(
      expect.objectContaining({ resourceGroupId: replacementGroupId, expectedVersion: 2 }),
    );
  });

  it('routes the cancel and close links to the component page', async () => {
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
      singleTarget: { id: targetId, code: 'main', region: 'francecentral' },
    } as unknown as ComponentResponse;

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/components/:componentId/resources/new/:type',
              component: ResourceCreate,
            },
          ],
          withComponentInputBinding(),
        ),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            environments: vi.fn(() => of([])),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([{ type: 'KeyVault', labelFr: 'Coffre de clés' }])),
            type: vi.fn(() => of({ properties: [] })),
          },
        },
        {
          provide: ResourcesService,
          useValue: { previewName: vi.fn(() => of({ items: [], issues: [] })) },
        },
      ],
    });

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(
      `/o/${organizationId}/projects/${projectId}/components/${componentId}/resources/new/KeyVault`,
      ResourceCreate,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const host = harness.routeNativeElement!;
    await vi.waitFor(() => expect(host.querySelector('form')).not.toBeNull());
    harness.fixture.detectChanges();
    const cancelLink = host.querySelector<HTMLAnchorElement>('.resource-create__footer a');
    const closeLink = host.querySelector<HTMLAnchorElement>('.resource-create__close');
    const componentPath = `/o/${organizationId}/projects/${projectId}/components/${componentId}`;

    expect(cancelLink?.getAttribute('href')).toBe(componentPath);
    expect(closeLink?.getAttribute('href')).toBe(componentPath);
  });

  it('reports when cleanup cannot remove a partially created resource', async () => {
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
      singleTarget: { id: targetId, code: 'main', region: 'francecentral' },
    } as unknown as ComponentResponse;
    const createdResource = {
      id: createdResourceId,
      version: 1,
      type: 'KeyVault',
      logicalName: 'kv-recipe',
    } as ResourceResponse;
    const refreshedComponent = {
      ...component,
      version: 2,
      resources: [{ ...createdResource, version: 2 }],
    } as unknown as ComponentResponse;
    const componentGet = vi
      .fn()
      .mockReturnValue(of(refreshedComponent))
      .mockReturnValueOnce(of(component))
      .mockImplementationOnce(() =>
        throwError(() => new Error('Temporary component refresh failure.')),
      );
    const deleteResource = vi
      .fn()
      .mockImplementationOnce(() => throwError(() => new Error('Temporary delete failure.')))
      .mockImplementationOnce(() =>
        throwError(() => new ApiError(500, 'DELETE_FAILED', 'Temporary delete failure.')),
      )
      .mockImplementationOnce(() => of(undefined));
    const addResource = vi.fn(() => of(createdResource));

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceCreateTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            environments: vi.fn(() => of([])),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: componentGet } },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([{ type: 'KeyVault', labelFr: 'Coffre de clés' }])),
            type: vi.fn(() => of({ properties: [] })),
          },
        },
        {
          provide: ResourcesService,
          useValue: {
            add: addResource,
            setExistingResourceId: vi.fn(() =>
              throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'The component changed.')),
            ),
            delete: deleteResource,
            previewName: vi.fn(() => of({ items: [], issues: [] })),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    fixture.componentRef.setInput('existing', true);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('form')).not.toBeNull());

    const componentWithCreate = fixture.componentInstance as unknown as {
      logicalName: { set: (value: string) => void };
      updateExistingId: (id: string, value: string) => void;
      create: () => Promise<void>;
      partialCreateCleanupFailed: () => boolean;
      retryPartialCreateCleanup: () => Promise<void>;
      partialCreateCleanupRetryFailed: () => boolean;
      conflict: () => boolean;
      saveError: () => boolean;
    };
    componentWithCreate.logicalName.set('kv-recipe');
    componentWithCreate.updateExistingId(
      targetId,
      '/subscriptions/test/resourceGroups/main/providers/Microsoft.KeyVault/vaults/kv-recipe',
    );
    await componentWithCreate.create();
    fixture.detectChanges();

    expect(deleteResource).toHaveBeenCalledOnce();
    expect(host.textContent).toContain('La suppression automatique n’a pas pu être confirmée.');
    expect(
      host.querySelector<HTMLButtonElement>('.resource-create__footer button[type="submit"]')
        ?.disabled,
    ).toBe(true);
    expect(componentWithCreate.partialCreateCleanupFailed()).toBe(true);
    expect(host.querySelector('a.resource-create__close')).toBeNull();
    expect(host.querySelector('.resource-create__footer a')).toBeNull();
    expect(componentWithCreate.conflict()).toBe(false);
    expect(componentWithCreate.saveError()).toBe(false);
    expect(host.textContent).not.toContain('La ressource n’a pas pu être enregistrée.');
    expect(host.querySelector('.resource-create__conflict-backdrop')).toBeNull();
    expect(
      [...host.querySelectorAll('button')].some(
        (button) => button.textContent?.trim() === 'Réessayer la suppression',
      ),
    ).toBe(true);

    await componentWithCreate.create();

    expect(addResource).toHaveBeenCalledOnce();

    await componentWithCreate.retryPartialCreateCleanup();
    fixture.detectChanges();

    expect(deleteResource).toHaveBeenCalledTimes(2);
    expect(deleteResource).toHaveBeenLastCalledWith(projectId, componentId, createdResourceId, {
      expectedVersion: 2,
    });
    expect(componentWithCreate.partialCreateCleanupRetryFailed()).toBe(true);
    expect(host.textContent).toContain(
      'La suppression n’a pas pu être confirmée. Vérifiez l’état de la ressource avant de réessayer.',
    );

    await componentWithCreate.retryPartialCreateCleanup();
    fixture.detectChanges();

    expect(deleteResource).toHaveBeenCalledTimes(3);
    expect(deleteResource).toHaveBeenLastCalledWith(projectId, componentId, createdResourceId, {
      expectedVersion: 2,
    });
    expect(componentWithCreate.partialCreateCleanupFailed()).toBe(false);
    expect(componentWithCreate.partialCreateCleanupRetryFailed()).toBe(false);
    expect(host.textContent).not.toContain('La suppression automatique n’a pas pu être confirmée.');
  });

  it('clears the cleanup warning when a retry reports that the resource is already gone', async () => {
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
      singleTarget: { id: targetId, code: 'main', region: 'francecentral' },
    } as unknown as ComponentResponse;
    const createdResource = {
      id: createdResourceId,
      version: 1,
      type: 'KeyVault',
      logicalName: 'kv-recipe',
    } as ResourceResponse;
    const refreshedComponent = {
      ...component,
      version: 2,
      resources: [{ ...createdResource, version: 2 }],
    } as unknown as ComponentResponse;
    const componentWithoutPartial = {
      ...component,
      version: 3,
      resources: [],
    } as unknown as ComponentResponse;
    const componentGet = vi
      .fn()
      .mockReturnValue(of(componentWithoutPartial))
      .mockReturnValueOnce(of(component))
      .mockReturnValueOnce(of(refreshedComponent))
      .mockReturnValueOnce(of(refreshedComponent));
    const deleteResource = vi
      .fn()
      .mockImplementationOnce(() => throwError(() => new Error('Temporary delete failure.')))
      .mockImplementationOnce(() =>
        throwError(() => new ApiError(404, 'RESOURCE_NOT_FOUND', 'Resource already deleted.')),
      );

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceCreateTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            environments: vi.fn(() => of([])),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: componentGet } },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([{ type: 'KeyVault', labelFr: 'Coffre de clés' }])),
            type: vi.fn(() => of({ properties: [] })),
          },
        },
        {
          provide: ResourcesService,
          useValue: {
            add: vi.fn(() => of(createdResource)),
            setExistingResourceId: vi.fn(() =>
              throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'The component changed.')),
            ),
            delete: deleteResource,
            previewName: vi.fn(() => of({ items: [], issues: [] })),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    fixture.componentRef.setInput('existing', true);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('form')).not.toBeNull());

    const componentWithCreate = fixture.componentInstance as unknown as {
      logicalName: { set: (value: string) => void };
      updateExistingId: (id: string, value: string) => void;
      create: () => Promise<void>;
      partialCreateCleanupFailed: () => boolean;
      retryPartialCreateCleanup: () => Promise<void>;
    };
    componentWithCreate.logicalName.set('kv-recipe');
    componentWithCreate.updateExistingId(
      targetId,
      '/subscriptions/test/resourceGroups/main/providers/Microsoft.KeyVault/vaults/kv-recipe',
    );
    await componentWithCreate.create();

    expect(componentWithCreate.partialCreateCleanupFailed()).toBe(true);

    await componentWithCreate.retryPartialCreateCleanup();
    fixture.detectChanges();

    expect(deleteResource).toHaveBeenCalledTimes(2);
    expect(componentWithCreate.partialCreateCleanupFailed()).toBe(false);
    expect(host.textContent).not.toContain('La suppression automatique n’a pas pu être confirmée.');
  });

  it('keeps a created resource when navigation to its editor fails', async () => {
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
      singleTarget: { id: targetId, code: 'main', region: 'francecentral' },
    } as unknown as ComponentResponse;
    const createdResource = {
      id: createdResourceId,
      version: 1,
      type: 'KeyVault',
      logicalName: 'kv-recipe',
    } as ResourceResponse;
    const addResource = vi.fn(() => of(createdResource));
    const deleteResource = vi.fn(() => of(undefined));

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceCreateTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            environments: vi.fn(() => of([])),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([{ type: 'KeyVault', labelFr: 'Coffre de clés' }])),
            type: vi.fn(() => of({ properties: [] })),
          },
        },
        {
          provide: ResourcesService,
          useValue: {
            add: addResource,
            setPresence: vi.fn(() => of(createdResource)),
            delete: deleteResource,
            previewName: vi.fn(() => of({ items: [], issues: [] })),
          },
        },
      ],
    });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockRejectedValue(
      new Error('Navigation unavailable.'),
    );

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('form')).not.toBeNull());

    const componentWithCreate = fixture.componentInstance as unknown as {
      logicalName: { set: (value: string) => void };
      create: () => Promise<void>;
    };
    componentWithCreate.logicalName.set('kv-recipe');
    await componentWithCreate.create();
    fixture.detectChanges();

    expect(addResource).toHaveBeenCalledOnce();
    expect(deleteResource).not.toHaveBeenCalled();
    expect(host.textContent).toContain(
      'La ressource a été créée, mais son écran ne s’est pas ouvert.',
    );

    await componentWithCreate.create();

    expect(addResource).toHaveBeenCalledOnce();
  });

  it('sends changed descriptor defaults as initial resource properties', async () => {
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      resources: [],
      resourceGroups: [{ id: resourceGroupId, name: 'main' }],
      singleTarget: { id: targetId, code: 'main', region: 'francecentral' },
    } as unknown as ComponentResponse;
    const createdResource = {
      id: createdResourceId,
      version: 1,
      type: 'KeyVault',
      logicalName: 'kv-recipe',
    } as ResourceResponse;
    const addResourceCalls: Array<[string, string, { properties: Record<string, unknown> }]> = [];
    const addResource = vi.fn(
      (
        projectId: string,
        componentId: string,
        request: { properties: Record<string, unknown> },
      ) => {
        addResourceCalls.push([projectId, componentId, request]);
        return of(createdResource);
      },
    );

    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceCreateTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            environments: vi.fn(() => of([])),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: {
            types: vi.fn(() => of([{ type: 'KeyVault', labelFr: 'Coffre de clés' }])),
            type: vi.fn(() =>
              of({
                properties: [
                  {
                    name: 'purgeProtection',
                    labels: { fr: 'Protection contre la purge', en: 'Purge protection' },
                    kind: 'boolean',
                    overridable: true,
                    default: true,
                    irreversible: { from: false, to: true },
                  },
                  {
                    name: 'exposure',
                    labels: { fr: 'Exposition', en: 'Exposure' },
                    kind: 'enum',
                    overridable: true,
                    default: 'Public',
                    values: ['Public', 'Restricted'],
                  },
                  {
                    name: 'allowedIpRanges',
                    labels: { fr: 'Plages IP autorisées', en: 'Allowed IP ranges' },
                    kind: 'array',
                    overridable: true,
                    default: [],
                    condition: 'requires:exposure=Restricted',
                    items: { kind: 'string', format: 'ipv4Cidr' },
                    maxItems: 100,
                  },
                  {
                    name: 'allowedNames',
                    labels: { fr: 'Noms autorisés', en: 'Allowed names' },
                    kind: 'array',
                    overridable: true,
                    default: [],
                    items: { kind: 'string' },
                    maxItems: 2,
                  },
                  {
                    name: 'maxConcurrentCalls',
                    labels: { fr: 'Appels simultanés', en: 'Concurrent calls' },
                    kind: 'integer',
                    overridable: true,
                    default: 4,
                    minimum: 1,
                    maximum: 10,
                  },
                  {
                    name: 'storageAlias',
                    labels: { fr: 'Alias de stockage', en: 'Storage alias' },
                    kind: 'string',
                    overridable: true,
                    default: 'cache',
                    minLength: 3,
                    maxLength: 8,
                    pattern: '^[a-z]+$',
                  },
                ],
              }),
            ),
          },
        },
        {
          provide: ResourcesService,
          useValue: {
            add: addResource,
            previewName: vi.fn(() => of({ items: [], issues: [] })),
          },
        },
      ],
    });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(ResourceCreate);
    fixture.componentRef.setInput('orgId', organizationId);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('type', 'KeyVault');
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('form')).not.toBeNull());
    fixture.detectChanges();

    const purgeProtection = host.querySelector<HTMLInputElement>(
      'input[aria-label="Protection contre la purge"]',
    );
    expect(purgeProtection).not.toBeNull();
    expect(purgeProtection?.checked).toBe(true);
    expect(host.querySelector('app-ds-cidr-field')).toBeNull();

    const exposure = host.querySelector<HTMLSelectElement>('select[aria-label="Exposition"]');
    expect(exposure).not.toBeNull();
    expect(exposure!.value).toBe(JSON.stringify('Public'));
    exposure!.value = JSON.stringify('Restricted');
    exposure!.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(host.querySelector('app-ds-cidr-field')).not.toBeNull();

    const componentWithCreate = fixture.componentInstance as unknown as {
      logicalName: { set: (value: string) => void };
      create: () => Promise<void>;
      propertyDefinitions: () => Array<{ name: string }>;
      updateInitialCidrValue: (definition: unknown, value: string[]) => void;
    };
    const allowedIpRanges = componentWithCreate
      .propertyDefinitions()
      .find((definition) => definition.name === 'allowedIpRanges');
    expect(allowedIpRanges).toBeDefined();
    componentWithCreate.updateInitialCidrValue(allowedIpRanges, ['203.0.113.0/24']);

    exposure!.value = JSON.stringify('Public');
    exposure!.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(host.querySelector('app-ds-cidr-field')).toBeNull();

    purgeProtection!.checked = false;
    purgeProtection!.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    componentWithCreate.logicalName.set('kv-recipe');

    const allowedNames = host.querySelector<HTMLTextAreaElement>(
      'textarea[aria-label="Noms autorisés"]',
    );
    expect(allowedNames).not.toBeNull();
    allowedNames!.value = '["incomplete"';
    allowedNames!.dispatchEvent(new Event('change'));
    await componentWithCreate.create();
    expect(addResource).not.toHaveBeenCalled();

    allowedNames!.value = '["first", "second", "third"]';
    allowedNames!.dispatchEvent(new Event('change'));
    await componentWithCreate.create();
    expect(addResource).not.toHaveBeenCalled();
    allowedNames!.value = '["first", 2]';
    allowedNames!.dispatchEvent(new Event('change'));
    await componentWithCreate.create();
    expect(addResource).not.toHaveBeenCalled();
    allowedNames!.value = '["first"]';
    allowedNames!.dispatchEvent(new Event('change'));

    const concurrentCalls = host.querySelector<HTMLInputElement>(
      'input[aria-label="Appels simultanés"]',
    );
    expect(concurrentCalls).not.toBeNull();
    expect(concurrentCalls!.getAttribute('min')).toBe('1');
    expect(concurrentCalls!.getAttribute('max')).toBe('10');
    const validityDescriptor = Object.getOwnPropertyDescriptor(
      HTMLInputElement.prototype,
      'validity',
    );
    if (validityDescriptor === undefined) {
      throw new Error('HTMLInputElement.validity must be defined on its prototype.');
    }
    Object.defineProperty(concurrentCalls, 'validity', {
      configurable: true,
      value: { badInput: true },
    });
    concurrentCalls!.value = '';
    concurrentCalls!.dispatchEvent(new Event('change'));
    await componentWithCreate.create();
    expect(addResource).not.toHaveBeenCalled();
    Object.defineProperty(concurrentCalls, 'validity', validityDescriptor);

    concurrentCalls!.value = '11';
    concurrentCalls!.dispatchEvent(new Event('change'));
    await componentWithCreate.create();
    expect(addResource).not.toHaveBeenCalled();
    concurrentCalls!.value = '3';
    concurrentCalls!.dispatchEvent(new Event('change'));
    concurrentCalls!.value = '';
    concurrentCalls!.dispatchEvent(new Event('change'));

    const storageAlias = host.querySelector<HTMLInputElement>(
      'input[aria-label="Alias de stockage"]',
    );
    expect(storageAlias).not.toBeNull();
    expect(storageAlias!.getAttribute('min')).toBeNull();
    expect(storageAlias!.getAttribute('max')).toBeNull();
    storageAlias!.value = 'UP';
    storageAlias!.dispatchEvent(new Event('change'));
    await componentWithCreate.create();
    expect(addResource).not.toHaveBeenCalled();
    storageAlias!.value = 'redis';
    storageAlias!.dispatchEvent(new Event('change'));

    await componentWithCreate.create();

    expect(addResource).toHaveBeenCalledWith(
      projectId,
      componentId,
      expect.objectContaining({
        properties: {
          purgeProtection: false,
          allowedNames: ['first'],
          storageAlias: 'redis',
        },
      }),
    );
    expect(addResourceCalls[0]?.[2].properties).not.toHaveProperty('maxConcurrentCalls');
  });

  it('shows the current version under saved and the request version under submitted', () => {
    TestBed.configureTestingModule({
      imports: [ResourceCreate],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr' },
          loader: ResourceCreateTranslations,
        }),
        { provide: ProjectsService, useValue: {} },
        { provide: ComponentsService, useValue: {} },
        { provide: CatalogService, useValue: {} },
        { provide: ResourcesService, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(ResourceCreate);
    const componentWithConflict = fixture.componentInstance as unknown as {
      conflict: { set: (value: boolean) => void };
      conflictVersions: { set: (value: { current: string; submitted: string }) => void };
    };
    componentWithConflict.conflict.set(true);
    componentWithConflict.conflictVersions.set({ current: '8', submitted: '7' });
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(
      [...host.querySelectorAll('.resource-create__conflict thead th')].map((cell) =>
        cell.textContent?.trim(),
      ),
    ).toEqual(['Version du composant', 'Enregistrée', 'Soumise']);
    expect(
      [...host.querySelectorAll('.resource-create__conflict tbody td')].map((cell) =>
        cell.textContent?.trim(),
      ),
    ).toEqual(['8', '7']);
  });
});
