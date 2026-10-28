import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { ProjectEnvironmentResponse, ProjectSummaryResponse } from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';

@Component({
  selector: 'app-project-overview',
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './project-overview.html',
  styleUrl: './project-overview.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectOverview implements OnInit {
  readonly projectId = input.required<string>();
  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly service = inject(ProjectsService);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, environments] = await Promise.all([
        firstValueFrom(this.service.get(this.projectId())),
        firstValueFrom(this.service.environments(this.projectId())),
      ]);
      this.project.set(project);
      this.environments.set(environments);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
