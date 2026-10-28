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
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';
import { DsToggle } from '../../ds/toggle/toggle';
import type { OrganizationMemberResponse, ProjectSummaryResponse } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { ProjectsService } from '../../core/projects/projects.service';

type SettingsForm = {
  name: string;
  code: string;
  description: string;
  systemTagsEnabled: boolean;
  coordinatorUserProfileId: string;
  tags: string;
};

function formFromProject(project: ProjectSummaryResponse): SettingsForm {
  return {
    name: project.name,
    code: project.code,
    description: project.description,
    systemTagsEnabled: project.systemTagsEnabled,
    coordinatorUserProfileId: project.coordinatorUserProfileId ?? '',
    tags: Object.entries(project.tags)
      .map(([key, value]) => `${key}=${value}`)
      .join('\n'),
  };
}

function parseTags(value: string): Record<string, string> {
  return Object.fromEntries(
    value.split(/\r?\n/).flatMap((line) => {
      const separator = line.indexOf('=');
      if (separator < 1) {
        return [];
      }
      const key = line.slice(0, separator).trim();
      return key ? [[key, line.slice(separator + 1).trim()]] : [];
    }),
  );
}

function impactLines(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) =>
      typeof item === 'string' ? item : (JSON.stringify(item) ?? String(item)),
    );
  }
  if (typeof value === 'object' && value !== null) {
    return Object.entries(value).map(([key, item]) => `${key}: ${String(item)}`);
  }
  return typeof value === 'string' ? [value] : [];
}

@Component({
  selector: 'app-project-settings',
  imports: [DsButton, DsTextField, DsToggle, FormsModule, TranslocoPipe],
  templateUrl: './project-settings.html',
  styleUrl: './project-settings.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectSettings implements OnInit {
  readonly projectId = input.required<string>();
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly projects = inject(ProjectsService);
  private readonly organizations = inject(OrganizationsService);
  @ViewChild('impactDialog') private impactDialog?: ElementRef<HTMLDialogElement>;
  @ViewChild('deleteDialog') private deleteDialog?: ElementRef<HTMLDialogElement>;

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly form = signal<SettingsForm>({
    name: '',
    code: '',
    description: '',
    systemTagsEnabled: true,
    coordinatorUserProfileId: '',
    tags: '',
  });
  protected readonly members = signal<OrganizationMemberResponse[]>([]);
  protected readonly permissions = signal<string[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly saving = signal(false);
  protected readonly deleteError = signal(false);
  protected readonly deleting = signal(false);
  protected readonly nameConfirmation = signal('');
  protected readonly affectedResources = signal<string[]>([]);
  protected readonly affectedData = signal<string[]>([]);
  protected readonly versionConflict = signal<{
    stored: ProjectSummaryResponse;
    yours: SettingsForm;
  } | null>(null);
  protected readonly canAdminister = computed(() =>
    this.permissions().includes('project.administer'),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected updateField<K extends keyof SettingsForm>(field: K, value: SettingsForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected async save(confirmImpact = false): Promise<void> {
    const project = this.project();
    if (!project || !this.canAdminister() || this.saving()) {
      return;
    }

    const value = this.form();
    if (value.name.trim().length < 3 || value.code.trim().length < 2) {
      this.saveError.set(true);
      return;
    }

    this.saving.set(true);
    this.saveError.set(false);
    try {
      const updated = await firstValueFrom(
        this.projects.updateSettings(project.id, {
          name: value.name.trim(),
          code: value.code.trim(),
          description: value.description.trim(),
          systemTagsEnabled: value.systemTagsEnabled,
          coordinatorUserProfileId: value.coordinatorUserProfileId || null,
          tags: parseTags(value.tags),
          confirmImpact,
          expectedVersion: Number(project.version),
        }),
      );
      this.project.set(updated);
      this.form.set(formFromProject(updated));
      this.versionConflict.set(null);
    } catch (error) {
      if (
        !confirmImpact &&
        error instanceof ApiError &&
        error.code === 'PROJECT_SETTINGS_CONFIRMATION_REQUIRED'
      ) {
        this.affectedResources.set(impactLines(error.metadata['affectedResources']));
        this.affectedData.set(impactLines(error.metadata['affectedData']));
        this.impactDialog?.nativeElement.showModal();
      } else if (error instanceof ApiError && error.code === 'CONFLICT_VERSION') {
        await this.loadConflict();
      } else {
        this.saveError.set(true);
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected async confirmImpact(): Promise<void> {
    if (this.impactDialog?.nativeElement.open) {
      this.impactDialog.nativeElement.close();
    }
    await this.save(true);
  }

  protected requestDelete(): void {
    if (!this.canAdminister()) {
      return;
    }
    this.nameConfirmation.set('');
    this.deleteError.set(false);
    this.deleteDialog?.nativeElement.showModal();
  }

  protected cancelDelete(): void {
    if (this.deleteDialog?.nativeElement.open) {
      this.deleteDialog.nativeElement.close();
    }
    this.nameConfirmation.set('');
    this.deleteError.set(false);
  }

  protected async confirmDelete(): Promise<void> {
    const project = this.project();
    if (!project || this.nameConfirmation() !== project.name || this.deleting()) {
      this.deleteError.set(true);
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(false);
    try {
      await firstValueFrom(
        this.projects.delete(project.id, {
          nameConfirmation: this.nameConfirmation(),
          expectedVersion: Number(project.version),
        }),
      );
      const organizationId = this.organizationId();
      if (organizationId) {
        await this.router.navigate(['/o', organizationId, 'projects']);
      }
    } catch (error) {
      this.deleteError.set(true);
      if (error instanceof ApiError && error.code === 'CONFLICT_VERSION') {
        await this.loadConflict();
      }
    } finally {
      this.deleting.set(false);
    }
  }

  protected codeLocked(): boolean {
    return this.project()?.firstPublishedAt != null;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, permissions] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      this.project.set(project);
      this.form.set(formFromProject(project));
      this.permissions.set(permissions.permissions);
      const organizationId = this.organizationId();
      if (organizationId) {
        this.members.set(await firstValueFrom(this.organizations.listMembers(organizationId)));
      }
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadConflict(): Promise<void> {
    const yours = this.form();
    try {
      const stored = await firstValueFrom(this.projects.get(this.projectId()));
      this.project.set(stored);
      this.projects.notifyProjectUpdated(stored);
      this.versionConflict.set({ stored, yours });
    } catch {
      this.saveError.set(true);
    }
  }

  private organizationId(): string | null {
    let current = this.route.snapshot;
    while (current) {
      const organizationId = current.paramMap.get('orgId');
      if (organizationId) {
        return organizationId;
      }
      if (!current.parent) {
        break;
      }
      current = current.parent;
    }
    return null;
  }
}
