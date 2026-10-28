import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { ProjectEnvironmentResponse, ProjectSummaryResponse } from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { EnvironmentEdit } from './environment-edit';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement…',
        environmentLoadError: 'Impossible de charger l’environnement.',
        environmentEditorTitle: 'Modifier l’environnement',
        environmentName: 'Nom',
        environmentCodeLabel: 'Code',
        subscriptionId: 'Abonnement',
        regionLabel: 'Région',
        deploymentConnection: 'Connexion de déploiement',
        protectedEnvironment: 'Environnement protégé',
        approvers: 'Approbateurs',
        commaSeparatedHint: 'Séparés par une virgule',
        runners: 'Exécuteurs',
        environmentDescription: 'Description',
        environmentTags: 'Tags',
        tagsHint: 'Une paire clé=valeur par ligne',
        permissionRequired: 'La permission de gérer cet environnement est requise.',
        cancel: 'Annuler',
        environmentSave: 'Enregistrer',
        impactTitle: 'Confirmer l’impact',
        confirmImpact: 'Confirmer',
        noAffectedResources: 'Aucune ressource affectée.',
      },
    });
  }
}

describe('EnvironmentEdit', () => {
  it('keeps save unavailable when an unprivileged user unprotects a protected environment', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const environmentId = '88888888-8888-4888-8888-888888888888';
    const project = {
      id: projectId,
      version: 2,
      name: 'shop',
      code: 'shop',
      firstPublishedAt: null,
    } as ProjectSummaryResponse;
    const environment: ProjectEnvironmentResponse = {
      id: environmentId,
      name: 'Production',
      code: 'prd',
      position: 2,
      subscriptionId: '99999999-9999-4999-8999-999999999999',
      region: 'francecentral',
      deploymentConnection: 'ifs-shop-prd',
      isProtected: true,
      approvers: ['release-approvers'],
      runners: [],
      description: '',
      tags: {},
      firstPublishedAt: null,
    };
    const service = {
      get: vi.fn().mockReturnValue(of(project)),
      environments: vi.fn().mockReturnValue(of([environment])),
      myPermissions: vi
        .fn()
        .mockReturnValue(of({ permissions: ['environments.manage'], assignments: [] })),
    };

    await TestBed.configureTestingModule({
      imports: [EnvironmentEdit],
      providers: [
        provideRouter(
          [
            {
              path: 'o/:orgId/projects/:projectId/environments/:environmentId',
              component: EnvironmentEdit,
            },
          ],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: service },
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
      `/o/contoso/projects/${projectId}/environments/${environmentId}`,
      EnvironmentEdit,
    );
    const native = harness.routeNativeElement as HTMLElement;
    await vi.waitFor(() => expect(native.querySelector('[role="switch"]')).not.toBeNull());
    expect(native.querySelector('button[type="submit"]')).toBeNull();

    (native.querySelector('[role="switch"]') as HTMLButtonElement).click();
    harness.fixture.detectChanges();

    expect(native.querySelector('[role="switch"]')?.getAttribute('aria-checked')).toBe('false');
    expect(native.querySelector('button[type="submit"]')).toBeNull();
  });
});
