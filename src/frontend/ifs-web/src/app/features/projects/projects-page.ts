import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { DsSegmented, type SegmentedOption } from '../../ds/segmented/segmented';
import type { ProjectSummaryResponse } from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';

@Component({
  selector: 'app-projects-page',
  imports: [DatePipe, DsButton, DsSegmented, RouterLink, TranslocoPipe],
  templateUrl: './projects-page.html',
  styleUrl: './projects-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(ProjectsService);
  private readonly transloco = inject(TranslocoService);

  protected readonly includeDeleted = signal(false);
  protected readonly projects = signal<ProjectSummaryResponse[]>([]);
  protected readonly searchTerm = signal('');
  protected readonly favoritesOnly = signal(false);
  protected readonly savingFavoriteProjectId = signal<string | null>(null);
  protected readonly favoriteError = signal(false);
  protected readonly favoriteCount = computed(
    () => this.projects().filter((project) => project.isFavorite).length,
  );
  protected readonly visibleProjects = computed(() => {
    const searchTerm = this.searchTerm().trim().toLocaleLowerCase();
    return this.projects().filter((project) => {
      const matchesFavoriteFilter = !this.favoritesOnly() || project.isFavorite;
      const matchesSearch =
        !searchTerm ||
        project.name.toLocaleLowerCase().includes(searchTerm) ||
        project.code.toLocaleLowerCase().includes(searchTerm);
      return matchesFavoriteFilter && matchesSearch;
    });
  });
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly restoringProjectId = signal<string | null>(null);
  protected readonly restoreError = signal(false);

  ngOnInit(): void {
    this.includeDeleted.set(this.route.snapshot.data['includeDeleted'] === true);
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const projects = await firstValueFrom(this.service.list(this.includeDeleted()));
      this.projects.set(
        this.includeDeleted() ? projects.filter((project) => project.deletedAt !== null) : projects,
      );
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  protected updateSearchTerm(event: Event): void {
    if (event.target instanceof HTMLInputElement) {
      this.searchTerm.set(event.target.value);
    }
  }

  protected projectFilterOptions(): SegmentedOption[] {
    return [
      {
        value: 'all',
        label: this.transloco.translate('project.allProjectsCount', { count: this.projects().length }),
      },
      {
        value: 'favorites',
        label: this.transloco.translate('project.favoritesCount', { count: this.favoriteCount() }),
      },
    ];
  }

  protected async toggleFavorite(project: ProjectSummaryResponse): Promise<void> {
    if (this.savingFavoriteProjectId()) {
      return;
    }

    this.savingFavoriteProjectId.set(project.id);
    this.favoriteError.set(false);
    try {
      await firstValueFrom(
        project.isFavorite
          ? this.service.removeFavorite(project.id)
          : this.service.setFavorite(project.id),
      );
      await this.load();
    } catch {
      this.favoriteError.set(true);
    } finally {
      this.savingFavoriteProjectId.set(null);
    }
  }

  protected openProject(projectId: string): void {
    const organizationId = this.organizationId();
    if (organizationId) {
      void this.router.navigate(['/o', organizationId, 'projects', projectId]);
    }
  }

  protected createProject(): void {
    const organizationId = this.organizationId();
    if (organizationId) {
      void this.router.navigate(['/o', organizationId, 'projects', 'new']);
    }
  }

  protected async restoreProject(project: ProjectSummaryResponse): Promise<void> {
    if (this.restoringProjectId()) {
      return;
    }

    this.restoringProjectId.set(project.id);
    this.restoreError.set(false);
    try {
      await firstValueFrom(this.service.restore(project.id, { expectedVersion: project.version }));
      await this.load();
    } catch {
      this.restoreError.set(true);
    } finally {
      this.restoringProjectId.set(null);
    }
  }

  private organizationId(): string | null {
    return this.route.parent?.snapshot.paramMap.get('orgId') ?? null;
  }
}
