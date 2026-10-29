import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ObjectType,
  ProjectValidationFindingResponse,
  ProjectValidationResponse,
} from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { DsBadge } from '../../ds/badge/badge';
import { DsButton } from '../../ds/button/button';

@Component({
  selector: 'app-project-findings',
  imports: [DsBadge, DsButton, FormsModule, RouterLink, TranslocoPipe],
  templateUrl: './project-findings.html',
  styleUrl: './project-findings.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectFindings {
  readonly projectId = input.required<string>();
  private readonly projects = inject(ProjectsService);
  private readonly transloco = inject(TranslocoService);

  protected readonly validation = signal<ProjectValidationResponse | null>(null);
  protected readonly permissions = signal<string[]>([]);
  protected readonly canEditModel = computed(() => this.permissions().includes('model.edit'));
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal(false);
  protected readonly actionSaved = signal(false);
  protected readonly saving = signal(false);
  protected readonly comment = signal('');
  protected readonly commentError = signal(false);
  protected readonly selectedFinding = signal<ProjectValidationFindingResponse | null>(null);

  private readonly loadProjectValidation = effect(() => {
    const projectId = this.projectId();
    if (projectId) void this.load(projectId);
  });

  protected message(finding: ProjectValidationFindingResponse): string {
    return this.transloco.getActiveLang() === 'fr' ? finding.messageFr : finding.messageEn;
  }

  protected severityLabel(finding: ProjectValidationFindingResponse): string {
    const labels: Record<string, string> = {
      Error: 'project.validationSeverityError',
      ErrorAtPublish: 'project.validationSeverityPublishError',
      Warning: 'project.validationSeverityWarning',
      Info: 'project.validationSeverityInfo',
    };
    return labels[finding.severity] ?? 'project.validationSeverityInfo';
  }

  protected severityTone(finding: ProjectValidationFindingResponse): 'danger' | 'ember' | 'signal' | 'neutral' {
    const tones: Record<string, 'danger' | 'ember' | 'signal' | 'neutral'> = {
      Error: 'danger',
      ErrorAtPublish: 'danger',
      Warning: 'ember',
      Info: 'signal',
    };
    return tones[finding.severity] ?? 'neutral';
  }

  protected countLabelKey(
    count: number | string,
    singularKey: string,
    pluralKey: string,
  ): string {
    return Number(count) <= 1 ? singularKey : pluralKey;
  }

  protected countValue(count: number | string): number {
    return Number(count);
  }

  protected findingKey(finding: ProjectValidationFindingResponse): string {
    return [
      finding.code,
      finding.subjectType,
      finding.subjectId,
      finding.environmentId ?? '',
      finding.detailKey ?? '',
    ].join(':');
  }

  protected canAcknowledge(finding: ProjectValidationFindingResponse): boolean {
    return this.canEditModel()
      && finding.severity === 'Warning'
      && finding.acknowledgment === null
      && finding.objectVersion !== null;
  }

  protected subjectRoute(finding: ProjectValidationFindingResponse): string[] | null {
    if (finding.subjectType === 'Environment' && finding.environmentId) {
      return ['../environments', finding.environmentId];
    }
    if (finding.subjectComponentId && finding.subjectResourceId) {
      return ['../components', finding.subjectComponentId, 'resources', finding.subjectResourceId];
    }
    if (finding.subjectComponentId) {
      return ['../components', finding.subjectComponentId];
    }
    return null;
  }

  protected subjectQueryParams(finding: ProjectValidationFindingResponse): Record<string, string> | null {
    return finding.subjectType === 'Link' ? { tab: 'links' } : null;
  }

  protected openAcknowledgment(finding: ProjectValidationFindingResponse): void {
    if (!this.canAcknowledge(finding)) return;
    this.selectedFinding.set(finding);
    this.comment.set('');
    this.commentError.set(false);
    this.actionError.set(false);
    this.actionSaved.set(false);
  }

  protected closeAcknowledgment(): void {
    if (this.saving()) return;
    this.selectedFinding.set(null);
    this.comment.set('');
    this.commentError.set(false);
    this.actionError.set(false);
  }

  protected updateComment(value: string): void {
    this.comment.set(value);
    this.commentError.set(false);
  }

  protected retry(): void {
    void this.load(this.projectId());
  }

  protected async saveAcknowledgment(): Promise<void> {
    const finding = this.selectedFinding();
    const validation = this.validation();
    const comment = this.comment().trim();
    if (!finding || !validation || !this.canAcknowledge(finding) || this.saving()) return;
    if (!comment) {
      this.commentError.set(true);
      return;
    }

    this.actionError.set(false);
    this.saving.set(true);
    try {
      await firstValueFrom(this.projects.acknowledgeFinding(this.projectId(), finding.code, {
        comment,
        subjectId: finding.subjectId,
        subjectType: finding.subjectType as ObjectType,
        environmentId: finding.environmentId,
        expectedObjectVersion: Number(finding.objectVersion),
        detailKey: finding.detailKey,
      }));
      this.actionSaved.set(true);
      this.selectedFinding.set(null);
      await this.load(this.projectId());
    } catch {
      this.actionError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  protected async applyFix(finding: ProjectValidationFindingResponse): Promise<void> {
    const validation = this.validation();
    if (!validation || !this.canEditModel() || !finding.canApplyFix || finding.objectVersion === null || this.saving()) {
      return;
    }

    this.actionError.set(false);
    this.actionSaved.set(false);
    this.saving.set(true);
    try {
      await firstValueFrom(this.projects.applyFindingFix(this.projectId(), finding.code, {
        subjectId: finding.subjectId,
        subjectType: finding.subjectType as ObjectType,
        environmentId: finding.environmentId,
        expectedObjectVersion: Number(finding.objectVersion),
        detailKey: finding.detailKey,
      }));
      this.actionSaved.set(true);
      await this.load(this.projectId());
    } catch {
      this.actionError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(projectId: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [validation, permissions] = await Promise.all([
        firstValueFrom(this.projects.validation(projectId)),
        firstValueFrom(this.projects.myPermissions(projectId)),
      ]);
      if (this.projectId() === projectId) {
        this.validation.set(validation);
        this.permissions.set(permissions.permissions);
      }
    } catch {
      if (this.projectId() === projectId) this.loadError.set(true);
    } finally {
      if (this.projectId() === projectId) this.loading.set(false);
    }
  }
}
