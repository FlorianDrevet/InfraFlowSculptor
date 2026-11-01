import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of, Subject, throwError } from 'rxjs';
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
        orderingLoading: 'Calcul de l’ordre de déploiement…',
        orderingUnavailable: 'Ordre de déploiement indisponible.',
        componentsHeading: 'Composants',
        componentsInDeploymentOrder: "Composants, dans l'ordre de déploiement",
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
      resources: [],
    } as ComponentResponse;
    const dependentComponent = {
      ...component,
      id: '99999999-9999-4999-8999-999999999999',
      name: 'Api',
      code: 'api',
    };
    const validationOrder = {
      orderedComponents: [
        {
          id: dependentComponent.id,
          code: dependentComponent.code,
          name: dependentComponent.name,
          position: 1,
        },
        { id: component.id, code: component.code, name: component.name, position: 2 },
      ],
    };
    const service = {
      get: vi
        .fn()
        .mockReturnValue(
          of({ id: projectId, version: 1, name: 'Shop', code: 'shop', componentCount: 1 }),
        ),
      environments: vi.fn().mockReturnValue(of([])),
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['components.manage'] })),
      validation: vi.fn().mockReturnValue(of(validationOrder)),
    };
    const componentsService = {
      list: vi.fn().mockReturnValue(of([component, dependentComponent])),
    };

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
    await vi.waitFor(() =>
      expect(
        [
          ...(harness.routeNativeElement?.querySelectorAll<HTMLElement>(
            '.project-overview__components li',
          ) ?? []),
        ].map((row) => row.querySelector('.project-overview__code')?.textContent?.trim()),
      ).toEqual(['api', 'core']),
    );

    expect(componentsService.list).toHaveBeenCalledWith(projectId);
    expect(service.validation).toHaveBeenCalledWith(projectId);
    const componentList = harness.routeNativeElement?.querySelector<HTMLElement>(
      '.project-overview__components',
    );
    const componentRow = componentList?.querySelector<HTMLElement>('li');
    const overview = harness.routeNativeElement?.querySelector<HTMLElement>('.project-overview');
    const section = harness.routeNativeElement?.querySelector<HTMLElement>(
      '.project-overview__section',
    );
    const componentLink = harness.routeNativeElement?.querySelector<HTMLAnchorElement>(
      'a[href*="components/88888888-8888-4888-8888-888888888888"]',
    );
    expect(componentList).not.toBeNull();
    expect(componentRow).not.toBeNull();
    expect(overview).not.toBeNull();
    expect(section).not.toBeNull();
    expect(getComputedStyle(componentList!).display).toBe('grid');
    expect(getComputedStyle(componentRow!).display).toBe('flex');
    expect(componentLink && getComputedStyle(componentLink).display).toBe('flex');
    expect(componentLink?.textContent).toContain('Core');
    expect(
      [...(componentList?.querySelectorAll<HTMLElement>('li') ?? [])].map((row) =>
        row.querySelector('.project-overview__code')?.textContent?.trim(),
      ),
    ).toEqual(['api', 'core']);
    expect(harness.routeNativeElement?.textContent).toContain('Par environnement');
    expect(
      harness.routeNativeElement?.querySelector('#components-heading')?.textContent?.trim(),
    ).toBe("Composants, dans l'ordre de déploiement");
  });

  it('reloads project data when Angular reuses the route for another project', async () => {
    const firstProjectId = '77777777-7777-4777-8777-777777777777';
    const secondProjectId = '66666666-6666-4666-8666-666666666666';
    const firstComponent = {
      id: '88888888-8888-4888-8888-888888888888',
      projectId: firstProjectId,
      version: 1,
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
      resources: [],
    } as ComponentResponse;
    const secondComponent = {
      ...firstComponent,
      id: '99999999-9999-4999-8999-999999999999',
      projectId: secondProjectId,
      name: 'Data',
      code: 'data',
    };
    const firstProjectResponse = new Subject<{
      id: string;
      version: number;
      name: string;
      code: string;
      componentCount: number;
    }>();
    const service = {
      get: vi.fn((projectId: string) =>
        projectId === firstProjectId
          ? firstProjectResponse.asObservable()
          : of({
              id: projectId,
              version: 1,
              name: 'Second project',
              code: 'second',
              componentCount: 1,
            }),
      ),
      environments: vi.fn(() => of([])),
      myPermissions: vi.fn(() => of({ permissions: ['components.manage'] })),
      validation: vi.fn((projectId: string) =>
        of({
          orderedComponents: [
            {
              id: projectId === firstProjectId ? firstComponent.id : secondComponent.id,
              code: projectId === firstProjectId ? firstComponent.code : secondComponent.code,
              name: projectId === firstProjectId ? firstComponent.name : secondComponent.name,
              position: 1,
            },
          ],
        }),
      ),
    };
    const componentsService = {
      list: vi.fn((projectId: string) =>
        of(projectId === firstProjectId ? [firstComponent] : [secondComponent]),
      ),
    };

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
    const basePath = '/o/33333333-3333-4333-8333-333333333333/projects';
    await harness.navigateByUrl(`${basePath}/${firstProjectId}`, ProjectOverview);
    await vi.waitFor(() => expect(service.get).toHaveBeenLastCalledWith(firstProjectId));

    await harness.navigateByUrl(`${basePath}/${secondProjectId}`, ProjectOverview);
    await vi.waitFor(() => expect(service.get).toHaveBeenLastCalledWith(secondProjectId));
    await vi.waitFor(() =>
      expect(
        [
          ...(harness.routeNativeElement?.querySelectorAll<HTMLElement>(
            '.project-overview__code',
          ) ?? []),
        ].map((element) => element.textContent?.trim()),
      ).toEqual(['data']),
    );

    firstProjectResponse.next({
      id: firstProjectId,
      version: 1,
      name: 'First project',
      code: 'first',
      componentCount: 1,
    });
    firstProjectResponse.complete();
    await vi.waitFor(() =>
      expect(
        [
          ...(harness.routeNativeElement?.querySelectorAll<HTMLElement>(
            '.project-overview__code',
          ) ?? []),
        ].map((element) => element.textContent?.trim()),
      ).toEqual(['data']),
    );
  });

  it('keeps project content available when validation cannot provide deployment order', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const component = {
      id: '88888888-8888-4888-8888-888888888888',
      projectId,
      version: 1,
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
      resources: [],
    } as ComponentResponse;
    const service = {
      get: vi.fn(() =>
        of({ id: projectId, version: 1, name: 'Shop', code: 'shop', componentCount: 1 }),
      ),
      environments: vi.fn(() => of([])),
      myPermissions: vi.fn(() => of({ permissions: ['components.manage'] })),
      validation: vi.fn(() => throwError(() => new Error('Validation unavailable.'))),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectOverview],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId', component: ProjectOverview }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: service },
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([component])) } },
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
    await vi.waitFor(() =>
      expect(harness.routeNativeElement?.textContent).toContain(
        'Ordre de déploiement indisponible.',
      ),
    );

    expect(harness.routeNativeElement?.querySelector('.project-overview')).not.toBeNull();
    expect(harness.routeNativeElement?.textContent).toContain('Core');
    expect(harness.routeNativeElement?.textContent).toContain('Ordre de déploiement indisponible.');
    expect(harness.routeNativeElement?.textContent).not.toContain(
      'Impossible de charger le projet.',
    );
    expect(
      harness.routeNativeElement?.querySelector('#components-heading')?.textContent?.trim(),
    ).toBe('Composants');
  });

  it('does not claim a deployment order when the validation response omits a component', async () => {
    const projectId = '77777777-7777-4777-8777-777777777777';
    const core = {
      id: '88888888-8888-4888-8888-888888888888',
      projectId,
      version: 1,
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
      resources: [],
    } as ComponentResponse;
    const data = {
      ...core,
      id: '99999999-9999-4999-8999-999999999999',
      name: 'Data',
      code: 'data',
    };
    const validation = new Subject<{
      orderedComponents: { id: string; code: string; name: string; position: number }[];
    }>();
    const service = {
      get: vi.fn(() =>
        of({ id: projectId, version: 1, name: 'Shop', code: 'shop', componentCount: 2 }),
      ),
      environments: vi.fn(() => of([])),
      myPermissions: vi.fn(() => of({ permissions: ['components.manage'] })),
      validation: vi.fn(() => validation.asObservable()),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectOverview],
      providers: [
        provideRouter(
          [{ path: 'o/:orgId/projects/:projectId', component: ProjectOverview }],
          withComponentInputBinding(),
        ),
        { provide: ProjectsService, useValue: service },
        { provide: ComponentsService, useValue: { list: vi.fn(() => of([data, core])) } },
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
    await vi.waitFor(() =>
      expect(harness.routeNativeElement?.textContent).toContain(
        'Calcul de l’ordre de déploiement…',
      ),
    );
    expect(
      harness.routeNativeElement?.querySelector('#components-heading')?.textContent?.trim(),
    ).toBe('Composants');

    validation.next({
      orderedComponents: [{ id: core.id, code: core.code, name: core.name, position: 1 }],
    });
    validation.complete();
    await vi.waitFor(() =>
      expect(harness.routeNativeElement?.textContent).toContain(
        'Ordre de déploiement indisponible.',
      ),
    );
    harness.fixture.detectChanges();

    expect(
      harness.routeNativeElement?.querySelector('#components-heading')?.textContent?.trim(),
    ).toBe('Composants');
    expect(
      [
        ...(harness.routeNativeElement?.querySelectorAll<HTMLElement>(
          '.project-overview__components li',
        ) ?? []),
      ].map((row) => row.querySelector('.project-overview__code')?.textContent?.trim()),
    ).toEqual(['data', 'core']);
  });
});
