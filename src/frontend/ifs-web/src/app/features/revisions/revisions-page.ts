import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  OnDestroy,
  OnInit,
  signal,
  SimpleChanges,
} from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import type {
  ProjectValidationFindingResponse,
  ProjectValidationResponse,
  RevisionSummaryResponse,
} from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { RevisionsService } from '../../core/revisions/revisions.service';
import { DsBadge } from '../../ds/badge/badge';
import { DsBanner } from '../../ds/banner/banner';
import { DsButton } from '../../ds/button/button';
import { DsPanel } from '../../ds/panel/panel';
import { DsTable } from '../../ds/table/table';

@Component({
  selector: 'app-revisions-page',
  standalone: true,
  imports: [CdkTrapFocus, DsBadge, DsBanner, DsButton, DsPanel, DsTable, RouterLink, TranslocoPipe],
  templateUrl: './revisions-page.html',
  styleUrl: './revisions-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RevisionsPage implements OnChanges, OnInit, OnDestroy {
  readonly projectId = input.required<string>();
  protected readonly revisions = signal<RevisionSummaryResponse[]>([]);
  protected readonly validation = signal<ProjectValidationResponse | null>(null);
  protected readonly nextNumber = signal(1);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly generating = signal(false);
  protected readonly generateError = signal(false);
  protected readonly notice = signal<string | null>(null);
  protected readonly blockingFindings = computed(
    () => this.validation()?.findings.filter((finding) => finding.severity === 'Error') ?? [],
  );
  protected readonly nonBlockingFindings = computed(
    () => this.validation()?.findings.filter((finding) => finding.severity !== 'Error') ?? [],
  );
  protected readonly isBlocked = computed(() =>
    this.validation() ? !this.validation()!.readiness.revisionGenerable : false,
  );
  private readonly revisionsService = inject(RevisionsService);
  private readonly projectsService = inject(ProjectsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);
  private polling?: Subscription;

  ngOnInit(): void {
    void this.load();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId'] && !changes['projectId'].firstChange) {
      this.polling?.unsubscribe();
      this.revisions.set([]);
      this.validation.set(null);
      void this.load();
    }
  }

  ngOnDestroy(): void {
    this.polling?.unsubscribe();
  }

  protected async generate(): Promise<void> {
    if (this.generating() || this.isBlocked()) return;
    this.generating.set(true);
    this.generateError.set(false);
    this.notice.set(null);
    try {
      const result = await firstValueFrom(this.revisionsService.generate(this.projectId()));
      if (result.isUnchanged) {
        this.notice.set(this.transloco.translate('revisions.noChanges', { number: result.number }));
      } else if (result.queuePosition !== null) {
        this.notice.set(
          this.transloco.translate('revisions.queued', {
            number: result.number,
            position: result.queuePosition,
          }),
        );
      } else {
        this.notice.set(this.transloco.translate('revisions.generationRequested'));
      }
      await this.load(false);
      if (result.state === 'Queued' || result.state === 'Generating') this.startPolling();
    } catch {
      this.generateError.set(true);
      await this.load(false);
    } finally {
      this.generating.set(false);
    }
  }

  protected stateKey(revision: RevisionSummaryResponse): string {
    const keys: Record<string, string> = {
      Queued: 'revisions.stateQueued',
      Generating: 'revisions.stateGenerating',
      Generated: 'revisions.stateGenerated',
      Failed: 'revisions.stateFailed',
    };
    return keys[revision.state] ?? 'revisions.stateUnknown';
  }

  protected stateTone(
    revision: RevisionSummaryResponse,
  ): 'signal' | 'success' | 'danger' | 'neutral' {
    const tones: Record<string, 'signal' | 'success' | 'danger' | 'neutral'> = {
      Queued: 'signal',
      Generating: 'signal',
      Generated: 'success',
      Failed: 'danger',
    };
    return tones[revision.state] ?? 'neutral';
  }

  protected number(value: number | string): number {
    return Number(value);
  }

  protected date(value: string): string {
    return new Intl.DateTimeFormat(this.transloco.getActiveLang(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  protected generatedDate(revision: RevisionSummaryResponse): string {
    return this.date(revision.completedAt ?? revision.createdAt);
  }

  protected changeCountLabel(count: number | string): string {
    const key = Number(count) === 1 ? 'revisions.changeCountOne' : 'revisions.changeCountMany';
    return this.transloco.translate(key, { count });
  }

  protected findingMessage(finding: ProjectValidationFindingResponse): string {
    return this.transloco.getActiveLang() === 'fr' ? finding.messageFr : finding.messageEn;
  }

  protected blockedSummaryKey(): string {
    return this.blockingFindings().length === 1
      ? 'revisions.blockedSummaryOne'
      : 'revisions.blockedSummaryMany';
  }

  protected closeBlocked(): void {
    void this.router.navigate(['../'], { relativeTo: this.route });
  }

  protected viewFindings(): void {
    void this.router.navigate(['../findings'], { relativeTo: this.route });
  }

  protected isPending(revision: RevisionSummaryResponse): boolean {
    return revision.state === 'Queued' || revision.state === 'Generating';
  }

  private async load(showLoading = true): Promise<void> {
    const projectId = this.projectId();
    if (showLoading) {
      this.loading.set(true);
      this.loadError.set(false);
    }
    try {
      const [revisionList, validation] = await Promise.all([
        firstValueFrom(this.revisionsService.list(projectId)),
        firstValueFrom(this.projectsService.validation(projectId)),
      ]);
      this.revisions.set(revisionList.items);
      this.nextNumber.set(Number(revisionList.nextNumber));
      this.validation.set(validation);
      if (revisionList.items.some((revision) => this.isPending(revision))) this.startPolling();
    } catch {
      if (showLoading) this.loadError.set(true);
    } finally {
      if (showLoading) this.loading.set(false);
    }
  }

  private startPolling(): void {
    this.polling?.unsubscribe();
    this.polling = timer(2000, 3000)
      .pipe(
        switchMap(() => this.revisionsService.list(this.projectId())),
        takeWhile(
          (revisionList) => revisionList.items.some((revision) => this.isPending(revision)),
          true,
        ),
      )
      .subscribe({
        next: (revisionList) => {
          this.revisions.set(revisionList.items);
          this.nextNumber.set(Number(revisionList.nextNumber));
        },
        error: () => this.loadError.set(true),
      });
  }
}
