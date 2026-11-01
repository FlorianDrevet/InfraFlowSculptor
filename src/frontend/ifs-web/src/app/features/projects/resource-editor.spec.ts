import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
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
import { ResourceEditor } from './resource-editor';

class ResourceEditorTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        links: 'Liaisons',
        saveError: 'La modification a échoué.',
        irreversiblePropertyError:
          'Cette modification annulerait un changement irréversible. La valeur enregistrée reste inchangée.',
        refreshError: 'Impossible d’actualiser la ressource. Rechargez la page avant de continuer.',
      },
    });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const secondProjectId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceId = '99999999-9999-4999-8999-999999999999';
const secondResourceId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

describe('ResourceEditor', () => {
  it('shows the links tab for a modeled resource', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
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
    const secondResource: ResourceResponse = {
      ...resource,
      id: secondResourceId,
      logicalName: 'worker',
    };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'API',
      code: 'api',
      resources: [resource, secondResource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const getComponent = vi.fn(() => of(component));
    const getEffectiveValues = vi.fn(() => of([]));
    const getPermissions = vi.fn((requestedProjectId: string) =>
      of({ permissions: requestedProjectId === projectId ? ['model.edit'] : [] }),
    );
    const setProperties = vi.fn(() => of(undefined));

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            myPermissions: getPermissions,
          },
        },
        { provide: ComponentsService, useValue: { get: getComponent } },
        {
          provide: CatalogService,
          useValue: { type: vi.fn(() => of({ properties: [] })), types: vi.fn(() => of([])) },
        },
        {
          provide: ResourcesService,
          useValue: {
            effectiveValues: getEffectiveValues,
            previewName: vi.fn(() => of({ items: [], issues: [] })),
            setProperties,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.componentRef.setInput('tab', 'links');
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('nav')).not.toBeNull());
    fixture.detectChanges();
    expect(fixture.componentInstance['canEditModel']()).toBe(true);

    const linksTab = host.querySelector('a[aria-current="page"]');
    expect(linksTab?.textContent?.trim()).toBe('Liaisons');

    fixture.componentRef.setInput('projectId', secondProjectId);
    fixture.componentRef.setInput('resourceId', secondResourceId);
    fixture.detectChanges();
    expect(fixture.componentInstance['canEditModel']()).toBe(false);
    await fixture.whenStable();
    await vi.waitFor(() =>
      expect(fixture.componentInstance['resource']()?.id).toBe(secondResourceId),
    );
    fixture.detectChanges();
    expect(getEffectiveValues).toHaveBeenLastCalledWith(secondProjectId, secondResourceId);
    expect(getPermissions).toHaveBeenLastCalledWith(secondProjectId);
    expect(fixture.componentInstance['resource']()?.id).toBe(secondResourceId);
    await fixture.componentInstance['updateProperty']({
      property: 'allowedIpRanges',
      targetId: null,
      value: ['10.0.0.0/8'],
    });
    expect(setProperties).not.toHaveBeenCalled();
    expect(host.querySelector('h2')?.textContent?.trim()).toBe('worker');
  });

  it('ignores an old resource conflict after navigating to another resource', async () => {
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
    const secondResource: ResourceResponse = {
      ...resource,
      id: secondResourceId,
      logicalName: 'worker',
    };
    const component = {
      id: componentId,
      projectId,
      version: 1,
      name: 'API',
      code: 'api',
      resources: [resource, secondResource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const pendingUpdate = new Subject<void>();
    const setProperties = vi.fn(() => pendingUpdate.asObservable());

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: { type: vi.fn(() => of({ properties: [] })), types: vi.fn(() => of([])) },
        },
        {
          provide: ResourcesService,
          useValue: {
            effectiveValues: vi.fn(() => of([])),
            previewName: vi.fn(() => of({ items: [], issues: [] })),
            setProperties,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance['resource']()?.id).toBe(resourceId));

    const editor = fixture.componentInstance as unknown as {
      actionError: () => boolean;
      conflict: () => unknown;
      pendingConflictAction: () => unknown;
      resource: () => ResourceResponse | null;
      saving: () => boolean;
      updateProperty: (update: {
        property: string;
        targetId: string | null;
        value: string[];
      }) => Promise<void>;
    };
    const update = editor.updateProperty({
      property: 'allowedIpRanges',
      targetId: null,
      value: ['10.0.0.0/8'],
    });
    await vi.waitFor(() => expect(setProperties).toHaveBeenCalledOnce());

    fixture.componentRef.setInput('resourceId', secondResourceId);
    fixture.detectChanges();
    await vi.waitFor(() => expect(editor.resource()?.id).toBe(secondResourceId));
    expect(editor.saving()).toBe(false);

    pendingUpdate.error(new ApiError(409, 'CONFLICT', 'The resource changed.'));
    await update;
    fixture.detectChanges();

    expect(editor.resource()?.id).toBe(secondResourceId);
    expect(editor.actionError()).toBe(false);
    expect(editor.conflict()).toBeNull();
    expect(editor.pendingConflictAction()).toBeNull();
    expect(editor.saving()).toBe(false);
  });

  it('keeps the editor mounted and explains when refreshing after a property update fails', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
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
      name: 'API',
      code: 'api',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const refreshProject = new Subject<ProjectSummaryResponse>();
    const getProject = vi.fn().mockReturnValueOnce(of(project)).mockReturnValueOnce(refreshProject);
    const updateMetadata = vi.fn(() => of(undefined));
    const setProperties = vi.fn(() => of(undefined));

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: getProject,
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: { type: vi.fn(() => of({ properties: [] })), types: vi.fn(() => of([])) },
        },
        {
          provide: ResourcesService,
          useValue: {
            effectiveValues: vi.fn(() => of([])),
            previewName: vi.fn(() => of({ items: [], issues: [] })),
            updateMetadata,
            setProperties,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('nav')).not.toBeNull());

    const componentWithUpdate = fixture.componentInstance as unknown as {
      updateProperty: (update: {
        property: string;
        targetId: string | null;
        value: string[];
      }) => Promise<void>;
      saveMetadata: () => Promise<void>;
      canEditModel: () => boolean;
      syncRevision: () => number;
    };
    const update = componentWithUpdate.updateProperty({
      property: 'allowedIpRanges',
      targetId: null,
      value: ['10.1.0.0/16'],
    });
    await vi.waitFor(() => expect(getProject).toHaveBeenCalledTimes(2));
    fixture.detectChanges();

    expect(host.querySelector('nav')).not.toBeNull();
    expect(host.textContent).not.toContain('resource.loadingResource');

    refreshProject.error(new Error('Refresh unavailable.'));
    await update;
    fixture.detectChanges();

    expect(host.querySelector('nav')).not.toBeNull();
    expect(host.textContent).toContain(
      'Impossible d’actualiser la ressource. Rechargez la page avant de continuer.',
    );
    expect(host.querySelector('[role="alert"]')?.textContent).toContain(
      'Impossible d’actualiser la ressource. Rechargez la page avant de continuer.',
    );
    expect(componentWithUpdate.canEditModel()).toBe(false);
    expect(
      (host.querySelector('input[name="resourceDescription"]') as HTMLInputElement).disabled,
    ).toBe(true);
    expect(
      (host.querySelector('textarea[name="resourceTags"]') as HTMLTextAreaElement).disabled,
    ).toBe(true);
    expect(componentWithUpdate.syncRevision()).toBe(0);
    const successfulPropertyUpdateCount = setProperties.mock.calls.length;

    await componentWithUpdate.updateProperty({
      property: 'memoryGi',
      targetId: null,
      value: ['2Gi'],
    });
    await componentWithUpdate.saveMetadata();
    expect(componentWithUpdate.syncRevision()).toBe(2);
    expect(setProperties).toHaveBeenCalledTimes(successfulPropertyUpdateCount);
    expect(updateMetadata).not.toHaveBeenCalled();
  });

  it('restores presence switches after the update is refused', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
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
      name: 'API',
      code: 'api',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const target = {
      code: 'dev',
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      isPresent: true,
      properties: [],
      region: 'francecentral',
      tags: {},
    };
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const setPresence = vi.fn(() =>
      throwError(() => new ApiError(400, 'PRESENCE_REFUSED', 'The update was refused.')),
    );

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: { type: vi.fn(() => of({ properties: [] })), types: vi.fn(() => of([])) },
        },
        {
          provide: ResourcesService,
          useValue: {
            effectiveValues: vi.fn(() => of([target])),
            previewName: vi.fn(() => of({ items: [], issues: [] })),
            setPresence,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.componentRef.setInput('tab', 'general');
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance['resource']()?.id).toBe(resourceId));
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const presenceSwitch = host.querySelector('input[role="switch"]') as HTMLInputElement;
    expect((host.querySelector('input[role="switch"]') as HTMLInputElement).checked).toBe(true);
    presenceSwitch.checked = false;
    presenceSwitch.dispatchEvent(new Event('change'));
    await vi.waitFor(() => expect(setPresence).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['saving']()).toBe(false));
    fixture.detectChanges();

    expect(fixture.componentInstance['currentPresence'](target)).toBe(true);
    expect((host.querySelector('input[role="switch"]') as HTMLInputElement).checked).toBe(true);
    fixture.destroy();
  });

  it('resynchronizes network enum selections after a refused update', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'SqlServer',
      logicalName: 'orders',
      description: '',
      tags: {},
      version: 1,
      isExisting: false,
      properties: { exposure: 'Restricted' },
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
      name: 'Data',
      code: 'data',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const target = {
      code: 'dev',
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      isPresent: true,
      properties: [{ name: 'exposure', value: 'Restricted', provenance: 'resource' }],
      region: 'francecentral',
      tags: {},
    };
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const setProperties = vi.fn(() =>
      throwError(() => new ApiError(400, 'PROPERTY_REFUSED', 'The update was refused.')),
    );

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: {
            type: vi.fn(() =>
              of({
                properties: [
                  {
                    name: 'exposure',
                    kind: 'enum',
                    overridable: true,
                    values: ['Public', 'Restricted'],
                  },
                ],
                exposure: { modes: ['Public', 'Restricted'] },
              }),
            ),
            types: vi.fn(() => of([])),
          },
        },
        {
          provide: ResourcesService,
          useValue: {
            effectiveValues: vi.fn(() => of([target])),
            previewName: vi.fn(() => of({ items: [], issues: [] })),
            setProperties,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.componentRef.setInput('tab', 'network');
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance['resource']()?.id).toBe(resourceId));
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const select = host.querySelector('app-resource-network-panel select') as HTMLSelectElement;
    expect(
      (
        host.querySelector('app-resource-network-panel select') as HTMLSelectElement
      ).selectedOptions[0]?.textContent?.trim(),
    ).toBe('Restricted');
    select.value = `value:${JSON.stringify('Public')}`;
    select.dispatchEvent(new Event('change'));
    await vi.waitFor(() => expect(setProperties).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(fixture.componentInstance['saving']()).toBe(false));
    fixture.detectChanges();

    expect(fixture.componentInstance['syncRevision']()).toBe(1);
    expect(
      (
        host.querySelector('app-resource-network-panel select') as HTMLSelectElement
      ).selectedOptions[0]?.textContent?.trim(),
    ).toBe('Restricted');
    fixture.destroy();
  });

  it('explains when an irreversible property change is refused', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      type: 'KeyVault',
      logicalName: 'main',
      description: '',
      tags: {},
      version: 1,
      isExisting: false,
      properties: { purgeProtection: true },
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
      name: 'Core',
      code: 'core',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const project = { catalogVersion: '2026.10-pilot.2' } as ProjectSummaryResponse;
    const setProperties = vi.fn(() =>
      throwError(() => new ApiError(400, 'RESOURCE_PROPERTY_IRREVERSIBLE', 'Irreversible.')),
    );

    TestBed.configureTestingModule({
      imports: [ResourceEditor],
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceEditorTranslations,
        }),
        {
          provide: ProjectsService,
          useValue: {
            get: vi.fn(() => of(project)),
            myPermissions: vi.fn(() => of({ permissions: ['model.edit'] })),
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        {
          provide: CatalogService,
          useValue: {
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
                ],
              }),
            ),
            types: vi.fn(() => of([])),
          },
        },
        {
          provide: ResourcesService,
          useValue: {
            effectiveValues: vi.fn(() => of([])),
            previewName: vi.fn(() => of({ items: [], issues: [] })),
            setProperties,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(ResourceEditor);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('componentId', componentId);
    fixture.componentRef.setInput('resourceId', resourceId);
    fixture.detectChanges();
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('nav')).not.toBeNull());

    const componentWithUpdate = fixture.componentInstance as unknown as {
      updateProperty: (update: {
        property: string;
        targetId: string | null;
        value: boolean;
      }) => Promise<void>;
    };
    await componentWithUpdate.updateProperty({
      property: 'purgeProtection',
      targetId: null,
      value: false,
    });
    fixture.detectChanges();

    expect(setProperties).toHaveBeenCalledOnce();
    expect(host.textContent).toContain('Cette modification annulerait un changement irréversible.');
    expect(host.textContent).not.toContain('La modification a échoué.');
  });
});
