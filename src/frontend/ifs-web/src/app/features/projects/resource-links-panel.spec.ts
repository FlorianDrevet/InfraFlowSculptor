import { TestBed } from '@angular/core/testing';
import { provideTransloco, TranslocoLoader, TranslocoService, type Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ComponentResponse,
  LinkPreviewResponse,
  ResourceLinkOptionsResponse,
  ResourceLinksResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { ResourceLinksPanel } from './resource-links-panel';

class ResourceLinksTranslations implements TranslocoLoader {
  getTranslation(language = 'fr'): Observable<Translation> {
    const isEnglish = language === 'en';
    return of({
      resource: {
        outgoingLinks: 'Liaisons sortantes',
        linksHelp: 'Configurez les liaisons pour {{name}}.',
        loadingLinks: 'Chargement des liaisons.',
        linkUses: 'utilise',
        deploymentDependency: 'Dépendance de déploiement',
        addLink: 'Ajouter une liaison',
        addLinkFrom: 'Ajouter une liaison depuis {{name}}',
        removeLink: 'Retirer',
        confirmRemoveLink: 'Retirer la liaison',
        linkRemovalSummary: 'Retirer la liaison vers {{name}} ?',
        linkRemovalBlockerWarning: isEnglish
          ? 'Blocking break: project generation will be rejected until repaired.'
          : 'Rupture bloquante : vous pouvez retirer cette liaison, mais la génération du projet sera refusée.',
        linkRemovalReviewRequired: isEnglish
          ? 'The model changed since this preview. The impact was refreshed; review it before confirming again.'
          : 'Le modèle a changé depuis cet aperçu. L’impact a été actualisé ; relisez-le avant de confirmer à nouveau.',
        noLinkRemovalImpacts: 'Aucun impact supplémentaire calculé.',
        linkImpactRequiredLinkBroken: isEnglish
          ? 'A required link is broken.'
          : 'Une liaison obligatoire est rompue.',
        linkTarget: 'Cible',
        linkType: 'Type de liaison',
        linkIdentity: 'Identité',
        linkRoles: 'Rôles attribués',
        previewLink: 'Prévisualiser',
        linkPreviewHeading: 'Ce que la liaison produira',
        linkPreviewHelp: 'Demandez un aperçu.',
        linkPreviewDependency: '{{environment}} : {{sourceComponent}} dépend de {{targetComponent}}',
        dataAccess: { Lecture: 'Lecture', LectureEcriture: 'Lecture et écriture', Schema: 'Schéma' },
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
const duplicateTargetResourceId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
const identityId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

describe('ResourceLinksPanel', () => {
  it('shows blocking consequences before allowing a required link to be removed', async () => {
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
      defaultIdentityResourceId: null,
      children: [],
    };
    const component = {
      id: componentId,
      projectId,
      version: 12,
      name: 'API',
      code: 'api',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const link = {
      accessScope: 'Resource',
      createsDeploymentDependency: true,
      dataAccessLevel: null,
      findings: [],
      id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
      identityKind: 'None',
      identityResourceId: null,
      isExternal: false,
      kind: 'Hosting',
      kindLabelEn: 'Hosting',
      kindLabelFr: 'Hébergement',
      origin: 'Explicit',
      originSource: null,
      roles: [],
      sourceComponentCode: 'api',
      sourceComponentId: componentId,
      sourceResourceId: resourceId,
      sourceResourceName: 'api',
      targetComponentCode: 'core',
      targetComponentId: componentId,
      targetResourceId,
      targetResourceName: 'main',
      version: 4,
    };
    const removeLink = vi.fn(() => of({ linkId: link.id, summary: '', effects: [], findings: [] }));
    const resourceService = {
      links: vi.fn(() => of({ outgoing: [link], incoming: [] })),
      linkOptions: vi.fn(() => of({ kinds: [] })),
      linkRemovalImpact: vi.fn(() => of({
        modelVersion: 12,
        severity: 'Blocker',
        items: [{
          code: 'IMPACT-LIA-OBLIGATOIRE-ROMPU',
          linkId: link.id,
          message: 'Message d’impact du serveur',
          resourceId,
          severity: 'Blocker',
        }],
      })),
      removeLink,
    };

    TestBed.configureTestingModule({
      imports: [ResourceLinksPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr', 'en'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceLinksTranslations,
        }),
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        { provide: ResourcesService, useValue: resourceService },
      ],
    });

    const fixture = TestBed.createComponent(ResourceLinksPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(resourceService.links).toHaveBeenCalledOnce());

    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('.resource-links__item button')).not.toBeNull());
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Retirer'))
      ?.click();
    fixture.detectChanges();
    await vi.waitFor(() => expect(resourceService.linkRemovalImpact).toHaveBeenCalledOnce());
    fixture.detectChanges();
    expect(host.textContent).toContain('la génération du projet sera refusée');
    expect(host.textContent).toContain('Une liaison obligatoire est rompue.');
    expect(host.textContent).not.toContain('Message d’impact du serveur');
    const transloco = TestBed.inject(TranslocoService);
    transloco.setActiveLang('en');
    await vi.waitFor(() => expect(host.textContent).toContain('A required link is broken.'));
    expect(host.textContent).toContain('Blocking break: project generation will be rejected until repaired.');
    transloco.setActiveLang('fr');
    await vi.waitFor(() => expect(host.textContent).toContain('Retirer la liaison'));

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Retirer la liaison'))
      ?.click();
    await vi.waitFor(() => expect(removeLink).toHaveBeenCalledOnce());
    expect(removeLink).toHaveBeenCalledWith(projectId, componentId, resourceId, link.id, {
      expectedModelVersion: 12,
      expectedLinkVersion: 4,
    });
  });

  it('refreshes the impact and link version after a stale removal is rejected', async () => {
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
      defaultIdentityResourceId: null,
      children: [],
    };
    const component = {
      id: componentId,
      projectId,
      version: 12,
      name: 'API',
      code: 'api',
      resources: [resource],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    const link = {
      accessScope: 'Resource',
      createsDeploymentDependency: true,
      dataAccessLevel: null,
      findings: [],
      id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
      identityKind: 'None',
      identityResourceId: null,
      isExternal: false,
      kind: 'Hosting',
      kindLabelEn: 'Hosting',
      kindLabelFr: 'Hébergement',
      origin: 'Explicit',
      originSource: null,
      roles: [],
      sourceComponentCode: 'api',
      sourceComponentId: componentId,
      sourceResourceId: resourceId,
      sourceResourceName: 'api',
      targetComponentCode: 'core',
      targetComponentId: componentId,
      targetResourceId,
      targetResourceName: 'main',
      version: 4,
    };
    const impact = (modelVersion: number) => ({
      modelVersion,
      severity: 'Blocker',
      items: [{
        code: 'IMPACT-LIA-OBLIGATOIRE-ROMPU',
        linkId: link.id,
        message: 'Une liaison obligatoire serait rompue.',
        resourceId,
        severity: 'Blocker',
      }],
    });
    const refreshedLink = { ...link, version: 5 };
    let currentLink = link;
    const removeLink = vi.fn()
      .mockImplementationOnce(() => {
        currentLink = refreshedLink;
        return throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'Le modèle a changé.'));
      })
      .mockReturnValueOnce(of({ linkId: link.id, summary: '', effects: [], findings: [] }));
    const resourceService = {
      links: vi.fn(() => of({ outgoing: [currentLink], incoming: [] })),
      linkOptions: vi.fn(() => of({ kinds: [] })),
      linkRemovalImpact: vi.fn()
        .mockReturnValueOnce(of(impact(12)))
        .mockReturnValueOnce(of(impact(13))),
      removeLink,
    };

    TestBed.configureTestingModule({
      imports: [ResourceLinksPanel],
      providers: [
        provideTransloco({
          config: { availableLangs: ['fr', 'en'], defaultLang: 'fr', reRenderOnLangChange: true },
          loader: ResourceLinksTranslations,
        }),
        { provide: ComponentsService, useValue: { get: vi.fn(() => of(component)) } },
        { provide: ResourcesService, useValue: resourceService },
      ],
    });

    const fixture = TestBed.createComponent(ResourceLinksPanel);
    fixture.componentRef.setInput('projectId', projectId);
    fixture.componentRef.setInput('component', component);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('canEdit', true);
    fixture.detectChanges();
    await vi.waitFor(() => expect(resourceService.links).toHaveBeenCalledOnce());

    const host = fixture.nativeElement as HTMLElement;
    await vi.waitFor(() => expect(host.querySelector('.resource-links__item button')).not.toBeNull());
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Retirer'))
      ?.click();
    fixture.detectChanges();
    await vi.waitFor(() => expect(resourceService.linkRemovalImpact).toHaveBeenCalledOnce());
    fixture.detectChanges();

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Retirer la liaison'))
      ?.click();
    await vi.waitFor(() => expect(removeLink).toHaveBeenCalledOnce());
    await vi.waitFor(() => expect(resourceService.linkRemovalImpact).toHaveBeenCalledTimes(2));
    fixture.detectChanges();
    expect(host.textContent).toContain('Le modèle a changé depuis cet aperçu.');

    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Retirer la liaison'))
      ?.click();
    await vi.waitFor(() => expect(removeLink).toHaveBeenCalledTimes(2));
    expect(removeLink).toHaveBeenNthCalledWith(2, projectId, componentId, resourceId, link.id, {
      expectedModelVersion: 13,
      expectedLinkVersion: 5,
    });
  });

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
      resources: [
        resource,
        { ...resource, id: identityId, type: 'UserAssignedIdentity', logicalName: 'id api' },
      ],
      resourceGroups: [],
    } as unknown as ComponentResponse;
    let componentVersion = 12;
    const componentGet = vi.fn(() => of({ ...component, version: componentVersion }));
    const links: ResourceLinksResponse = { outgoing: [], incoming: [] };
    const options: ResourceLinkOptionsResponse = {
      kinds: [
        {
          kind: 'Access',
          labelFr: 'Accès',
          labelEn: 'Access',
          fields: [{ name: 'roles', kind: 'array', required: true }],
          targets: [
            {
              componentCode: 'core',
              componentId,
              dataAccessLevels: [],
              identities: [
                {
                  kind: 'UserAssigned',
                  resourceId: identityId,
                  resourceName: 'id api',
                  isDefault: true,
                },
              ],
              isExisting: false,
              resourceId: targetResourceId,
              resourceName: 'log main',
              resourceType: 'LogAnalyticsWorkspace',
              roles: ['Log Analytics Reader'],
            },
            {
              componentCode: 'core',
              componentId,
              dataAccessLevels: [],
              identities: [
                {
                  kind: 'UserAssigned',
                  resourceId: identityId,
                  resourceName: 'id api',
                  isDefault: true,
                },
              ],
              isExisting: false,
              resourceId: duplicateTargetResourceId,
              resourceName: 'log main',
              resourceType: 'ApplicationInsights',
              roles: ['Log Analytics Reader'],
            },
          ],
        },
      ],
    };
    const preview: LinkPreviewResponse = {
      applicationSettings: [],
      dataAccess: [
        {
          databaseRoles: ['db_datareader'],
          environmentCode: 'dev',
          environmentId: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
          level: 'Lecture',
          sourceResourceId: resourceId,
          sourceResourceName: 'api',
          targetResourceId,
          targetResourceName: 'log main',
        },
      ],
      dependencies: [
        {
          environmentCode: 'dev',
          environmentId: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
          kind: 'Access',
          sourceComponentCode: 'api',
          sourceComponentId: componentId,
          targetComponentCode: 'core',
          targetComponentId: componentId,
        },
      ],
      findings: [],
      identities: [],
      roleAssignments: [
        {
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
        },
      ],
    };
    const addLink = vi.fn((...args: unknown[]) => {
      void args;
      componentVersion++;
      return of({
        linkId: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
        summary: '',
        effects: [],
        findings: [],
      });
    });
    const stalePreview = new Subject<LinkPreviewResponse>();
    const successfulPreview = new Subject<LinkPreviewResponse>();
    const previewResponses = [
      stalePreview,
      successfulPreview,
      of(preview),
      of(preview),
      of(preview),
      of(preview),
      of(preview),
    ];
    const previewLink = vi.fn((...args: unknown[]) => {
      void args;
      return previewResponses.shift()!;
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
        { provide: ComponentsService, useValue: { get: componentGet } },
        { provide: ResourcesService, useValue: resourceService },
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
    const clickPreview = (): void => {
      const button = buttons().find((candidate) => candidate.textContent?.includes('Prévisualiser'));
      expect(button).toBeDefined();
      button?.click();
      fixture.detectChanges();
    };
    buttons()
      .find((button) => button.textContent?.includes('Ajouter une liaison'))
      ?.click();
    fixture.detectChanges();
    const selectedKind = host.querySelector('.resource-links__kind-option[aria-pressed="true"]');
    expect(selectedKind?.textContent?.trim()).toBe('Accès');
    const targetSelect = host.querySelector<HTMLSelectElement>('.resource-links__form select')!;
    expect([...targetSelect.options].map((option) => option.textContent?.trim())).toEqual([
      'core / log main · LogAnalyticsWorkspace',
      'core / log main · ApplicationInsights',
    ]);
    const panel = fixture.componentInstance as unknown as {
      reload(): Promise<void>;
      previewResult(): LinkPreviewResponse | null;
    };
    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledOnce());
    targetSelect.value = duplicateTargetResourceId;
    targetSelect.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    stalePreview.next(preview);
    stalePreview.complete();
    await vi.waitFor(() => expect(panel.previewResult()).toBeNull());
    expect(panel.previewResult()).toBeNull();

    targetSelect.value = targetResourceId;
    targetSelect.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledTimes(2));
    successfulPreview.next(preview);
    successfulPreview.complete();
    await vi.waitFor(() => expect(panel.previewResult()).not.toBeNull());
    fixture.detectChanges();

    expect(addLink).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Log Analytics Reader');
    expect(host.textContent).toContain('dev');
    expect(host.textContent).toContain('dev : Lecture — db_datareader');
    expect(host.textContent).toContain('dev : api dépend de core');

    buttons()
      .filter((button) => button.textContent?.includes('Ajouter une liaison'))
      .at(-1)
      ?.click();
    await vi.waitFor(() => expect(addLink).toHaveBeenCalledOnce());
    expect(addLink.mock.calls[0]?.[3]).toMatchObject({
      expectedComponentVersion: 12,
      identityKind: 'UserAssigned',
      identityResourceId: identityId,
      roles: ['Log Analytics Reader'],
      targetResourceId,
    });

    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(2));
    const reopenedPanel = fixture.componentInstance as unknown as {
      openAdd(): void;
      addLink(): Promise<void>;
      previewResult(): LinkPreviewResponse | null;
      saving(): boolean;
    };
    await vi.waitFor(() => expect(reopenedPanel.saving()).toBe(false));
    reopenedPanel.openAdd();
    fixture.detectChanges();
    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledTimes(3));
    expect(reopenedPanel.previewResult()).not.toBeNull();
    await reopenedPanel.addLink();
    await vi.waitFor(() => expect(addLink).toHaveBeenCalledTimes(2));
    expect(addLink.mock.calls[1]?.[3]).toMatchObject({
      expectedComponentVersion: 13,
      identityKind: 'UserAssigned',
      identityResourceId: identityId,
      roles: ['Log Analytics Reader'],
      targetResourceId,
    });

    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(3));
    reopenedPanel.openAdd();
    fixture.detectChanges();
    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledTimes(4));
    expect(reopenedPanel.previewResult()).not.toBeNull();
    componentVersion = 14;
    addLink.mockImplementationOnce(() =>
      throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'Le composant a changé.')),
    );

    await reopenedPanel.addLink();

    expect(reopenedPanel.previewResult()).toBeNull();
    await vi.waitFor(() => expect(componentGet).toHaveBeenCalledTimes(4));
    fixture.componentRef.setInput('component', { ...component, version: 15 });
    fixture.detectChanges();
    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledTimes(5));
    expect(reopenedPanel.previewResult()).not.toBeNull();
    await reopenedPanel.addLink();
    await vi.waitFor(() => expect(addLink).toHaveBeenCalledTimes(4));
    expect(addLink.mock.calls[3]?.[3]).toMatchObject({ expectedComponentVersion: 15 });

    reopenedPanel.openAdd();
    fixture.detectChanges();
    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledTimes(6));
    expect(reopenedPanel.previewResult()).not.toBeNull();
    componentVersion = 17;
    await panel.reload();
    fixture.detectChanges();
    expect(reopenedPanel.previewResult()).toBeNull();

    clickPreview();
    await vi.waitFor(() => expect(previewLink).toHaveBeenCalledTimes(7));
    expect(reopenedPanel.previewResult()).not.toBeNull();
    fixture.componentRef.setInput('component', { ...component, version: 18 });
    fixture.detectChanges();
    expect(reopenedPanel.previewResult()).toBeNull();
  });
});
