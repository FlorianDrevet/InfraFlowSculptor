import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnChanges,
  OnInit,
  signal,
  SimpleChanges,
} from '@angular/core';
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
export class ProjectOverview implements OnChanges, OnInit {
  readonly projectId = input.required<string>();
  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
  protected readonly components = signal<ComponentResponse[]>([]);
  protected readonly permissions = signal<string[]>([]);
  protected readonly orderingLoading = signal(true);
  protected readonly orderingUnavailable = signal(false);
  protected readonly canManageComponents = () => this.permissions().includes('components.manage');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly service = inject(ProjectsService);
  private readonly componentsService = inject(ComponentsService);
  private loadRequestId = 0;

  ngOnInit(): void {
    void this.load();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId'] && !changes['projectId'].firstChange) {
      void this.load();
    }
  }

  private async load(): Promise<void> {
    const requestId = ++this.loadRequestId;
    const projectId = this.projectId();
    this.loading.set(true);
    this.loadError.set(false);
    this.orderingUnavailable.set(false);
    this.orderingLoading.set(true);
    try {
      const [project, environments, components, permissions] = await Promise.all([
        firstValueFrom(this.service.get(projectId)),
        firstValueFrom(this.service.environments(projectId)),
        firstValueFrom(this.componentsService.list(projectId)),
        firstValueFrom(this.service.myPermissions(projectId)),
      ]);
      if (requestId !== this.loadRequestId) return;
      this.project.set(project);
      this.environments.set(environments);
      this.components.set(components);
      this.permissions.set(permissions.permissions);
      void this.loadOrdering(projectId, components, requestId);
    } catch {
      if (requestId === this.loadRequestId) this.loadError.set(true);
    } finally {
      if (requestId === this.loadRequestId) this.loading.set(false);
    }
  }

  private async loadOrdering(
    projectId: string,
    components: ComponentResponse[],
    requestId: number,
  ): Promise<void> {
    try {
      const validation = await firstValueFrom(this.service.validation(projectId));
      if (requestId !== this.loadRequestId) return;
      const positions = new Map(
        validation.orderedComponents.map((item) => [item.id, item.position]),
      );
      if (components.some((component) => !positions.has(component.id))) {
        this.orderingUnavailable.set(true);
        return;
      }
      this.components.set(
        [...components].sort(
          (left, right) => Number(positions.get(left.id)) - Number(positions.get(right.id)),
        ),
      );
      this.orderingUnavailable.set(false);
    } catch {
      if (requestId === this.loadRequestId) this.orderingUnavailable.set(true);
    } finally {
      if (requestId === this.loadRequestId) this.orderingLoading.set(false);
    }
  }
}
