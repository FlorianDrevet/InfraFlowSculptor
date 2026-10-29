import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ComponentResponse,
  ProjectEnvironmentResponse,
  ProjectSummaryResponse,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
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
  protected readonly components = signal<ComponentResponse[]>([]);
  protected readonly permissions = signal<string[]>([]);
  protected readonly canManageComponents = () => this.permissions().includes('components.manage');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly service = inject(ProjectsService);
  private readonly componentsService = inject(ComponentsService);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, environments, components, permissions, validation] = await Promise.all([
        firstValueFrom(this.service.get(this.projectId())),
        firstValueFrom(this.service.environments(this.projectId())),
        firstValueFrom(this.componentsService.list(this.projectId())),
        firstValueFrom(this.service.myPermissions(this.projectId())),
        firstValueFrom(this.service.validation(this.projectId())),
      ]);
      this.project.set(project);
      this.environments.set(environments);
      const positions = new Map(validation.orderedComponents.map((item) => [item.id, item.position]));
      this.components.set(
        [...components].sort(
          (left, right) =>
            Number(positions.get(left.id) ?? Number.MAX_SAFE_INTEGER) -
            Number(positions.get(right.id) ?? Number.MAX_SAFE_INTEGER),
        ),
      );
      this.permissions.set(permissions.permissions);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
