import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { ProjectSummaryResponse } from '../../core/api/generated';
import { NavigationRegistry } from '../../core/layout/navigation.registry';
import { ProjectsService } from '../../core/projects/projects.service';

@Component({
  selector: 'app-project-workspace',
  imports: [RouterLink, RouterOutlet, TranslocoPipe],
  templateUrl: './project-workspace.html',
  styleUrl: './project-workspace.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectWorkspace {
  readonly orgId = input.required<string>();
  readonly projectId = input.required<string>();
  private readonly projects = inject(ProjectsService);
  private readonly navigation = inject(NavigationRegistry);

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly loadError = signal(false);
  constructor() {
    this.projects.projectUpdates$.pipe(takeUntilDestroyed()).subscribe((updatedProject) => {
      if (updatedProject.id === this.projectId()) {
        this.project.set(updatedProject);
      }
    });
  }

  private readonly loadProject = effect(() => {
    const projectId = this.projectId();
    if (projectId) {
      void this.load(projectId);
    }
  });

  private readonly registerProjectNavigation = effect((onCleanup) => {
    const orgId = this.orgId();
    const projectId = this.projectId();
    if (!orgId || !projectId) {
      return;
    }

    const base = `/o/${orgId}/projects/${projectId}`;
    const unregister = [
      this.navigation.register({
        id: `project-overview-${projectId}`,
        labelKey: 'project.overviewLink',
        path: base,
        scope: 'project',
        iconName: 'folder',
      }),
      this.navigation.register({
        id: `project-environments-${projectId}`,
        labelKey: 'project.environmentsLink',
        path: `${base}/environments`,
        scope: 'project',
        iconName: 'layers',
      }),
      this.navigation.register({
        id: `project-naming-${projectId}`,
        labelKey: 'project.namingLink',
        path: `${base}/naming`,
        scope: 'project',
        iconName: 'list',
      }),
      this.navigation.register({
        id: `project-components-${projectId}`,
        labelKey: 'project.componentsHeading',
        path: `${base}/components`,
        scope: 'project',
        iconName: 'folder',
      }),
      this.navigation.register({
        id: `project-settings-${projectId}`,
        labelKey: 'project.settingsLink',
        path: `${base}/settings`,
        scope: 'project',
        iconName: 'sliders',
      }),
      this.navigation.register({
        id: `project-members-${projectId}`,
        labelKey: 'project.membersLink',
        path: `${base}/members`,
        scope: 'project',
        iconName: 'users',
      }),
    ];
    onCleanup(() => unregister.forEach((remove) => remove()));
  });

  private async load(projectId: string): Promise<void> {
    this.loadError.set(false);
    try {
      const project = await firstValueFrom(this.projects.get(projectId));
      if (this.projectId() === projectId) {
        const current = this.project();
        if (
          !current ||
          current.id !== project.id ||
          Number(current.version) <= Number(project.version)
        ) {
          this.project.set(project);
        }
      }
    } catch {
      if (this.projectId() === projectId) {
        this.loadError.set(true);
      }
    }
  }
}
