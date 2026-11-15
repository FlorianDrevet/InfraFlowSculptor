import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  InstallationChecklistItemResponse,
  InstallationChecklistItemState,
  InstallationChecklistResponse,
  ProjectSummaryResponse,
} from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { DsBadge } from '../../ds/badge/badge';
import { DsButton } from '../../ds/button/button';

const CHECKLIST_STATE_LABEL_KEYS: Record<InstallationChecklistItemState, string> = {
  Automatic: 'project.installationStatusAutomatic',
  ToDo: 'project.installationStatusToDo',
  Done: 'project.installationStatusDone',
  Verified: 'project.installationStatusVerified',
  NeedsReverification: 'project.installationStatusNeedsReverification',
};

const CHECKLIST_STATE_TONES: Record<
  InstallationChecklistItemState,
  'neutral' | 'ember' | 'success'
> = {
  Automatic: 'neutral',
  ToDo: 'ember',
  Done: 'success',
  Verified: 'success',
  NeedsReverification: 'ember',
};

@Component({
  selector: 'app-installation-checklist',
  imports: [DatePipe, RouterLink, TranslocoPipe, DsBadge, DsButton],
  templateUrl: './installation-checklist.html',
  styleUrl: './installation-checklist.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstallationChecklist {
  readonly projectId = input.required<string>();
  private readonly projects = inject(ProjectsService);

  protected readonly checklist = signal<InstallationChecklistResponse | null>(null);
  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly permissions = signal<string[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly actionSaved = signal(false);
  protected readonly copied = signal(false);
  protected readonly copyError = signal(false);
  protected readonly savingItemKey = signal<string | null>(null);
  protected readonly canManage = computed(() => this.permissions().includes('installation.manage'));
  private loadRequestId = 0;
  protected readonly command = computed(
    () =>
      this.checklist()
        ?.sections.flatMap((section) => section.items)
        .find((item) => item.command)?.command ?? null,
  );
  protected readonly counts = computed(() => {
    const items = this.checklist()?.sections.flatMap((section) => section.items) ?? [];
    const automatic = items.filter((item) => item.state === 'Automatic').length;
    const done = items.filter((item) => item.state === 'Done').length;
    const verified = items.filter((item) => item.state === 'Verified').length;
    const toDo = items.filter(
      (item) => item.state === 'ToDo' || item.state === 'NeedsReverification',
    ).length;
    return { automatic, done, verified, toDo, total: items.length };
  });
  protected readonly completedCount = computed(
    () => this.counts().automatic + this.counts().done + this.counts().verified,
  );

  constructor() {
    effect(() => {
      const projectId = this.projectId();
      if (projectId) void this.load(projectId);
    });
  }

  protected retry(): void {
    void this.load(this.projectId());
  }

  protected statusLabel(state: InstallationChecklistItemState): string {
    return CHECKLIST_STATE_LABEL_KEYS[state];
  }

  protected statusTone(state: InstallationChecklistItemState): 'neutral' | 'ember' | 'success' {
    return CHECKLIST_STATE_TONES[state];
  }

  protected isChecked(item: InstallationChecklistItemResponse): boolean {
    return item.state === 'Done' || item.state === 'Verified';
  }

  protected async setCompleted(
    item: InstallationChecklistItemResponse,
    event: Event,
  ): Promise<void> {
    const isCompleted = (event.currentTarget as HTMLInputElement).checked;
    const projectId = this.projectId();
    this.savingItemKey.set(item.key);
    this.saveError.set(false);
    this.actionSaved.set(false);
    try {
      await firstValueFrom(
        this.projects.setInstallationChecklistItem(projectId, item.key, { isCompleted }),
      );
      await this.load(projectId, false);
      this.actionSaved.set(true);
    } catch {
      this.saveError.set(true);
      await this.load(projectId, false);
    } finally {
      this.savingItemKey.set(null);
    }
  }

  protected async copyCommand(): Promise<void> {
    const command = this.command();
    if (!command) return;
    this.copyError.set(false);
    this.copied.set(false);
    try {
      await navigator.clipboard.writeText(command);
      this.copied.set(true);
    } catch {
      this.copyError.set(true);
    }
  }

  private async load(projectId: string, showLoading = true): Promise<void> {
    const requestId = ++this.loadRequestId;
    if (showLoading) this.loading.set(true);
    this.loadError.set(false);
    try {
      const [checklist, project, permissions] = await Promise.all([
        firstValueFrom(this.projects.installationChecklist(projectId)),
        firstValueFrom(this.projects.get(projectId)),
        firstValueFrom(this.projects.myPermissions(projectId)),
      ]);
      if (requestId !== this.loadRequestId || this.projectId() !== projectId) return;
      this.checklist.set(checklist);
      this.project.set(project);
      this.permissions.set(permissions.permissions);
    } catch {
      if (requestId === this.loadRequestId && this.projectId() === projectId) {
        this.loadError.set(true);
      }
    } finally {
      if (requestId === this.loadRequestId && this.projectId() === projectId) {
        this.loading.set(false);
      }
    }
  }
}
