import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  OnInit,
  signal,
  SimpleChanges,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { ProjectValidationResponse, RevisionSummaryResponse } from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { RevisionsService } from '../../core/revisions/revisions.service';
import { DsBanner } from '../../ds/banner/banner';
import { DsButton } from '../../ds/button/button';
import { DsGoldenPath, GoldenPathStep } from '../../ds/golden-path/golden-path';

@Component({
  selector: 'app-project-revision-rail',
  standalone: true,
  imports: [DsBanner, DsButton, DsGoldenPath, RouterLink, TranslocoPipe],
  templateUrl: './project-revision-rail.html',
  styleUrl: './project-revision-rail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectRevisionRail implements OnChanges, OnInit {
  readonly projectId = input.required<string>();
  protected readonly revisions = signal<RevisionSummaryResponse[]>([]);
  protected readonly validation = signal<ProjectValidationResponse | null>(null);
  protected readonly nextNumber = signal(1);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly generating = signal(false);
  protected readonly generationError = signal(false);
  protected readonly notice = signal<string | null>(null);
  private readonly service = inject(RevisionsService);
  private readonly projects = inject(ProjectsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);
  private readonly language = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });
  protected readonly latest = computed(() => this.revisions()[0] ?? null);
  protected readonly steps = computed<readonly GoldenPathStep[]>(() => {
    this.language();
    const latest = this.latest();
    const canGenerate = this.validation()?.readiness.revisionGenerable ?? false;
    const generatedState =
      latest?.state === 'Generated' ? (latest.isStale ? 'attention' : 'done') : 'pending';
    return [
      {
        label: this.transloco.translate('project.revisionStepModel'),
        state: canGenerate ? 'done' : this.validation() ? 'error' : 'pending',
        hint: canGenerate ? undefined : this.transloco.translate('project.revisionModelBlocked'),
      },
      {
        label: this.transloco.translate('project.revisionStepGenerate'),
        state:
          latest?.state === 'Queued' || latest?.state === 'Generating'
            ? 'current'
            : latest?.state === 'Failed'
              ? 'error'
              : generatedState,
        meta: latest
          ? this.transloco.translate(this.stateKey(latest), { number: latest.number })
          : this.transloco.translate('project.noRevisionGenerated'),
        hint: latest?.isStale ? this.transloco.translate('revisions.stale') : undefined,
      },
    ];
  });
  protected readonly blocked = computed(() =>
    this.validation() ? !this.validation()!.readiness.revisionGenerable : false,
  );

  ngOnInit(): void {
    void this.load();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId'] && !changes['projectId'].firstChange) void this.load();
  }

  protected async generate(): Promise<void> {
    if (this.generating() || this.blocked()) return;
    this.generating.set(true);
    this.generationError.set(false);
    this.notice.set(null);
    try {
      const result = await firstValueFrom(this.service.generate(this.projectId()));
      if (result.isUnchanged) {
        this.notice.set(this.transloco.translate('revisions.noChanges', { number: result.number }));
        await this.load(false);
      } else {
        await this.router.navigate(['revisions', result.revisionId], { relativeTo: this.route });
      }
    } catch {
      this.generationError.set(true);
    } finally {
      this.generating.set(false);
    }
  }

  private async load(showLoading = true): Promise<void> {
    if (showLoading) this.loading.set(true);
    this.loadError.set(false);
    try {
      const [revisions, validation] = await Promise.all([
        firstValueFrom(this.service.list(this.projectId())),
        firstValueFrom(this.projects.validation(this.projectId())),
      ]);
      this.revisions.set(revisions.items);
      this.nextNumber.set(Number(revisions.nextNumber));
      this.validation.set(validation);
    } catch {
      this.loadError.set(true);
    } finally {
      if (showLoading) this.loading.set(false);
    }
  }

  private stateKey(revision: RevisionSummaryResponse): string {
    const keys: Record<string, string> = {
      Queued: 'revisions.stateQueued',
      Generating: 'revisions.stateGenerating',
      Generated: 'revisions.stateGeneratedNumber',
      Failed: 'revisions.stateFailed',
    };
    return keys[revision.state] ?? 'revisions.stateUnknown';
  }
}
