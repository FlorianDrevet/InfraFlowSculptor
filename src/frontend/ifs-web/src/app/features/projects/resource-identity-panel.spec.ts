import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
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
        identityNone: 'Aucune',
        saveIdentity: 'Enregistrer',
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
  it('saves the component version loaded with the screen', async () => {
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
    const componentGet = vi.fn(() => of({ ...component, version: componentVersion }));
    const links: ResourceLinksResponse = { outgoing: [], incoming: [] };
    const setDefaultIdentity = vi.fn((...args: unknown[]) => {
      void args;
      componentVersion++;
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
    await vi.waitFor(() => expect(setDefaultIdentity).not.toHaveBeenCalled());

    const button = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (candidate) => candidate.textContent?.includes('Enregistrer'),
    );
    expect(button).toBeDefined();
    button!.click();

    await vi.waitFor(() => expect(setDefaultIdentity).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(2));
    const panel = fixture.componentInstance as unknown as {
      saveDefaultIdentity(): Promise<void>;
      saving(): boolean;
    };
    await vi.waitFor(() => expect(panel.saving()).toBe(false));
    await panel.saveDefaultIdentity();
    await vi.waitFor(() => expect(setDefaultIdentity).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(3));
    expect(setDefaultIdentity.mock.calls.map((call) => call[3])).toEqual([
      { expectedVersion: 12, identityResourceId: null },
      { expectedVersion: 13, identityResourceId: null },
    ]);
  });
});
