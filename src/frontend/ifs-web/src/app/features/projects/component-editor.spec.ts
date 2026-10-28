import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ComponentResponse,
  ProjectSummaryResponse,
  SaveComponentRequest,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ComponentEditor } from './component-editor';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      component: {
        loading: 'Chargement…',
        loadError: 'Le composant n’a pas pu être chargé.',
        newTitle: 'Nouveau composant',
        mode: 'Mode de déploiement',
        modePerEnvironment: 'Par environnement',
        modeSingle: 'Unique',
        targetCode: 'Code de la cible',
        targetHint: 'Remplace {env} dans les noms.',
        subscription: 'Abonnement',
        region: 'Région',
        deploymentConnection: 'Connexion de déploiement',
        protectedTarget: 'Cible protégée',
        approvers: 'Approbateurs',
        runners: 'Exécuteurs',
        save: 'Enregistrer',
        saving: 'Enregistrement…',
        saveError: 'Le composant n’a pas pu être enregistré.',
        versionConflict: 'Le composant a changé.',
        cancel: 'Annuler',
        backToComponents: 'Composants',
        lifecycle: 'Cycle de vie',
        removedResources: 'Ressources retirées du modèle',
        detach: 'Détacher',
        deleteOutsideProtected: 'Supprimer (hors cibles protégées)',
        deletionProtection: 'Protection contre la suppression dans les cibles protégées',
        tags: 'Tags',
        tagsHint: 'Une paire clé=valeur par ligne.',
        identity: 'Identité',
        name: 'Nom',
        code: 'Code',
        description: 'Description',
        allEnvironments: 'Tous les environnements',
        targets: 'Environnements ciblés',
        singleTarget: 'Cible propre',
      },
      project: { permissionRequired: 'Permission requise.' },
    });
  }
}

describe('ComponentEditor', () => {
  it('creates a Single component with the shared target defaults', async () => {
    const orgId = '33333333-3333-4333-8333-333333333333';
    const projectId = '77777777-7777-4777-8777-777777777777';
    const subscriptionId = '11111111-1111-4111-8111-111111111111';
    const project = {
      id: projectId,
      version: 7,
      code: 'shop',
      defaultRunners: ['hosted-linux'],
    } as ProjectSummaryResponse;
    const environment = {
      id: '88888888-8888-4888-8888-888888888888',
      name: 'Production',
      code: 'prd',
      subscriptionId,
      region: 'francecentral',
      approvers: ['alice@contoso.example'],
      runners: [],
    };
    const created = { id: '99999999-9999-4999-8999-999999999999' } as ComponentResponse;
    const componentService = {
      create: vi.fn().mockReturnValue(of(created)),
      get: vi.fn(),
    };
    const projectService = {
      get: vi.fn().mockReturnValue(of(project)),
      environments: vi.fn().mockReturnValue(of([environment])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['components.manage'] })),
    };

    await TestBed.configureTestingModule({
      imports: [ComponentEditor],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/components/:componentId/settings',
              component: ComponentEditor,
            },
          ],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: projectService },
        { provide: ComponentsService, useValue: componentService },
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

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(
      `/o/${orgId}/projects/${projectId}/components/new/settings`,
      ComponentEditor,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    const inputs = harness.routeNativeElement?.querySelectorAll<HTMLInputElement>('input') ?? [];
    const componentName = [...inputs].find((input) => input.name === 'componentName');
    const componentCode = [...inputs].find((input) => input.name === 'componentCode');
    expect(componentName).toBeDefined();
    expect(componentCode).toBeDefined();
    componentName!.value = 'Platform';
    componentName!.dispatchEvent(new Event('input', { bubbles: true }));
    componentCode!.value = 'platform';
    componentCode!.dispatchEvent(new Event('input', { bubbles: true }));
    const singleMode = [...(harness.routeNativeElement?.querySelectorAll('button') ?? [])].find(
      (button) => button.textContent?.trim() === 'Unique',
    );
    singleMode?.click();
    harness.fixture.detectChanges();

    const targetCode = harness.routeNativeElement?.querySelector<HTMLInputElement>(
      'input[name="targetCode"]',
    );
    const targetApprovers = harness.routeNativeElement?.querySelector<HTMLInputElement>(
      'input[name="targetApprovers"]',
    );
    expect(targetCode?.value).toBe('shared');
    expect(targetApprovers?.value).toBe('alice@contoso.example');

    harness.routeNativeElement
      ?.querySelector('form')
      ?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await harness.fixture.whenStable();

    expect(componentService.create).toHaveBeenCalledWith(
      projectId,
      expect.objectContaining({
        name: 'Platform',
        code: 'platform',
        mode: 'Single',
        targetEnvironmentIds: [],
        targetsAllEnvironments: false,
        singleTarget: expect.objectContaining({
          code: 'shared',
          subscriptionId,
          region: 'francecentral',
          isProtected: true,
          approvers: ['alice@contoso.example'],
          runners: ['hosted-linux'],
        }) as NonNullable<SaveComponentRequest['singleTarget']>,
        expectedVersion: 7,
      }) as SaveComponentRequest,
    );
  });
});
