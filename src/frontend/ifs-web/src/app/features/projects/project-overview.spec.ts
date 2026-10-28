import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { ComponentResponse } from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectOverview } from './project-overview';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement…',
        loadError: 'Impossible de charger le projet.',
        componentsHeading: 'Composants',
        componentsEmpty: 'Aucun composant dans ce projet.',
        environmentsHeading: 'Environnements',
        viewAllEnvironments: 'Gérer les environnements',
        noEnvironments: 'Aucun environnement dans ce projet.',
      },
      component: {
        modePerEnvironment: 'Par environnement',
        modeSingle: 'Unique',
        create: 'Créer un composant',
      },
    });
  }
}

describe('ProjectOverview', () => {
  it('loads components and links each one to its component screen', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const component = {
      id: '88888888-8888-4888-8888-888888888888',
      projectId,
      version: 1,
      name: 'Core',
      code: 'core',
      description: '',
      mode: 'PerEnvironment',
      targetsAllEnvironments: true,
      targetEnvironmentIds: [],
      singleTarget: null,
      removedResourceBehavior: 'Detach',
      deletionProtectionEnabled: true,
      firstPublishedAt: null,
      tags: {},
      resourceGroups: [],
    } as ComponentResponse;
    const service = {
      get: vi
        .fn()
        .mockReturnValue(
          of({ id: projectId, version: 1, name: 'Shop', code: 'shop', componentCount: 1 }),
        ),
      environments: vi.fn().mockReturnValue(of([])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['components.manage'] })),
    };
    const componentsService = { list: vi.fn().mockReturnValue(of([component])) };

    await TestBed.configureTestingModule({
      imports: [ProjectOverview],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId', component: ProjectOverview }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: service },
        { provide: ComponentsService, useValue: componentsService },
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
      `/o/33333333-3333-4333-8333-333333333333/projects/${projectId}`,
      ProjectOverview,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(componentsService.list).toHaveBeenCalledWith(projectId);
    const componentList = harness.routeNativeElement?.querySelector<HTMLElement>(
      '.project-overview__components',
    );
    const componentRow = componentList?.querySelector<HTMLElement>('li');
    const componentLink = harness.routeNativeElement?.querySelector<HTMLAnchorElement>(
      'a[href*="components/88888888-8888-4888-8888-888888888888"]',
    );
    expect(componentList).not.toBeNull();
    expect(componentRow).not.toBeNull();
    expect(getComputedStyle(componentList!).display).toBe('grid');
    expect(getComputedStyle(componentRow!).display).toBe('flex');
    expect(componentLink && getComputedStyle(componentLink).display).toBe('flex');
    expect(componentLink?.textContent).toContain('Core');
    expect(harness.routeNativeElement?.textContent).toContain('Par environnement');
  });
});
