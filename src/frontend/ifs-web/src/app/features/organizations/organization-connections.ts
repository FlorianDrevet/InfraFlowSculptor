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
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { GitConnectionsService } from '../../core/git-connections/git-connections.service';
import { ProjectsService } from '../../core/projects/projects.service';
import type {
  CreateGitConnectionRequest,
  GitConnectionAuthenticationMode,
  GitConnectionResponse,
  GitConnectionRepositoryResponse,
  GitProviderAvailabilityResponse,
  GitProviderKind,
  ProjectSummaryResponse,
  SetGitConnectionRepositoryAccessRequest,
} from '../../core/api/generated';

interface RepositoryAccessDraft {
  openToAllProjects: boolean;
  projectIds: string[];
}

@Component({
  selector: 'app-organization-connections',
  imports: [DsButton, FormsModule, TranslocoPipe],
  templateUrl: './organization-connections.html',
  styleUrls: ['./organization-connections.scss', './organization-connections-form.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationConnections implements OnInit {
  readonly id = input.required<string>();
  private readonly connectionsApi = inject(GitConnectionsService);
  private readonly projectsApi = inject(ProjectsService);
  @ViewChild('removeDialog') private removeDialog?: ElementRef<HTMLDialogElement>;

  protected readonly providers = signal<GitProviderAvailabilityResponse[]>([]);
  protected readonly connections = signal<GitConnectionResponse[]>([]);
  protected readonly projects = signal<ProjectSummaryResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal('');
  protected readonly actionSuccess = signal('');
  protected readonly saving = signal(false);
  protected readonly editingConnectionId = signal<string | null>(null);
  protected readonly showForm = signal(false);
  protected readonly accessDrafts = signal<Record<string, RepositoryAccessDraft>>({});
  protected readonly pendingRemoval = signal<GitConnectionResponse | null>(null);
  protected readonly formProvider = signal<GitProviderKind>('AzureRepos');
  protected readonly formName = signal('');
  protected readonly formOrganization = signal('');
  protected readonly formTenantId = signal('');
  protected readonly formMode = signal<GitConnectionAuthenticationMode>(
    'FederatedServicePrincipal',
  );
  protected readonly formToken = signal('');
  protected readonly formExpiresOn = signal('');

  protected readonly isGitea = computed(() => this.formProvider() === 'Gitea');
  protected readonly usesPersonalAccessToken = computed(
    () => this.formMode() === 'PersonalAccessToken',
  );
  protected readonly isEditing = computed(() => this.editingConnectionId() !== null);

  ngOnInit(): void {
    void this.load();
  }

  protected beginCreate(): void {
    this.actionError.set('');
    this.actionSuccess.set('');
    this.editingConnectionId.set(null);
    this.formProvider.set(this.providers()[0]?.kind ?? 'AzureRepos');
    this.formName.set('');
    this.formOrganization.set('');
    this.formTenantId.set('');
    this.formMode.set(
      this.providers()[0]?.kind === 'Gitea' ? 'DevelopmentToken' : 'FederatedServicePrincipal',
    );
    this.formToken.set('');
    this.formExpiresOn.set('');
    this.showForm.set(true);
  }

  protected beginEdit(connection: GitConnectionResponse): void {
    this.actionError.set('');
    this.actionSuccess.set('');
    this.editingConnectionId.set(connection.id);
    this.formProvider.set(connection.providerKind);
    this.formName.set(connection.name);
    this.formOrganization.set(connection.organization);
    this.formTenantId.set(connection.tenantId ?? '');
    this.formMode.set(connection.authenticationMode);
    this.formToken.set('');
    this.formExpiresOn.set(connection.personalAccessTokenExpiresAt?.slice(0, 10) ?? '');
    this.showForm.set(true);
  }

  protected cancelForm(): void {
    this.showForm.set(false);
    this.editingConnectionId.set(null);
    this.formToken.set('');
    this.actionError.set('');
  }

  protected providerChanged(kind: string): void {
    const providerKind = kind as GitProviderKind;
    this.formProvider.set(providerKind);
    this.formMode.set(providerKind === 'Gitea' ? 'DevelopmentToken' : 'FederatedServicePrincipal');
    this.formTenantId.set('');
    this.formToken.set('');
    this.formExpiresOn.set('');
  }

  protected authenticationChanged(mode: string): void {
    this.formMode.set(mode as GitConnectionAuthenticationMode);
    this.formToken.set('');
    this.formExpiresOn.set('');
  }

  protected async saveConnection(): Promise<void> {
    if (this.saving()) {
      return;
    }

    this.actionError.set('');
    this.actionSuccess.set('');
    const expiresAt = this.usesPersonalAccessToken() ? this.expirationIso() : null;
    if (
      !this.formName().trim() ||
      !this.formOrganization().trim() ||
      (this.formMode() === 'FederatedServicePrincipal' && !this.formTenantId().trim()) ||
      (this.usesPersonalAccessToken() &&
        ((!this.formToken().trim() && !this.isEditing()) || !expiresAt))
    ) {
      this.actionError.set('gitConnections.formError');
      return;
    }

    this.saving.set(true);
    try {
      if (this.editingConnectionId()) {
        const current = this.connections().find(
          (connection) => connection.id === this.editingConnectionId(),
        );
        if (!current) {
          throw new Error('The Git connection is no longer available.');
        }
        const updated = await firstValueFrom(
          this.connectionsApi.update(this.id(), current.id, {
            expectedVersion: Number(current.version),
            name: this.formName().trim(),
            organization: this.formOrganization().trim(),
            tenantId:
              this.formMode() === 'FederatedServicePrincipal' ? this.formTenantId().trim() : null,
            personalAccessTokenExpiresAt: expiresAt,
            personalAccessToken: this.formToken().trim() || null,
          }),
        );
        this.upsertConnection(updated);
      } else {
        const request: CreateGitConnectionRequest = {
          providerKind: this.formProvider(),
          name: this.formName().trim(),
          organization: this.formOrganization().trim(),
          tenantId:
            this.formMode() === 'FederatedServicePrincipal' ? this.formTenantId().trim() : null,
          authenticationMode: this.formMode(),
          personalAccessTokenExpiresAt: expiresAt,
          personalAccessToken: this.usesPersonalAccessToken() ? this.formToken().trim() : null,
        };
        const created = await firstValueFrom(this.connectionsApi.create(this.id(), request));
        this.upsertConnection(created);
      }
      this.showForm.set(false);
      this.formToken.set('');
      this.actionSuccess.set('gitConnections.saveSuccess');
    } catch (error) {
      this.setRequestError(error, 'gitConnections.saveError');
    } finally {
      this.saving.set(false);
    }
  }

  protected async testConnection(connection: GitConnectionResponse): Promise<void> {
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.actionError.set('');
    this.actionSuccess.set('');
    try {
      const tested = await firstValueFrom(this.connectionsApi.test(this.id(), connection.id));
      this.upsertConnection(tested);
      this.actionSuccess.set('gitConnections.testSuccess');
    } catch (error) {
      this.setRequestError(error, 'gitConnections.testError');
    } finally {
      this.saving.set(false);
    }
  }

  protected async saveRepositoryAccess(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
  ): Promise<void> {
    if (this.saving()) {
      return;
    }

    const draft = this.getAccessDraft(connection, repository);
    const request: SetGitConnectionRepositoryAccessRequest = {
      expectedVersion: Number(connection.version),
      providerRepositoryId: repository.providerRepositoryId,
      openToAllProjects: draft.openToAllProjects,
      projectIds: draft.openToAllProjects ? [] : draft.projectIds,
    };
    this.saving.set(true);
    this.actionError.set('');
    this.actionSuccess.set('');
    try {
      const updated = await firstValueFrom(
        this.connectionsApi.setRepositoryAccess(this.id(), connection.id, request),
      );
      this.upsertConnection(updated);
      this.actionSuccess.set('gitConnections.accessSaved');
    } catch (error) {
      this.setRequestError(error, 'gitConnections.accessError');
    } finally {
      this.saving.set(false);
    }
  }

  protected requestRemoval(connection: GitConnectionResponse): void {
    this.pendingRemoval.set(connection);
    this.actionError.set('');
    this.removeDialog?.nativeElement.showModal();
  }

  protected cancelRemoval(): void {
    if (this.removeDialog?.nativeElement.open) {
      this.removeDialog.nativeElement.close();
    }
    this.pendingRemoval.set(null);
  }

  protected async confirmRemoval(): Promise<void> {
    const connection = this.pendingRemoval();
    if (!connection || this.saving()) {
      return;
    }

    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.actionError.set('');
    this.actionSuccess.set('');
    try {
      await firstValueFrom(this.connectionsApi.delete(this.id(), connection.id));
      this.connections.update((current) => current.filter((item) => item.id !== connection.id));
      this.actionSuccess.set('gitConnections.removeSuccess');
      this.cancelRemoval();
    } catch (error) {
      this.setRequestError(error, 'gitConnections.removeError');
    } finally {
      this.saving.set(false);
    }
  }

  protected draftFor(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
  ): RepositoryAccessDraft {
    return this.getAccessDraft(connection, repository);
  }

  protected toggleProject(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
    projectId: string,
    event: Event,
  ): void {
    const checked = (event.target as HTMLInputElement).checked;
    const draft = this.getAccessDraft(connection, repository);
    this.setAccessDraft(connection, repository, {
      openToAllProjects: false,
      projectIds: checked
        ? [...new Set([...draft.projectIds, projectId])]
        : draft.projectIds.filter((id) => id !== projectId),
    });
  }

  protected setOpenToAllProjects(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
    event: Event,
  ): void {
    const checked = (event.target as HTMLInputElement).checked;
    const draft = this.getAccessDraft(connection, repository);
    this.setAccessDraft(connection, repository, {
      ...draft,
      openToAllProjects: checked,
      projectIds: checked ? [] : draft.projectIds,
    });
  }

  protected providerLabel(kind: GitProviderKind): string {
    return this.providers().find((provider) => provider.kind === kind)?.displayName ?? kind;
  }

  protected accessSummary(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
  ): string {
    const draft = this.getAccessDraft(connection, repository);
    if (draft.openToAllProjects) {
      return 'gitConnections.allProjects';
    }
    return draft.projectIds.length === 0
      ? 'gitConnections.noProjects'
      : 'gitConnections.selectedProjects';
  }

  protected expirationDays(connection: GitConnectionResponse): number | null {
    if (!connection.personalAccessTokenExpiresAt) {
      return null;
    }
    const date = new Date(connection.personalAccessTokenExpiresAt);
    const today = new Date();
    const utcToday = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
    const utcExpiry = Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
    return Math.ceil((utcExpiry - utcToday) / 86_400_000);
  }

  protected hasExpiryWarning(connection: GitConnectionResponse): boolean {
    const days = this.expirationDays(connection);
    return days !== null && days >= 0 && days <= 15;
  }

  private async load(): Promise<void> {
    try {
      const [providers, connections, projects] = await Promise.all([
        firstValueFrom(this.connectionsApi.providers(this.id())),
        firstValueFrom(this.connectionsApi.list(this.id())),
        firstValueFrom(this.projectsApi.list()),
      ]);
      this.providers.set(providers);
      this.connections.set(connections);
      this.projects.set(projects);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private expirationIso(): string | null {
    const value = this.formExpiresOn();
    return value ? new Date(`${value}T23:59:59.999Z`).toISOString() : null;
  }

  private getAccessDraft(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
  ): RepositoryAccessDraft {
    const existing = this.accessDrafts()[this.accessKey(connection, repository)];
    return (
      existing ?? {
        openToAllProjects: repository.openToAllProjects,
        projectIds: [...repository.allowedProjectIds],
      }
    );
  }

  private setAccessDraft(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
    draft: RepositoryAccessDraft,
  ): void {
    const key = this.accessKey(connection, repository);
    this.accessDrafts.update((current) => ({ ...current, [key]: draft }));
  }

  private accessKey(
    connection: GitConnectionResponse,
    repository: GitConnectionRepositoryResponse,
  ): string {
    return `${connection.id}:${repository.providerRepositoryId}`;
  }

  private upsertConnection(connection: GitConnectionResponse): void {
    this.connections.update((current) => {
      const exists = current.some((item) => item.id === connection.id);
      return exists
        ? current.map((item) => (item.id === connection.id ? connection : item))
        : [...current, connection];
    });
  }

  private setRequestError(error: unknown, fallbackKey: string): void {
    const detail = this.problemDetail(error);
    this.actionError.set(detail || fallbackKey);
  }

  private problemDetail(error: unknown): string {
    if (typeof error !== 'object' || error === null || !('error' in error)) {
      return '';
    }
    const body = (error as { error?: unknown }).error;
    if (typeof body !== 'object' || body === null || !('detail' in body)) {
      return '';
    }
    const detail = (body as { detail?: unknown }).detail;
    return typeof detail === 'string' ? detail : '';
  }
}
