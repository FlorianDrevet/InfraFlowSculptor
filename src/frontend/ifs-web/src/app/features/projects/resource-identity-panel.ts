import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ComponentResponse,
  ResourceLinkResponse,
  ResourceLinksResponse,
  ResourceResponse,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';

@Component({
  selector: 'app-resource-identity-panel',
  imports: [DsButton, FormsModule, TranslocoPipe],
  templateUrl: './resource-identity-panel.html',
  styleUrl: './resource-identity-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceIdentityPanel implements OnInit {
  readonly projectId = input.required<string>();
  readonly component = input.required<ComponentResponse>();
  readonly resource = input.required<ResourceResponse>();
  readonly canEdit = input(false);

  protected readonly links = signal<ResourceLinksResponse | null>(null);
  protected readonly currentComponent = signal<ComponentResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal(false);
  protected readonly defaultIdentityId = signal('');
  protected readonly outgoingIdentityLinks = computed(
    () => this.links()?.outgoing.filter((link) => link.identityKind !== 'None') ?? [],
  );
  protected readonly identities = computed(() =>
    (this.currentComponent() ?? this.component()).resources.filter(
      (resource) => resource.type === 'UserAssignedIdentity',
    ),
  );
  protected readonly systemIdentityUsed = computed(
    () => this.links()?.outgoing.some((link) => link.identityKind === 'System') ?? false,
  );
  protected readonly outgoingRoleAssignments = computed(
    () =>
      this.links()?.outgoing.flatMap((link) => link.roles.map((role) => ({ role, link }))) ?? [],
  );
  protected readonly incomingRoleAssignments = computed(
    () =>
      this.links()?.incoming.flatMap((link) => link.roles.map((role) => ({ role, link }))) ?? [],
  );
  protected readonly currentDefaultIdentity = computed(
    () => this.identities().find((identity) => identity.id === this.defaultIdentityId()) ?? null,
  );

  private readonly resources = inject(ResourcesService);
  private readonly components = inject(ComponentsService);
  private readonly transloco = inject(TranslocoService);

  ngOnInit(): void {
    this.defaultIdentityId.set(this.resource().defaultIdentityResourceId ?? '');
    void this.load();
  }

  protected async saveDefaultIdentity(): Promise<void> {
    if (this.saving() || !this.canEdit()) return;
    this.saving.set(true);
    this.saveError.set(false);
    try {
      await firstValueFrom(
        this.resources.setDefaultIdentity(
          this.projectId(),
          this.component().id,
          this.resource().id,
          {
            expectedVersion: (this.currentComponent() ?? this.component()).version,
            identityResourceId: this.defaultIdentityId() || null,
          },
        ),
      );
      await this.load();
    } catch {
      this.saveError.set(true);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  protected identityName(resourceId: string | null): string {
    if (!resourceId) return '';
    return (
      this.identities().find((identity) => identity.id === resourceId)?.logicalName ?? resourceId
    );
  }

  protected originLabel(link: ResourceLinkResponse): string {
    if (link.origin === 'Explicit') return this.transloco.translate('resource.explicitLinkOrigin');
    if (link.originSource?.kind === 'ApplicationSetting') {
      return this.transloco.translate('resource.linkOriginAppSetting', {
        name: link.originSource.name ?? '',
      });
    }
    if (link.originSource?.kind === 'ComponentDefaultLogAnalytics') {
      return this.transloco.translate('resource.linkOriginDefaultLogging', {
        name: link.originSource.name ?? '',
      });
    }
    return this.transloco.translate('resource.implicitLinkOrigin');
  }

  protected targetLabel(link: ResourceLinkResponse, outgoing: boolean): string {
    return outgoing
      ? `${link.targetComponentCode} / ${link.targetResourceName}`
      : `${link.sourceComponentCode} / ${link.sourceResourceName}`;
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [component, links] = await Promise.all([
        firstValueFrom(this.components.get(this.projectId(), this.component().id)),
        firstValueFrom(
          this.resources.links(this.projectId(), this.component().id, this.resource().id),
        ),
      ]);
      this.currentComponent.set(component);
      const currentResource = component.resources.find(
        (resource) => resource.id === this.resource().id,
      );
      if (currentResource)
        this.defaultIdentityId.set(currentResource.defaultIdentityResourceId ?? '');
      this.links.set(links);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
