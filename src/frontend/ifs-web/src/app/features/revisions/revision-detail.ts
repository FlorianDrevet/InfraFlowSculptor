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
import { NgTemplateOutlet } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  catchError,
  debounceTime,
  finalize,
  firstValueFrom,
  from,
  map,
  mergeMap,
  of,
  Subject,
  Subscription,
  switchMap,
  takeWhile,
  tap,
  timer,
  toArray,
} from 'rxjs';
import type {
  RevisionDetailResponse,
  RevisionFileResponse,
  RevisionFindingResponse,
} from '../../core/api/generated';
import { RevisionsService } from '../../core/revisions/revisions.service';
import { DsBadge } from '../../ds/badge/badge';
import { DsBanner } from '../../ds/banner/banner';
import { DsButton } from '../../ds/button/button';
import { DsTabs, TabItem } from '../../ds/tabs/tabs';
import { DsTextField } from '../../ds/text-field/text-field';
import { CodeViewer } from '../../shared/code-viewer/code-viewer';
import { buildRevisionFileTree, RevisionFileTreeNode } from './revision-file-tree';

@Component({
  selector: 'app-revision-detail',
  standalone: true,
  imports: [
    CodeViewer,
    DsBadge,
    DsBanner,
    DsButton,
    DsTabs,
    DsTextField,
    NgTemplateOutlet,
    RouterLink,
    TranslocoPipe,
  ],
  templateUrl: './revision-detail.html',
  styleUrl: './revision-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RevisionDetail implements OnChanges, OnInit, OnDestroy {
  readonly projectId = input.required<string>();
  readonly revisionId = input.required<string>();
  protected readonly detail = signal<RevisionDetailResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly activeTab = signal('changes');
  protected readonly plan = signal<string | null>(null);
  protected readonly planLoading = signal(false);
  protected readonly planError = signal(false);
  protected readonly files = signal<RevisionFileResponse[]>([]);
  protected readonly filteredFiles = signal<RevisionFileResponse[]>([]);
  protected readonly selectedPath = signal<string | null>(null);
  protected readonly fileContent = signal<string | null>(null);
  protected readonly fileLoading = signal(false);
  protected readonly fileError = signal(false);
  protected readonly fileSearch = signal('');
  protected readonly searchingFiles = signal(false);
  protected readonly fileSearchError = signal(false);
  protected readonly tree = computed(() => buildRevisionFileTree(this.filteredFiles()));
  protected readonly tabs = computed<TabItem[]>(() => [
    { id: 'changes', label: this.transloco.translate('revisionDetail.changesTab') },
    { id: 'plan', label: this.transloco.translate('revisionDetail.planTab') },
    { id: 'files', label: this.transloco.translate('revisionDetail.filesTab') },
    { id: 'findings', label: this.transloco.translate('revisionDetail.findingsTab') },
  ]);
  protected readonly revision = computed(() => this.detail()?.revision ?? null);
  private readonly service = inject(RevisionsService);
  private readonly transloco = inject(TranslocoService);
  private polling?: Subscription;
  private searchRequestId = 0;
  private readonly searchQuery = new Subject<{ query: string; requestId: number }>();
  private readonly fileContents = new Map<string, string>();

  constructor() {
    this.searchQuery
      .pipe(
        debounceTime(250),
        switchMap(({ query, requestId }) => {
          this.fileSearchError.set(false);
          if (!query.trim()) {
            this.searchingFiles.set(false);
            return of({ requestId, files: this.files() });
          }

          this.searchingFiles.set(true);
          const normalizedQuery = query.trim().toLocaleLowerCase();
          return from(this.files()).pipe(
            mergeMap((file) => {
              if (file.relativePath.toLocaleLowerCase().includes(normalizedQuery)) return of(file);
              const cached = this.fileContents.get(file.relativePath);
              if (cached !== undefined) {
                return of(cached.toLocaleLowerCase().includes(normalizedQuery) ? file : null);
              }
              return this.service
                .readFile(this.projectId(), this.revisionId(), file.relativePath)
                .pipe(
                  tap((content) => this.fileContents.set(file.relativePath, content)),
                  map((content) =>
                    content.toLocaleLowerCase().includes(normalizedQuery) ? file : null,
                  ),
                );
            }, 4),
            toArray(),
            map((matches) => ({
              requestId,
              files: matches.filter((file): file is RevisionFileResponse => file !== null),
            })),
            catchError(() => {
              this.fileSearchError.set(true);
              return of({ requestId, files: [] });
            }),
            finalize(() => this.searchingFiles.set(false)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe(({ requestId, files }) => {
        if (requestId === this.searchRequestId) this.filteredFiles.set(files);
      });
  }

  ngOnInit(): void {
    void this.load();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (
      (changes['projectId'] && !changes['projectId'].firstChange) ||
      (changes['revisionId'] && !changes['revisionId'].firstChange)
    ) {
      this.polling?.unsubscribe();
      this.detail.set(null);
      this.plan.set(null);
      this.files.set([]);
      this.filteredFiles.set([]);
      this.selectedPath.set(null);
      this.fileContent.set(null);
      this.fileContents.clear();
      void this.load();
    }
  }

  ngOnDestroy(): void {
    this.polling?.unsubscribe();
  }

  selectTab(tab: string | undefined): void {
    if (!tab) return;
    this.activeTab.set(tab);
    if (
      tab === 'plan' &&
      !this.plan() &&
      !this.planLoading() &&
      this.revision()?.state === 'Generated'
    ) {
      void this.loadPlan();
    }
    if (tab === 'files' && this.files().length > 0 && !this.selectedPath()) {
      void this.selectFile(this.files()[0]);
    }
  }

  protected statusKey(): string {
    const keys: Record<string, string> = {
      Queued: 'revisions.stateQueued',
      Generating: 'revisions.stateGenerating',
      Generated: 'revisions.stateGenerated',
      Failed: 'revisions.stateFailed',
    };
    return keys[this.revision()?.state ?? ''] ?? 'revisions.stateUnknown';
  }

  protected statusTone(): 'signal' | 'success' | 'danger' | 'neutral' {
    const tones: Record<string, 'signal' | 'success' | 'danger' | 'neutral'> = {
      Queued: 'signal',
      Generating: 'signal',
      Generated: 'success',
      Failed: 'danger',
    };
    return tones[this.revision()?.state ?? ''] ?? 'neutral';
  }

  protected date(value: string | null): string {
    if (!value) return this.transloco.translate('revisionDetail.notAvailable');
    return new Intl.DateTimeFormat(this.transloco.getActiveLang(), {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  protected findingsBySeverity(severity: string): RevisionFindingResponse[] {
    return this.detail()?.findings.filter((finding) => finding.severity === severity) ?? [];
  }

  protected number(value: number | string): number {
    return Number(value);
  }

  protected findingMessage(finding: RevisionFindingResponse): string {
    return this.transloco.getActiveLang() === 'fr' ? finding.messageFr : finding.messageEn;
  }

  protected async selectFile(file: RevisionFileResponse): Promise<void> {
    this.selectedPath.set(file.relativePath);
    this.fileLoading.set(true);
    this.fileError.set(false);
    try {
      const content =
        this.fileContents.get(file.relativePath) ??
        (await firstValueFrom(
          this.service.readFile(this.projectId(), this.revisionId(), file.relativePath),
        ));
      this.fileContents.set(file.relativePath, content);
      this.fileContent.set(content);
    } catch {
      this.fileContent.set(null);
      this.fileError.set(true);
    } finally {
      this.fileLoading.set(false);
    }
  }

  protected searchFiles(value: string | undefined): void {
    const query = value ?? '';
    this.fileSearch.set(value ?? '');
    const requestId = ++this.searchRequestId;
    this.searchQuery.next({ query, requestId });
  }

  protected async downloadSelectedFile(): Promise<void> {
    const path = this.selectedPath();
    if (!path) return;
    const blob = await firstValueFrom(
      this.service.downloadFile(this.projectId(), this.revisionId(), path),
    );
    this.saveBlob(blob, path.split('/').at(-1) ?? path);
  }

  protected async downloadArchive(): Promise<void> {
    const number = this.revision()?.number;
    if (number === undefined) return;
    const blob = await firstValueFrom(
      this.service.downloadArchive(this.projectId(), this.revisionId()),
    );
    this.saveBlob(blob, `revision-${number}.zip`);
  }

  protected isPending(): boolean {
    const state = this.revision()?.state;
    return state === 'Queued' || state === 'Generating';
  }

  protected treeTrack(node: RevisionFileTreeNode): string {
    return node.path;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const detail = await firstValueFrom(this.service.get(this.projectId(), this.revisionId()));
      this.applyDetail(detail);
      if (detail.revision.state === 'Queued' || detail.revision.state === 'Generating')
        this.startPolling();
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadPlan(): Promise<void> {
    this.planLoading.set(true);
    this.planError.set(false);
    try {
      const plan = await firstValueFrom(this.service.plan(this.projectId(), this.revisionId()));
      this.plan.set(JSON.stringify(plan, null, 2));
    } catch {
      this.planError.set(true);
    } finally {
      this.planLoading.set(false);
    }
  }

  private startPolling(): void {
    this.polling?.unsubscribe();
    this.polling = timer(2000, 3000)
      .pipe(
        switchMap(() => this.service.get(this.projectId(), this.revisionId())),
        takeWhile(
          (detail) => detail.revision.state === 'Queued' || detail.revision.state === 'Generating',
          true,
        ),
      )
      .subscribe({
        next: (detail) => this.applyDetail(detail),
        error: () => this.loadError.set(true),
      });
  }

  private applyDetail(detail: RevisionDetailResponse): void {
    this.detail.set(detail);
    this.files.set(detail.files);
    if (!this.fileSearch().trim()) this.filteredFiles.set(detail.files);
    if (detail.revision.state === 'Generated' && this.activeTab() === 'plan' && !this.plan()) {
      void this.loadPlan();
    }
  }

  private saveBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }
}
