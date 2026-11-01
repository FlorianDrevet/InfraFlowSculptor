import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ComponentResponse,
  ResourceLinksResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ResourceIdentityPanel } from './resource-identity-panel';

class ResourceIdentityTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        identityHeading: 'Identité et accès',
        identityHelp: 'Gérez les identités.',
        identitiesUsed: 'Identités utilisées',
        noIdentitiesUsed: 'Aucune identité utilisée.',
        systemIdentity: 'Identité système',
        loadingIdentity: 'Chargement…',
        identityNotUsed: 'Non utilisée.',
        defaultIdentity: 'Identité par défaut',
        defaultIdentityForSettings: 'Pour les paramètres',
        defaultIdentityCurrent: 'Actuellement : {{ name }}',
        identityLoadError: 'Échec du chargement.',
        identitySaveError: 'Échec de l’enregistrement.',
        retry: 'Réessayer',
        identityNone: 'Aucune',
        saveIdentity: 'Enregistrer',
        saving: 'Enregistrement…',
        rolesHeld: 'Rôles détenus',
        noRolesHeld: 'Aucun rôle détenu.',
        rolesGranted: 'Rôles accordés',
        noRolesGranted: 'Aucun rôle accordé.',
        explicitLinkOrigin: 'Origine explicite',
        implicitLinkOrigin: 'Origine implicite',
      },
    });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceId = '99999999-9999-4999-8999-999999999999';
const identityId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

