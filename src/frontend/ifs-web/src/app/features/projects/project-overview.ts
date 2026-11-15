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
  InstallationChecklistItemResponse,
  InstallationChecklistItemState,
  InstallationChecklistResponse,
  ProjectEnvironmentResponse,
  ProjectSummaryResponse,
} from '../../core/api/generated';
import { FEATURES } from '../../core/features/feature-keys';
import { FeatureAvailabilityService } from '../../core/features/feature-availability.service';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectRevisionRail } from './project-revision-rail';

@Component({
  selector: 'app-project-overview',
  imports: [ProjectRevisionRail, RouterLink, TranslocoPipe],
  templateUrl: './project-overview.html',
  styleUrl: './project-overview.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectOverview implements OnChanges, OnInit {
  readonly projectId = input.required<string>();
  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
  protected readonly components = signal<ComponentResponse[]>([]);
  protected readonly installationChecklist = signal<InstallationChecklistResponse | null>(null);
  protected readonly showInstallationCard = signal(false);
  protected readonly installationUnavailable = signal(false);
  protected readonly installationSummary = signal<{
    completed: number;
    total: number;
    remaining: InstallationChecklistItemResponse[];
    remainingCount: number;
  }>({ completed: 0, total: 0, remaining: [], remainingCount: 0 });
  protected readonly permissions = signal<string[]>([]);
  protected readonly orderingLoading = signal(true);
  protected readonly orderingUnavailable = signal(false);
  protected readonly canManageComponents = () => this.permissions().includes('components.manage');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly service = inject(ProjectsService);
  private readonly componentsService = inject(ComponentsService);
  private readonly features = inject(FeatureAvailabilityService);
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
    this.showInstallationCard.set(false);
    this.installationChecklist.set(null);
    this.installationUnavailable.set(false);
    this.installationSummary.set({ completed: 0, total: 0, remaining: [], remainingCount: 0 });
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
      void this.loadInstallationChecklist(projectId, requestId);
    } catch {
      if (requestId === this.loadRequestId) this.loadError.set(true);
    } finally {
      if (requestId === this.loadRequestId) this.loading.set(false);
    }
  }

  private async loadInstallationChecklist(projectId: string, requestId: number): Promise<void> {
    try {
      await this.features.ensureLoaded();
      if (requestId !== this.loadRequestId || projectId !== this.projectId()) return;
      if (!this.features.isEnabled(FEATURES.installationKit)) return;

      this.showInstallationCard.set(true);
      const checklist = await firstValueFrom(this.service.installationChecklist(projectId));
      if (requestId !== this.loadRequestId || projectId !== this.projectId()) return;
      const items = checklist.sections.flatMap((section) => section.items);
      const remainingItems = items.filter(
        (item) => item.state === 'ToDo' || item.state === 'NeedsReverification',
      );
      this.installationChecklist.set(checklist);
      this.installationSummary.set({
        completed: items.filter((item) => isCompletedChecklistState(item.state)).length,
        total: items.length,
        remaining: remainingItems.slice(0, 2),
        remainingCount: remainingItems.length,
      });
    } catch {
      if (requestId === this.loadRequestId && projectId === this.projectId()) {
        this.installationUnavailable.set(true);
      }
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

function isCompletedChecklistState(state: InstallationChecklistItemState): boolean {
  return state === 'Automatic' || state === 'Done' || state === 'Verified';
}
