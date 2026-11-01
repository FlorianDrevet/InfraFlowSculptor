import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  SimpleChanges,
  signal,
} from '@angular/core';
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
  imports: [DsButton, TranslocoPipe],
  templateUrl: './resource-identity-panel.html',
  styleUrl: './resource-identity-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceIdentityPanel implements OnChanges {
  readonly projectId = input.required<string>();
  readonly component = input.required<ComponentResponse>();
  readonly resource = input.required<ResourceResponse>();
  readonly canEdit = input(false);

  protected readonly links = signal<ResourceLinksResponse | null>(null);
  protected readonly currentComponent = signal<ComponentResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  private readonly savingTargetKeys = signal<ReadonlySet<string>>(new Set());
  protected readonly saving = computed(() =>
    this.savingTargetKeys().has(
      this.targetKey(this.projectId(), this.component().id, this.resource().id),
    ),
  );
  protected readonly saveError = signal(false);
  protected readonly defaultIdentityId = signal('');
  protected readonly savedDefaultIdentityId = signal('');
  private loadedTarget: { projectId: string; componentId: string; resourceId: string } | null =
    null;
  private loadRequestId = 0;
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
    () =>
      this.identities().find((identity) => identity.id === this.savedDefaultIdentityId()) ?? null,
  );

  private readonly resources = inject(ResourcesService);
  private readonly components = inject(ComponentsService);
  private readonly transloco = inject(TranslocoService);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId'] || changes['component'] || changes['resource']) {
      void this.load();
    }
  }

  protected async saveDefaultIdentity(): Promise<void> {
    if (
      this.saving() ||
      this.loading() ||
      this.loadError() ||
      !this.canEdit() ||
      !this.currentComponent()
    )
      return;
    const projectId = this.projectId();
    const componentId = this.component().id;
    const resourceId = this.resource().id;
    const component = this.currentComponent()!;
    const currentResource = component.resources.find((candidate) => candidate.id === resourceId);
    if (!currentResource) {
      this.saveError.set(true);
      return;
    }
    const targetKey = this.targetKey(projectId, componentId, resourceId);
    this.savingTargetKeys.update((keys) => new Set([...keys, targetKey]));
    this.saveError.set(false);
    try {
      await firstValueFrom(
        this.resources.setDefaultIdentity(projectId, componentId, resourceId, {
          expectedVersion: currentResource.version,
          identityResourceId: this.defaultIdentityId() || null,
        }),
      );
      if (this.isCurrentTarget(projectId, componentId, resourceId)) {
        this.savedDefaultIdentityId.set(this.defaultIdentityId());
        await this.load();
      }
    } catch {
      if (this.isCurrentTarget(projectId, componentId, resourceId)) {
        this.saveError.set(true);
        await this.load();
      }
    } finally {
      this.savingTargetKeys.update((keys) => {
        const updated = new Set(keys);
        updated.delete(targetKey);
        return updated;
      });
    }
  }

  protected identityName(resourceId: string | null): string {
    if (!resourceId) return '';
    return (
      this.identities().find((identity) => identity.id === resourceId)?.logicalName ?? resourceId
    );
  }

  protected selectDefaultIdentity(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) this.defaultIdentityId.set(target.value);
  }

  protected retryLoad(): void {
    if (!this.loading()) void this.load();
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
    const requestId = ++this.loadRequestId;
    const projectId = this.projectId();
    const componentId = this.component().id;
    const resourceId = this.resource().id;
    const sameTarget =
      this.loadedTarget?.projectId === projectId &&
      this.loadedTarget.componentId === componentId &&
      this.loadedTarget.resourceId === resourceId;
    const preserveSelection =
      sameTarget && this.defaultIdentityId() !== this.savedDefaultIdentityId();
    this.loadedTarget = { projectId, componentId, resourceId };
    if (!sameTarget) {
      this.saveError.set(false);
      const inputDefaultIdentityId = this.resource().defaultIdentityResourceId ?? '';
      this.defaultIdentityId.set(inputDefaultIdentityId);
      this.savedDefaultIdentityId.set(inputDefaultIdentityId);
    }
    this.loading.set(true);
    this.loadError.set(false);
    this.currentComponent.set(null);
    this.links.set(null);
    try {
      const [component, links] = await Promise.all([
        firstValueFrom(this.components.get(projectId, componentId)),
        firstValueFrom(this.resources.links(projectId, componentId, resourceId)),
      ]);
      if (requestId !== this.loadRequestId) return;
      this.currentComponent.set(component);
      const currentResource = component.resources.find((resource) => resource.id === resourceId);
      if (!currentResource) {
        this.currentComponent.set(null);
        this.defaultIdentityId.set('');
        this.savedDefaultIdentityId.set('');
        this.loadError.set(true);
        return;
      }
      const identityId = currentResource.defaultIdentityResourceId;
      const currentIdentity = component.resources.find(
        (resource) => resource.id === identityId && resource.type === 'UserAssignedIdentity',
      );
      const savedIdentityId = currentIdentity?.id ?? '';
      this.savedDefaultIdentityId.set(savedIdentityId);
      const selectedIdentityStillExists = component.resources.some(
        (resource) =>
          resource.id === this.defaultIdentityId() && resource.type === 'UserAssignedIdentity',
      );
      if (!preserveSelection || !selectedIdentityStillExists)
        this.defaultIdentityId.set(savedIdentityId);
      this.links.set(links);
    } catch {
      if (requestId === this.loadRequestId) this.loadError.set(true);
    } finally {
      if (requestId === this.loadRequestId) this.loading.set(false);
    }
  }

  private isCurrentTarget(projectId: string, componentId: string, resourceId: string): boolean {
    return (
      this.projectId() === projectId &&
      this.component().id === componentId &&
      this.resource().id === resourceId
    );
  }

  private targetKey(projectId: string, componentId: string, resourceId: string): string {
    return `${projectId}/${componentId}/${resourceId}`;
  }
}
