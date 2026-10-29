import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of, Subject } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { ProjectSummaryResponse } from '../../core/api/generated';
import { NavigationRegistry } from '../../core/layout/navigation.registry';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectWorkspace } from './project-workspace';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({ project: { projectsLink: 'Projets', loading: 'Chargement…' } });
  }
}

describe('ProjectWorkspace', () => {
  it('refreshes the project header when settings are saved', async () => {
    const initialProject = {
      id: '77777777-7777-4777-8777-777777777777',
      version: 2,
      name: 'Ancien nom',
      code: 'shop',
      description: '',
      environmentCount: 2,
      modelVersion: 1,
      firstPublishedAt: null,
      deletedAt: null,
    } as ProjectSummaryResponse;
    const updatedProject = { ...initialProject, version: 3, name: 'Nouveau nom' };
    const projectUpdates = new Subject<ProjectSummaryResponse>();
    const registerNavigation = vi.fn(() => vi.fn());
    const projects = {
      get: vi.fn().mockReturnValue(of(initialProject)),
      projectUpdates$: projectUpdates.asObservable(),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectWorkspace],
      providers: [
        provideRouter([]),
        { provide: ProjectsService, useValue: projects },
      { provide: NavigationRegistry, useValue: { register: registerNavigation } },
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

    const fixture = TestBed.createComponent(ProjectWorkspace);
    fixture.componentRef.setInput('orgId', '33333333-3333-4333-8333-333333333333');
    fixture.componentRef.setInput('projectId', initialProject.id);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const workspace = fixture.nativeElement as HTMLElement;
    const workspaceSection = workspace.querySelector<HTMLElement>('.project-workspace');
    expect(workspaceSection).not.toBeNull();
    expect(workspace.querySelector('h1')?.textContent).toContain('Ancien nom');
    expect(registerNavigation).toHaveBeenCalledWith(
      expect.objectContaining({
        labelKey: 'project.findingsLink',
        path: `/o/33333333-3333-4333-8333-333333333333/projects/${initialProject.id}/findings`,
      }),
    );
    projectUpdates.next(updatedProject);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(workspace.querySelector('h1')?.textContent).toContain('Nouveau nom');
  });

  it('does not replace a newer update with an older project load', async () => {
    const initialProject = {
      id: '77777777-7777-4777-8777-777777777777',
      version: 2,
      name: 'Ancien nom',
      code: 'shop',
      description: '',
      environmentCount: 2,
      modelVersion: 1,
      firstPublishedAt: null,
      deletedAt: null,
    } as ProjectSummaryResponse;
    const updatedProject = { ...initialProject, version: 3, name: 'Nouveau nom' };
    const projectLoad = new Subject<ProjectSummaryResponse>();
    const projectUpdates = new Subject<ProjectSummaryResponse>();
    const projects = {
      get: vi.fn().mockReturnValue(projectLoad.asObservable()),
      projectUpdates$: projectUpdates.asObservable(),
    };

    await TestBed.configureTestingModule({
      imports: [ProjectWorkspace],
      providers: [
        provideRouter([]),
        { provide: ProjectsService, useValue: projects },
        { provide: NavigationRegistry, useValue: { register: vi.fn(() => vi.fn()) } },
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

    const fixture = TestBed.createComponent(ProjectWorkspace);
    fixture.componentRef.setInput('orgId', '33333333-3333-4333-8333-333333333333');
    fixture.componentRef.setInput('projectId', initialProject.id);
    fixture.detectChanges();
    expect(projects.get).toHaveBeenCalledWith(initialProject.id);

    const workspace = fixture.nativeElement as HTMLElement;
    projectUpdates.next(updatedProject);
    fixture.detectChanges();
    expect(workspace.querySelector('h1')?.textContent).toContain('Nouveau nom');

    projectLoad.next(initialProject);
    projectLoad.complete();
    await Promise.resolve();
    fixture.detectChanges();
    expect(workspace.querySelector('h1')?.textContent).toContain('Nouveau nom');
  });
});