describe('ResourceIdentityPanel', () => {
  it('saves the resource version loaded with the screen', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      type: 'ContainerApp',
      logicalName: 'api',
      description: '',
      tags: {},
      version: 3,
      isExisting: false,
      properties: {},
      overrides: {},
      presences: {},
      existingIds: {},
      forcedNames: {},
      defaultIdentityResourceId: null,
      children: [],
    };
    const identity: ResourceResponse = {
      ...resource,
      id: identityId,
      type: 'UserAssignedIdentity',
      logicalName: 'id api',
    };
    const component = {
      id: componentId,
      projectId,
      version: 12,
      name: 'API',
      code: 'api',
      resources: [resource, identity],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    let componentVersion = 12;
    let resourceVersion = 3;
    const componentGet = vi.fn(() =>
      of({
        ...component,
        version: componentVersion,
        resources: component.resources.map((current) =>
          current.id === resourceId ? { ...current, version: resourceVersion } : current,
        ),
      }),
    );
    const links: ResourceLinksResponse = { outgoing: [], incoming: [] };
    const setDefaultIdentity = vi.fn((...args: unknown[]) => {
      void args;
      componentVersion++;
      resourceVersion++;
      return of({});
    });

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: { links: vi.fn(() => of(links)), setDefaultIdentity },
        },
        { provide: ComponentsService, useValue: { get: componentGet } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    const panel = fixture.componentInstance as unknown as {
      loading(): boolean;
      saveDefaultIdentity(): Promise<void>;
      saving(): boolean;
    };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    expect(setDefaultIdentity).not.toHaveBeenCalled();

    const button = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (candidate) => candidate.textContent?.includes('Enregistrer'),
    );
    expect(button).toBeDefined();
    button!.click();

    await vi.waitFor(() => expect(setDefaultIdentity).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(panel.saving()).toBe(false));
    await panel.saveDefaultIdentity();
    await vi.waitFor(() => expect(setDefaultIdentity).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(3));
    expect(setDefaultIdentity.mock.calls.map((call) => call[3])).toEqual([
      { expectedVersion: 3, identityResourceId: null },
      { expectedVersion: 4, identityResourceId: null },
    ]);
  });

  it('keeps the current identity label tied to the saved value', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api');
    const identity = createResource(identityId, 'UserAssignedIdentity', 'id api');
    const component = createComponent([resource, identity]);

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: { links: vi.fn(() => of({ outgoing: [], incoming: [] })) },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as { loading(): boolean };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    const select = (fixture.nativeElement as HTMLElement).querySelector('select')!;
    select.value = identityId;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(
      'Actuellement : id api',
    );
  });

  it('shows the saved identity label after loading', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api', identityId);
    const identity = createResource(identityId, 'UserAssignedIdentity', 'id api');
    const component = createComponent([resource, identity]);

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: { links: vi.fn(() => of({ outgoing: [], incoming: [] })) },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as { loading(): boolean };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Actuellement : id api');
  });

  it('keeps the confirmed save visible when the follow-up load fails', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api');
    const identity = createResource(identityId, 'UserAssignedIdentity', 'id api');
    const component = createComponent([resource, identity]);
    const componentGet = vi
      .fn()
      .mockReturnValueOnce(of(component))
      .mockReturnValueOnce(throwError(() => new Error('offline')));

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: {
            links: vi.fn(() => of({ outgoing: [], incoming: [] })),
            setDefaultIdentity: vi.fn(() => of({})),
          },
        },
        { provide: ComponentsService, useValue: { get: componentGet } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      defaultIdentityId(): string;
      loadError(): boolean;
      loading(): boolean;
      saveDefaultIdentity(): Promise<void>;
      savedDefaultIdentityId(): string;
    };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    const select = (fixture.nativeElement as HTMLElement).querySelector('select')!;
    select.value = identityId;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await panel.saveDefaultIdentity();
    fixture.detectChanges();

    expect(panel.loadError()).toBe(true);
    expect(panel.savedDefaultIdentityId()).toBe(identityId);
    expect(panel.defaultIdentityId()).toBe(identityId);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Actuellement : id api');
  });

  it('preserves an unsaved identity choice when the same component reloads', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api');
    const identity = createResource(identityId, 'UserAssignedIdentity', 'id api');
    const component = createComponent([resource, identity]);
    const updatedComponent = { ...component, version: Number(component.version) + 1 };
    const componentGet = vi
      .fn()
      .mockReturnValueOnce(of(component))
      .mockReturnValueOnce(of(updatedComponent));

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: { links: vi.fn(() => of({ outgoing: [], incoming: [] })) },
        },
        { provide: ComponentsService, useValue: { get: componentGet } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      defaultIdentityId(): string;
      loading(): boolean;
    };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    const select = (fixture.nativeElement as HTMLElement).querySelector('select')!;
    select.value = identityId;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    fixture.componentRef.setInput('component', updatedComponent);
    fixture.detectChanges();
    await vi.waitFor(() => expect(panel.loading()).toBe(false));

    expect(panel.defaultIdentityId()).toBe(identityId);
    expect(componentGet).toHaveBeenCalledTimes(2);
  });

  it('clears a save error when the selected resource changes', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api');
    const otherResourceId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
    const otherResource = createResource(otherResourceId, 'ContainerApp', 'worker');
    const identity = createResource(identityId, 'UserAssignedIdentity', 'id api');
    const component = createComponent([resource, otherResource, identity]);
    const setDefaultIdentity = vi.fn(() => throwError(() => new Error('conflict')));

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: {
            links: vi.fn(() => of({ outgoing: [], incoming: [] })),
            setDefaultIdentity,
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      loading(): boolean;
      saveDefaultIdentity(): Promise<void>;
      saveError(): boolean;
    };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    await panel.saveDefaultIdentity();
    expect(panel.saveError()).toBe(true);

    fixture.componentRef.setInput('resource', otherResource);
    fixture.detectChanges();
    await vi.waitFor(() => expect(panel.loading()).toBe(false));

    expect(panel.saveError()).toBe(false);
    expect(setDefaultIdentity).toHaveBeenCalledOnce();
  });

  it('does not block another resource while a save is pending', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api');
    const otherResourceId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
    const otherResource = createResource(otherResourceId, 'ContainerApp', 'worker');
    const component = createComponent([resource, otherResource]);
    const firstSaveResponse = new Subject<object>();
    const setDefaultIdentity = vi
      .fn()
      .mockReturnValueOnce(firstSaveResponse.asObservable())
      .mockReturnValueOnce(of({}));

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: {
            links: vi.fn(() => of({ outgoing: [], incoming: [] })),
            setDefaultIdentity,
          },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      loading(): boolean;
      saveDefaultIdentity(): Promise<void>;
      saving(): boolean;
    };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    const pendingSave = panel.saveDefaultIdentity();
    await vi.waitFor(() => expect(setDefaultIdentity).toHaveBeenCalledOnce());
    expect(panel.saving()).toBe(true);

    fixture.componentRef.setInput('resource', otherResource);
    fixture.detectChanges();
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    fixture.detectChanges();

    expect(panel.saving()).toBe(false);
    const saveButton = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    expect(saveButton.disabled).toBe(false);
    await panel.saveDefaultIdentity();
    expect(setDefaultIdentity).toHaveBeenCalledTimes(2);

    firstSaveResponse.next({});
    firstSaveResponse.complete();
    await pendingSave;
    expect(panel.saving()).toBe(false);
  });

  it('disables identity changes while loading and after a load failure', async () => {
    const resource = createResource(resourceId, 'ContainerApp', 'api');
    const component = createComponent([
      resource,
      createResource(identityId, 'UserAssignedIdentity', 'id api'),
    ]);
    const componentResponse = new Subject<ComponentResponse>();
    const retryComponentResponse = new Subject<ComponentResponse>();
    const linksResponse = new Subject<ResourceLinksResponse>();
    const retryLinksResponse = new Subject<ResourceLinksResponse>();
    const componentGet = vi
      .fn()
      .mockReturnValueOnce(componentResponse.asObservable())
      .mockReturnValueOnce(retryComponentResponse.asObservable());
    const links = vi
      .fn()
      .mockReturnValueOnce(linksResponse.asObservable())
      .mockReturnValueOnce(retryLinksResponse.asObservable());
    const setDefaultIdentity = vi.fn(() => of({}));

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        { provide: ResourcesService, useValue: { links, setDefaultIdentity } },
        { provide: ComponentsService, useValue: { get: componentGet } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      loadError(): boolean;
      loading(): boolean;
      saveDefaultIdentity(): Promise<void>;
    };
    expect(panel.loading()).toBe(true);
    const select = (fixture.nativeElement as HTMLElement).querySelector('select')!;
    const saveButton = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    expect(select.disabled).toBe(true);
    expect(saveButton.disabled).toBe(true);

    componentResponse.error(new Error('component unavailable'));
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    expect(panel.loadError()).toBe(true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Échec du chargement.');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(
      'Aucune identité utilisée.',
    );
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Aucun rôle détenu.');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Aucun rôle accordé.');
    expect(select.disabled).toBe(true);
    expect(saveButton.disabled).toBe(true);

    await panel.saveDefaultIdentity();
    expect(setDefaultIdentity).not.toHaveBeenCalled();

    const retryButton = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (button) => button.textContent?.includes('Réessayer'),
    );
    expect(retryButton).toBeDefined();
    retryButton!.click();
    fixture.detectChanges();
    expect(componentGet).toHaveBeenCalledTimes(2);

    retryComponentResponse.next(component);
    retryComponentResponse.complete();
    retryLinksResponse.next({ outgoing: [], incoming: [] });
    retryLinksResponse.complete();
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    expect(panel.loadError()).toBe(false);
    fixture.detectChanges();
    expect(select.disabled).toBe(false);
    expect(saveButton.disabled).toBe(false);
  });

  it('reloads when the resource changes and ignores the previous response', async () => {
    const secondResourceId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
    const secondIdentityId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
    const firstResource = createResource(resourceId, 'ContainerApp', 'api', identityId);
    const firstIdentity = createResource(identityId, 'UserAssignedIdentity', 'id api');
    const secondResource = createResource(
      secondResourceId,
      'ContainerApp',
      'worker',
      secondIdentityId,
    );
    const secondIdentity = createResource(secondIdentityId, 'UserAssignedIdentity', 'id worker');
    const firstComponent = createComponent([firstResource, firstIdentity]);
    const secondComponent = createComponent([secondResource, secondIdentity]);
    const firstComponentResponse = new Subject<ComponentResponse>();
    const secondComponentResponse = new Subject<ComponentResponse>();
    const firstLinksResponse = new Subject<ResourceLinksResponse>();
    const secondLinksResponse = new Subject<ResourceLinksResponse>();
    const componentGet = vi
      .fn()
      .mockReturnValueOnce(firstComponentResponse.asObservable())
      .mockReturnValueOnce(secondComponentResponse.asObservable());
    const links = vi
      .fn()
      .mockReturnValueOnce(firstLinksResponse.asObservable())
      .mockReturnValueOnce(secondLinksResponse.asObservable());

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        { provide: ResourcesService, useValue: { links, setDefaultIdentity: vi.fn(() => of({})) } },
        { provide: ComponentsService, useValue: { get: componentGet } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', firstComponent);
    fixture.componentRef.setInput('resource', firstResource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    expect(componentGet).toHaveBeenCalledTimes(1);

    fixture.componentRef.setInput('component', secondComponent);
    fixture.componentRef.setInput('resource', secondResource);
    fixture.detectChanges();
    expect(componentGet).toHaveBeenCalledTimes(2);

    secondComponentResponse.next(secondComponent);
    secondComponentResponse.complete();
    secondLinksResponse.next({ outgoing: [], incoming: [] });
    secondLinksResponse.complete();
    const panel = fixture.componentInstance as unknown as {
      defaultIdentityId(): string;
      loading(): boolean;
    };
    await vi.waitFor(() => expect(panel.loading()).toBe(false));
    expect(panel.defaultIdentityId()).toBe(secondIdentityId);

    firstComponentResponse.next(firstComponent);
    firstComponentResponse.complete();
    firstLinksResponse.next({ outgoing: [], incoming: [] });
    firstLinksResponse.complete();
    await vi.waitFor(() => expect(panel.defaultIdentityId()).toBe(secondIdentityId));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('select')!.value).toBe(
      secondIdentityId,
    );
  });

  it('clears a default identity that is no longer in the component', async () => {
    const resourceWithDeletedIdentity = createResource(
      resourceId,
      'ContainerApp',
      'api',
      identityId,
    );
    const component = createComponent([resourceWithDeletedIdentity]);
    const setDefaultIdentity = vi.fn(() => of({}));

    TestBed.configureTestingModule({
      imports: [ResourceIdentityPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceIdentityTranslations,
        }),
        {
          provide: ResourcesService,
          useValue: { links: vi.fn(() => of({ outgoing: [], incoming: [] })), setDefaultIdentity },
        },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceIdentityPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resourceWithDeletedIdentity);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();

    const panel = fixture.componentInstance as unknown as {
      defaultIdentityId(): string;
      saveDefaultIdentity(): Promise<void>;
    };
    await vi.waitFor(() => expect(panel.defaultIdentityId()).toBe(''));
    await panel.saveDefaultIdentity();

    expect(setDefaultIdentity).toHaveBeenCalledWith(projectId, componentId, resourceId, {
      expectedVersion: resourceWithDeletedIdentity.version,
      identityResourceId: null,
    });
  });
});

function createResource(
  id: string,
  type: string,
  logicalName: string,
  defaultIdentityResourceId: string | null = null,
): ResourceResponse {
  return {
    id,
    resourceGroupId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
    type,
    logicalName,
    description: '',
    tags: {},
    version: 3,
    isExisting: false,
    properties: {},
    overrides: {},
    presences: {},
    existingIds: {},
    forcedNames: {},
    defaultIdentityResourceId,
    children: [],
  };
}

function createComponent(resources: ResourceResponse[]): ComponentResponse {
  return {
    id: componentId,
    projectId,
    version: 12,
    name: 'API',
    code: 'api',
    resources,
    resourceGroups: [],
  } as unknown as ComponentResponse;
}
