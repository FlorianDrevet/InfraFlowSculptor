import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, type Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ComponentResponse,
  LinkPreviewResponse,
  ResourceLinkOptionsResponse,
  ResourceLinksResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ResourceLinksPanel } from './resource-links-panel';

class ResourceLinksTranslations implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      resource: {
        addLink: 'Ajouter une liaison',
        addLinkFrom: 'Ajouter une liaison depuis {{name}}',
        linkTarget: 'Cible',
        linkType: 'Type de liaison',
        linkIdentity: 'Identité',
        linkRoles: 'Rôles attribués',
        previewLink: 'Prévisualiser',
        linkPreviewHeading: 'Ce que la liaison produira',
        linkPreviewHelp: 'Demandez un aperçu.',
        cancel: 'Annuler',
        explicitLinkOrigin: 'Origine explicite',
        identityKind: { UserAssigned: 'Identité affectée : {{name}}' },
      },
    });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const componentId = '88888888-8888-4888-8888-888888888888';
const resourceId = '99999999-9999-4999-8999-999999999999';
const targetResourceId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const identityId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

describe('ResourceLinksPanel', () => {
  it('previews server effects before creating a link', async () => {
    const resource: ResourceResponse = {
      id: resourceId,
      resourceGroupId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
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
      defaultIdentityResourceId: identityId,
      children: [],
    };
    const component = {
      id: componentId,
      projectId,
      version: 12,
      name: 'API',
      code: 'api',
      resources: [resource, { ...resource, id: identityId, type: 'UserAssignedIdentity', logicalName: 'id api' }],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const links: ResourceLinksResponse = { outgoing: [], incoming: [] };
    const options: ResourceLinkOptionsResponse = {
      kinds: [{
        kind: 'Access',
        labelFr: 'Accès',
        labelEn: 'Access',
        fields: [{ name: 'roles', kind: 'array', required: true }],
        targets: [{
          componentCode: 'core',
          componentId,
          dataAccessLevels: [],
          identities: [{ kind: 'UserAssigned', resourceId: identityId, resourceName: 'id api', isDefault: true }],
          isExisting: false,
          resourceId: targetResourceId,
          resourceName: 'log main',
          resourceType: 'LogAnalyticsWorkspace',
          roles: ['Log Analytics Reader'],
        }],
      }],
    };
    const preview: LinkPreviewResponse = {
      applicationSettings: [],
      dataAccess: [],
      dependencies: [],
      findings: [],
      identities: [],
      roleAssignments: [{
        environmentCode: 'dev',
        environmentId: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
        isReused: false,
        principalResourceId: identityId,
        principalResourceName: 'id api',
        roleName: 'Log Analytics Reader',
        scope: 'Resource',
        scopeResourceId: targetResourceId,
        scopeResourceName: 'log main',
        targetResourceId,
        targetResourceName: 'log main',
      }],
    };
    const addLink = vi.fn((...args: unknown[]) => {
      void args;
      return of({ linkId: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', summary: '', effects: [], findings: [] });
    });
    const previewLink = vi.fn((...args: unknown[]) => {
      void args;
      return of(preview);
    });
    const resourceService = {
      links: vi.fn(() => of(links)),
      linkOptions: vi.fn(() => of(options)),
      previewLink,
      addLink,
    };

    TestBed.configureTestingModule({
      imports: [ResourceLinksPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceLinksTranslations,
        }),
        { provide: ResourcesService, useValue: resourceService },
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
      ],
    });

    const fixture = TestBed.createComponent(ResourceLinksPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(resourceService.linkOptions).toHaveBeenCalledOnce());

    const host = fixture.nativeElement as HTMLElement;
    const buttons = (): HTMLButtonElement[] => [...host.querySelectorAll('button')];
    buttons().find((button) => button.textContent?.includes('Ajouter une liaison'))?.click();
    fixture.detectChanges();
    const selectedKind = host.querySelector('.resource-links__kind-option[aria-pressed="true"]');
    expect(selectedKind?.textContent?.trim()).toBe('Accès');
    buttons().find((button) => button.textContent?.includes('Prévisualiser'))?.click();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledOnce());
    fixture.detectChanges();

    expect(addLink).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Log Analytics Reader');
    expect(host.textContent).toContain('dev');

    buttons().filter((button) => button.textContent?.includes('Ajouter une liaison')).at(-1)?.click();
    await vi.waitFor(() => expect(addLink).toHaveBeenCalledOnce());
    expect(addLink.mock.calls[0]?.[3]).toMatchObject({
      expectedComponentVersion: 12,
      identityKind: 'UserAssigned',
      identityResourceId: identityId,
      roles: ['Log Analytics Reader'],
      targetResourceId,
    });
  });
});
