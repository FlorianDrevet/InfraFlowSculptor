import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  TemplateRef,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, takeUntil } from 'rxjs';
import type {
  ObjectType,
  ProjectValidationFindingResponse,
  ProjectValidationResponse,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { ProjectsService } from '../../core/projects/projects.service';
import { DsBadge } from '../../ds/badge/badge';
import { DsButton } from '../../ds/button/button';

const OBJECT_VERSION_PATTERN = /^\d+$/;
const ACTIONABLE_SUBJECT_TYPES = [
  'Project',
  'Environment',
  'Component',
  'Resource',
  'Link',
  'AppSetting',
  'Application',
  'ResourceGroup',
] as const satisfies readonly Exclude<ObjectType, 'FindingAcknowledgment'>[];

function parseObjectVersion(value: number | string | null): number | null {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? value : null;
  }
  if (typeof value !== 'string' || !OBJECT_VERSION_PATTERN.test(value)) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function parseActionableSubjectType(
  value: string,
): Exclude<ObjectType, 'FindingAcknowledgment'> | null {
  return ACTIONABLE_SUBJECT_TYPES.find((subjectType) => subjectType === value) ?? null;
}

function isVersionConflict(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    ((error.status === 409 && error.code === 'CONFLICT_VERSION') ||
      (error.status === 404 &&
        ['VALIDATION_FINDING_NOT_FOUND', 'VALIDATION_FIX_NOT_AVAILABLE'].includes(error.code)))
  );
}

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
  private readonly dialog = inject(Dialog);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('acknowledgmentTemplate')
  private acknowledgmentTemplate!: TemplateRef<unknown>;
  @ViewChild('actionSavedStatus')
  private actionSavedStatus?: ElementRef<HTMLParagraphElement>;
  @ViewChild('actionConflictStatus')
  private actionConflictStatus?: ElementRef<HTMLParagraphElement>;
  private acknowledgmentDialog?: DialogRef<unknown>;
  private acknowledgmentTrigger?: HTMLElement;
  private restoreAcknowledgmentFocus = true;
  private conflictedAcknowledgmentKey: string | null = null;
  private preserveCommentOnAcknowledgmentClose = false;
  private loadRequestId = 0;
  private actionRequestId = 0;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.restoreAcknowledgmentFocus = false;
      this.acknowledgmentDialog?.close();
    });
  }

  protected readonly validation = signal<ProjectValidationResponse | null>(null);
  protected readonly permissions = signal<string[]>([]);
  protected readonly canEditModel = computed(() => this.permissions().includes('model.edit'));
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal(false);
  protected readonly actionConflict = signal(false);
  protected readonly actionSaved = signal(false);
  protected readonly saving = signal(false);
  protected readonly comment = signal('');
  protected readonly commentError = signal(false);
  protected readonly selectedFinding = signal<ProjectValidationFindingResponse | null>(null);

  private readonly loadProjectValidation = effect((onCleanup) => {
    const projectId = this.projectId();
    onCleanup(() => {
      this.restoreAcknowledgmentFocus = false;
      this.acknowledgmentDialog?.close();
    });
    if (projectId) {
      this.actionRequestId++;
      this.validation.set(null);
      this.permissions.set([]);
      this.loadError.set(false);
      this.actionSaved.set(false);
      this.actionError.set(false);
      this.actionConflict.set(false);
      this.saving.set(false);
      void this.load(projectId);
    }
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

  protected severityTone(
    finding: ProjectValidationFindingResponse,
  ): 'danger' | 'ember' | 'signal' | 'neutral' {
    const tones: Record<string, 'danger' | 'ember' | 'signal' | 'neutral'> = {
      Error: 'danger',
      ErrorAtPublish: 'danger',
      Warning: 'ember',
      Info: 'signal',
    };
    return tones[finding.severity] ?? 'neutral';
  }

  protected countLabelKey(count: number | string, singularKey: string, pluralKey: string): string {
    return Number(count) <= 1 ? singularKey : pluralKey;
  }

  protected countValue(count: number | string): number {
    return Number(count);
  }

  protected findingKey(finding: ProjectValidationFindingResponse, index: number): string {
    const identity = [
      finding.code,
      finding.subjectType,
      finding.subjectId,
      finding.environmentId ?? '',
      finding.detailKey ?? '',
    ];
    const variant = [finding.messageFr, finding.messageEn, finding.fixCode ?? ''];
    const validationFindings = this.validation()?.findings ?? [];
    const isSameFinding = (candidate: ProjectValidationFindingResponse): boolean =>
      candidate.code === finding.code &&
      candidate.subjectType === finding.subjectType &&
      candidate.subjectId === finding.subjectId &&
      candidate.environmentId === finding.environmentId &&
      candidate.detailKey === finding.detailKey &&
      candidate.messageFr === finding.messageFr &&
      candidate.messageEn === finding.messageEn &&
      candidate.fixCode === finding.fixCode;
    const matchingFindings = validationFindings.filter(isSameFinding);
    const occurrence =
      matchingFindings.length > 1
        ? validationFindings.slice(0, index).filter(isSameFinding).length
        : null;
    return JSON.stringify([...identity, ...variant, occurrence]);
  }

  protected canAcknowledge(finding: ProjectValidationFindingResponse): boolean {
    return (
      this.canEditModel() &&
      parseActionableSubjectType(finding.subjectType) !== null &&
      finding.severity === 'Warning' &&
      finding.acknowledgment === null &&
      parseObjectVersion(finding.objectVersion) !== null
    );
  }

  protected canApplyFix(finding: ProjectValidationFindingResponse): boolean {
    return (
      this.canEditModel() &&
      finding.canApplyFix &&
      parseActionableSubjectType(finding.subjectType) !== null &&
      parseObjectVersion(finding.objectVersion) !== null
    );
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

  protected subjectQueryParams(
    finding: ProjectValidationFindingResponse,
  ): Record<string, string> | null {
    return finding.subjectType === 'Link' ? { tab: 'links' } : null;
  }

  protected openAcknowledgment(finding: ProjectValidationFindingResponse, event: MouseEvent): void {
    if (!this.canAcknowledge(finding) || this.acknowledgmentDialog || this.saving()) return;
    const findings = this.validation()?.findings ?? [];
    const findingIndex = findings.indexOf(finding);
    const acknowledgmentKey = this.findingKey(finding, Math.max(findingIndex, 0));
    const preserveComment =
      this.actionConflict() && this.conflictedAcknowledgmentKey === acknowledgmentKey;
    const eventTarget = event.target instanceof HTMLElement ? event.target.closest('button') : null;
    const eventHost =
      event.currentTarget instanceof HTMLElement
        ? event.currentTarget.querySelector('button')
        : null;
    this.acknowledgmentTrigger = eventTarget ?? eventHost ?? undefined;
    this.restoreAcknowledgmentFocus = true;
    this.selectedFinding.set(finding);
    if (!preserveComment) this.comment.set('');
    this.conflictedAcknowledgmentKey = null;
    this.commentError.set(false);
    this.actionError.set(false);
    this.actionConflict.set(false);
    this.actionSaved.set(false);
    const dialogRef = this.dialog.open(this.acknowledgmentTemplate, {
      ariaLabelledBy: 'finding-acknowledgment-title',
      ariaModal: true,
      role: 'dialog',
      panelClass: 'ifs-dialog-pane',
      backdropClass: 'ifs-dialog-backdrop',
      maxWidth: 'calc(100vw - 32px)',
      autoFocus: 'first-tabbable',
      restoreFocus: false,
      disableClose: true,
    });
    this.acknowledgmentDialog = dialogRef;
    dialogRef.keydownEvents.pipe(takeUntil(dialogRef.closed)).subscribe((event) => {
      if (event.key === 'Escape') this.closeAcknowledgment();
    });
    dialogRef.closed.subscribe(() => {
      if (this.acknowledgmentDialog !== dialogRef) return;
      this.acknowledgmentDialog = undefined;
      if (this.restoreAcknowledgmentFocus) this.acknowledgmentTrigger?.focus();
      this.acknowledgmentTrigger = undefined;
      this.restoreAcknowledgmentFocus = true;
      this.selectedFinding.set(null);
      if (!this.preserveCommentOnAcknowledgmentClose) this.comment.set('');
      this.preserveCommentOnAcknowledgmentClose = false;
      this.commentError.set(false);
      this.actionError.set(false);
    });
  }

  protected closeAcknowledgment(): void {
    if (this.saving()) return;
    this.acknowledgmentDialog?.close();
  }

  protected updateComment(value: string): void {
    this.comment.set(value);
    this.commentError.set(false);
  }

  protected updateCommentFromEvent(event: Event): void {
    if (event.target instanceof HTMLTextAreaElement) {
      this.updateComment(event.target.value);
    }
  }

  protected retry(): void {
    void this.load(this.projectId());
  }

  protected async saveAcknowledgment(): Promise<void> {
    const projectId = this.projectId();
    const finding = this.selectedFinding();
    const validation = this.validation();
    const expectedObjectVersion = finding ? parseObjectVersion(finding.objectVersion) : null;
    const comment = this.comment().trim();
    if (this.saving()) return;
    if (!this.canEditModel()) {
      this.actionError.set(true);
      return;
    }
    if (!finding || !validation || expectedObjectVersion === null) return;
    const subjectType = parseActionableSubjectType(finding.subjectType);
    if (!subjectType) {
      this.actionError.set(true);
      return;
    }
    if (!this.canAcknowledge(finding)) return;
    if (!comment) {
      this.commentError.set(true);
      return;
    }

    const requestId = ++this.actionRequestId;
    this.actionError.set(false);
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.projects.acknowledgeFinding(projectId, finding.code, {
          comment,
          subjectId: finding.subjectId,
          subjectType,
          environmentId: finding.environmentId,
          expectedObjectVersion,
          detailKey: finding.detailKey,
        }),
      );
      if (
        this.destroyRef.destroyed ||
        this.actionRequestId !== requestId ||
        this.projectId() !== projectId
      )
        return;
      this.actionSaved.set(true);
      const dialogRef = this.acknowledgmentDialog;
      this.restoreAcknowledgmentFocus = false;
      const dialogClosed = dialogRef ? firstValueFrom(dialogRef.closed) : Promise.resolve();
      dialogRef?.close();
      await dialogClosed;
      await this.load(projectId);
      if (this.projectId() === projectId && this.actionRequestId === requestId) {
        this.focusSavedStatus();
      }
    } catch (error) {
      if (
        !this.destroyRef.destroyed &&
        this.actionRequestId === requestId &&
        this.projectId() === projectId
      ) {
        if (isVersionConflict(error)) {
          this.actionError.set(false);
          this.actionConflict.set(true);
          const findings = this.validation()?.findings ?? [];
          const findingIndex = findings.indexOf(finding);
          this.conflictedAcknowledgmentKey = this.findingKey(finding, Math.max(findingIndex, 0));
          this.preserveCommentOnAcknowledgmentClose = true;
          const dialogRef = this.acknowledgmentDialog;
          this.restoreAcknowledgmentFocus = false;
          const dialogClosed = dialogRef ? firstValueFrom(dialogRef.closed) : Promise.resolve();
          dialogRef?.close();
          await dialogClosed;
          if (this.actionRequestId === requestId && this.projectId() === projectId) {
            await this.load(projectId);
            if (this.actionRequestId === requestId && this.projectId() === projectId) {
              this.focusConflictStatus();
            }
          }
        } else {
          this.actionError.set(true);
        }
      }
    } finally {
      if (!this.destroyRef.destroyed && this.actionRequestId === requestId) this.saving.set(false);
    }
  }

  protected async applyFix(finding: ProjectValidationFindingResponse): Promise<void> {
    const projectId = this.projectId();
    const validation = this.validation();
    const expectedObjectVersion = parseObjectVersion(finding.objectVersion);
    if (!validation || this.saving()) {
      return;
    }
    if (!this.canEditModel()) {
      this.actionError.set(true);
      return;
    }
    if (!this.canApplyFix(finding) || expectedObjectVersion === null) return;
    const subjectType = parseActionableSubjectType(finding.subjectType);
    if (!subjectType) {
      this.actionError.set(true);
      return;
    }

    const requestId = ++this.actionRequestId;
    this.actionError.set(false);
    this.actionConflict.set(false);
    this.actionSaved.set(false);
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.projects.applyFindingFix(projectId, finding.code, {
          subjectId: finding.subjectId,
          subjectType,
          environmentId: finding.environmentId,
          expectedObjectVersion,
          detailKey: finding.detailKey,
        }),
      );
      if (
        this.destroyRef.destroyed ||
        this.actionRequestId !== requestId ||
        this.projectId() !== projectId
      )
        return;
      this.actionSaved.set(true);
      await this.load(projectId);
      if (this.projectId() === projectId && this.actionRequestId === requestId) {
        this.focusSavedStatus();
      }
    } catch (error) {
      if (
        !this.destroyRef.destroyed &&
        this.actionRequestId === requestId &&
        this.projectId() === projectId
      ) {
        if (isVersionConflict(error)) {
          this.actionError.set(false);
          this.actionConflict.set(true);
          await this.load(projectId);
          if (this.actionRequestId === requestId && this.projectId() === projectId) {
            this.focusConflictStatus();
          }
        } else {
          this.actionError.set(true);
        }
      }
    } finally {
      if (!this.destroyRef.destroyed && this.actionRequestId === requestId) this.saving.set(false);
    }
  }

  private async load(projectId: string): Promise<void> {
    if (this.destroyRef.destroyed) return;
    const requestId = ++this.loadRequestId;
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [validation, permissions] = await Promise.all([
        firstValueFrom(this.projects.validation(projectId)),
        firstValueFrom(this.projects.myPermissions(projectId)),
      ]);
      if (
        !this.destroyRef.destroyed &&
        this.projectId() === projectId &&
        this.loadRequestId === requestId
      ) {
        this.validation.set(validation);
        this.permissions.set(permissions.permissions);
      }
    } catch {
      if (
        !this.destroyRef.destroyed &&
        this.projectId() === projectId &&
        this.loadRequestId === requestId
      )
        this.loadError.set(true);
    } finally {
      if (
        !this.destroyRef.destroyed &&
        this.projectId() === projectId &&
        this.loadRequestId === requestId
      )
        this.loading.set(false);
    }
  }

  private focusSavedStatus(): void {
    if (this.destroyRef.destroyed) return;
    this.changeDetector.detectChanges();
    this.actionSavedStatus?.nativeElement.focus();
  }

  private focusConflictStatus(): void {
    if (this.destroyRef.destroyed) return;
    this.changeDetector.detectChanges();
    this.actionConflictStatus?.nativeElement.focus();
  }
}
