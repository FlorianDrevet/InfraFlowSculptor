import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ProjectPublishPlanResponse,
  PublishPlanAvailableDestinationResponse,
  PublishPlanDestinationResponse,
  PublishPlanRowResponse,
  SetProjectPublishPlanRequest,
} from '../../core/api/generated';
import { ProjectPublishPlanService } from '../../core/projects/project-publish-plan.service';
import { DsBanner } from '../../ds/banner/banner';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';

type RepositoryOption = {
  key: string;
  gitConnectionId: string;
  providerRepositoryId: string;
  connectionName: string;
  repositoryName: string;
  basePath: string;
};

type RowDraft = {
  optionKey: string;
  basePath: string;
};

@Component({
  selector: 'app-project-publishing',
  imports: [DsBanner, DsButton, DsTextField, TranslocoPipe],
  templateUrl: './project-publishing.html',
  styleUrl: './project-publishing.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectPublishing {
  readonly projectId = input.required<string>();
  private readonly publication = inject(ProjectPublishPlanService);

  protected readonly plan = signal<ProjectPublishPlanResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal(false);
  protected readonly hasSaved = signal(false);
  protected readonly hasChanges = signal(false);
  protected readonly defaultOptionKey = signal('');
  protected readonly defaultBasePath = signal('');
  protected readonly rowDrafts = signal<Record<string, RowDraft>>({});

  protected readonly repositoryOptions = computed<RepositoryOption[]>(() => {
    const current = this.plan();
    if (!current) return [];
    const options = new Map<string, RepositoryOption>();
    for (const destination of current.destinations) {
      const option = this.optionFromDestination(destination);
      options.set(this.repositoryIdentity(option), option);
    }
    for (const destination of current.availableDestinations) {
      const option = this.optionFromAvailable(destination);
      const identity = this.repositoryIdentity(option);
      if (!options.has(identity)) options.set(identity, option);
    }
    return [...options.values()].sort((first, second) =>
      `${first.connectionName}/${first.repositoryName}`.localeCompare(
        `${second.connectionName}/${second.repositoryName}`,
      ),
    );
  });

  protected readonly selectedDefaultLabel = computed(() => {
    const selected = this.repositoryOptions().find(option => option.key === this.defaultOptionKey());
    return selected ? `${selected.connectionName}/${selected.repositoryName}` : '—';
  });

  constructor() {
    effect(() => {
      const projectId = this.projectId();
      if (projectId) void this.load(projectId);
    });
  }

  protected rowKey(row: PublishPlanRowResponse): string {
    return `${row.componentId}\u001f${row.part}`;
  }

  protected rowDestinationSelection(row: PublishPlanRowResponse): string {
    return this.rowDrafts()[this.rowKey(row)]?.optionKey ?? '';
  }

  protected rowBasePath(row: PublishPlanRowResponse): string {
    return this.rowDrafts()[this.rowKey(row)]?.basePath ?? '';
  }

  protected partLabel(part: string): string {
    const labels: Record<string, string> = {
      Infrastructure: 'project.publicationInfrastructure',
      Applications: 'project.publicationApplications',
      SourceCode: 'project.publicationSourceCode',
    };
    return labels[part] ?? 'project.publicationSourceCode';
  }

  protected chooseDefaultRepository(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement)) return;
    const priorKey = this.defaultOptionKey();
    const priorPath = this.defaultBasePath();
    const option = this.repositoryOptions().find(candidate => candidate.key === target.value);
    this.defaultOptionKey.set(target.value);
    this.defaultBasePath.set(option?.basePath ?? '');
    this.rowDrafts.update(drafts => Object.fromEntries(
      Object.entries(drafts).map(([key, draft]) => [key,
        draft.optionKey === priorKey && draft.basePath === priorPath
          ? { optionKey: target.value, basePath: option?.basePath ?? '' }
          : draft,
      ]),
    ));
    this.hasChanges.set(true);
  }

  protected updateDefaultBasePath(value: string | undefined): void {
    const priorPath = this.defaultBasePath();
    const basePath = value ?? '';
    this.defaultBasePath.set(basePath);
    const optionKey = this.defaultOptionKey();
    this.rowDrafts.update(drafts => Object.fromEntries(
      Object.entries(drafts).map(([key, draft]) => [key,
        draft.optionKey === optionKey && draft.basePath === priorPath
          ? { ...draft, basePath }
          : draft,
      ]),
    ));
    this.hasChanges.set(true);
  }

  protected chooseRowRepository(row: PublishPlanRowResponse, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLSelectElement)) return;
    const option = this.repositoryOptions().find(candidate => candidate.key === target.value);
    this.rowDrafts.update(drafts => ({
      ...drafts,
      [this.rowKey(row)]: {
        optionKey: target.value,
        basePath: option?.key === this.defaultOptionKey()
          ? this.defaultBasePath()
          : option?.basePath ?? '',
      },
    }));
    this.hasChanges.set(true);
  }

  protected updateRowBasePath(row: PublishPlanRowResponse, value: string | undefined): void {
    const key = this.rowKey(row);
    const draft = this.rowDrafts()[key];
    if (!draft) return;
    this.rowDrafts.update(drafts => ({ ...drafts, [key]: { ...draft, basePath: value ?? '' } }));
    this.hasChanges.set(true);
  }

  protected updateRowBasePathFromEvent(row: PublishPlanRowResponse, event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) this.updateRowBasePath(row, target.value);
  }

  protected async retry(): Promise<void> {
    await this.load(this.projectId());
  }

  protected async save(): Promise<void> {
    const current = this.plan();
    if (!current || this.saving() || !this.hasChanges()) return;

    const destinations = new Map<string, { id: string; gitConnectionId: string; providerRepositoryId: string; basePath: string }>();
    const destinationIdByRow = new Map<string, string>();
    const addDestination = (optionKey: string, rawBasePath: string): string | undefined => {
      const option = this.repositoryOptions().find(candidate => candidate.key === optionKey);
      if (!option) return undefined;
      const basePath = this.normalizeBasePath(rawBasePath);
      const destinationKey = `${option.gitConnectionId}\u001f${option.providerRepositoryId}\u001f${basePath}`;
      const existing = destinations.get(destinationKey);
      if (existing) return existing.id;
      const savedDestination = current.destinations.find(destination =>
        destination.gitConnectionId === option.gitConnectionId
          && destination.providerRepositoryId === option.providerRepositoryId
          && destination.basePath === basePath);
      const id = savedDestination?.id ?? crypto.randomUUID();
      destinations.set(destinationKey, {
        id,
        gitConnectionId: option.gitConnectionId,
        providerRepositoryId: option.providerRepositoryId,
        basePath,
      });
      return id;
    };

    const defaultDestinationId = addDestination(this.defaultOptionKey(), this.defaultBasePath());
    for (const row of current.rows) {
      const draft = this.rowDrafts()[this.rowKey(row)];
      if (!draft) continue;
      const destinationId = addDestination(draft.optionKey, draft.basePath);
      if (destinationId) destinationIdByRow.set(this.rowKey(row), destinationId);
    }

    const assignments = current.rows.flatMap(row => {
      const destinationId = destinationIdByRow.get(this.rowKey(row));
      if (!destinationId || destinationId === defaultDestinationId) return [];
      return [{ componentId: row.componentId, part: row.part, destinationId }];
    });
    const request: SetProjectPublishPlanRequest = {
      preset: 'MonoRepo',
      destinations: [...destinations.values()],
      assignments,
      defaultInfrastructureDestinationId: defaultDestinationId ?? null,
      defaultApplicationsDestinationId: defaultDestinationId ?? null,
      defaultSourceCodeDestinationId: defaultDestinationId ?? null,
      expectedVersion: Number(current.projectVersion),
    };

    this.saving.set(true);
    this.saveError.set(false);
    try {
      await firstValueFrom(this.publication.save(this.projectId(), request));
      this.hasSaved.set(true);
      await this.load(this.projectId());
    } catch {
      this.saveError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(projectId: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const plan = await firstValueFrom(this.publication.get(projectId));
      if (this.projectId() !== projectId) return;
      this.plan.set(plan);
      const defaultDestination = plan.destinations.find(destination =>
        destination.id === plan.defaultInfrastructureDestinationId)
        ?? plan.destinations[0];
      const defaultOption = defaultDestination
        ? this.optionFromDestination(defaultDestination)
        : plan.availableDestinations[0]
          ? this.optionFromAvailable(plan.availableDestinations[0])
          : undefined;
      const defaultOptionKey = defaultOption?.key ?? '';
      const defaultBasePath = defaultDestination?.basePath ?? defaultOption?.basePath ?? '';
      this.defaultOptionKey.set(defaultOptionKey);
      this.defaultBasePath.set(defaultBasePath);

      const drafts: Record<string, RowDraft> = {};
      for (const row of plan.rows) {
        const destination = plan.destinations.find(candidate => candidate.id === row.destinationId);
        const option = destination
          ? this.optionFromDestination(destination)
          : defaultOption;
        drafts[this.rowKey(row)] = {
          optionKey: option?.key ?? '',
          basePath: destination?.basePath ?? defaultBasePath,
        };
      }
      this.rowDrafts.set(drafts);
      this.hasChanges.set(Boolean(defaultOption && !defaultDestination));
    } catch {
      if (this.projectId() === projectId) this.loadError.set(true);
    } finally {
      if (this.projectId() === projectId) this.loading.set(false);
    }
  }

  private optionFromDestination(destination: PublishPlanDestinationResponse): RepositoryOption {
    return {
      key: `destination:${destination.id}`,
      gitConnectionId: destination.gitConnectionId,
      providerRepositoryId: destination.providerRepositoryId,
      connectionName: destination.connectionName,
      repositoryName: destination.repositoryName,
      basePath: destination.basePath,
    };
  }

  private optionFromAvailable(destination: PublishPlanAvailableDestinationResponse): RepositoryOption {
    return {
      key: `repository:${destination.gitConnectionId}\u001f${destination.providerRepositoryId}`,
      gitConnectionId: destination.gitConnectionId,
      providerRepositoryId: destination.providerRepositoryId,
      connectionName: destination.connectionName,
      repositoryName: destination.repositoryName,
      basePath: '',
    };
  }

  private repositoryIdentity(option: RepositoryOption): string {
    return `${option.gitConnectionId}\u001f${option.providerRepositoryId}\u001f${option.basePath}`;
  }

  private normalizeBasePath(basePath: string): string {
    return basePath.trim().replaceAll('\\', '/').replace(/^\/+|\/+$/g, '');
  }
}
