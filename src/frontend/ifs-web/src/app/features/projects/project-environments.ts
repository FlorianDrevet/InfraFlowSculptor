import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import type { ProjectEnvironmentResponse } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { ProjectsService } from '../../core/projects/projects.service';

function impactLines(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) => (typeof item === 'string' ? item : JSON.stringify(item)));
  }

  if (typeof value === 'object' && value !== null) {
    return Object.entries(value).map(([key, item]) => {
      const detail = Array.isArray(item) ? item.join(', ') : String(item);
      return `${key}: ${detail}`;
    });
  }

  return typeof value === 'string' ? [value] : [];
}

@Component({
  selector: 'app-project-environments',
  imports: [DsButton, RouterLink, TranslocoPipe],
  templateUrl: './project-environments.html',
  styleUrl: './project-environments.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectEnvironments implements OnInit {
  readonly projectId = input.required<string>();
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(ProjectsService);
  @ViewChild('deleteDialog') private deleteDialog?: ElementRef<HTMLDialogElement>;

  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
  protected readonly projectVersion = signal(0);
  protected readonly permissions = signal<string[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal(false);
  protected readonly deleting = signal(false);
  protected readonly pendingDeletion = signal<ProjectEnvironmentResponse | null>(null);
  protected readonly impactReady = signal(false);
  protected readonly affectedResources = signal<string[]>([]);
  protected readonly affectedData = signal<string[]>([]);
  protected readonly canManage = computed(() => this.permissions().includes('environments.manage'));
  protected readonly canManageProtected = computed(() =>
    this.permissions().includes('environments.protected.manage'),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected mayChange(environment: ProjectEnvironmentResponse): boolean {
    return this.canManage() && (!environment.isProtected || this.canManageProtected());
  }

  protected createEnvironment(): void {
    void this.router.navigate(['new'], { relativeTo: this.route });
  }

  protected requestDeletion(environment: ProjectEnvironmentResponse): void {
    this.pendingDeletion.set(environment);
    this.impactReady.set(false);
    this.affectedResources.set([]);
    this.affectedData.set([]);
    this.actionError.set(false);
    this.deleteDialog?.nativeElement.showModal();
  }

  protected cancelDeletion(): void {
    if (this.deleteDialog?.nativeElement.open) {
      this.deleteDialog.nativeElement.close();
    }
    this.pendingDeletion.set(null);
    this.impactReady.set(false);
    this.deleting.set(false);
  }

  protected async confirmDeletion(): Promise<void> {
    const environment = this.pendingDeletion();
    if (!environment || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.actionError.set(false);
    try {
      await firstValueFrom(
        this.service.deleteEnvironment(this.projectId(), environment.id, {
          confirmDeletion: this.impactReady(),
          expectedVersion: this.projectVersion(),
        }),
      );
      this.cancelDeletion();
      await this.load();
    } catch (error) {
      if (error instanceof ApiError && error.code === 'ENVIRONMENT_DELETE_CONFIRMATION_REQUIRED') {
        this.affectedResources.set(impactLines(error.metadata['affectedResources']));
        this.affectedData.set(impactLines(error.metadata['affectedData']));
        this.impactReady.set(true);
      } else if (error instanceof ApiError && error.code === 'CONFLICT_VERSION') {
        await this.load();
        this.actionError.set(true);
      } else {
        this.actionError.set(true);
      }
    } finally {
      this.deleting.set(false);
    }
  }

  protected async move(environment: ProjectEnvironmentResponse, offset: -1 | 1): Promise<void> {
    if (!this.canManage()) {
      return;
    }

    this.actionError.set(false);
    try {
      await firstValueFrom(
        this.service.moveEnvironment(this.projectId(), environment.id, {
          position: Number(environment.position) + offset,
          expectedVersion: this.projectVersion(),
        }),
      );
      await this.load();
    } catch {
      this.actionError.set(true);
      await this.load();
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, environments, permissions] = await Promise.all([
        firstValueFrom(this.service.get(this.projectId())),
        firstValueFrom(this.service.environments(this.projectId())),
        firstValueFrom(this.service.myPermissions(this.projectId())),
      ]);
      this.projectVersion.set(Number(project.version));
      this.environments.set(environments);
      this.permissions.set(permissions.permissions);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
