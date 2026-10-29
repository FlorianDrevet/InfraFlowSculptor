import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  OnInit,
  SimpleChanges,
  signal,
} from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ComponentResponse,
  DataAccessLevel,
  LinkIdentityKind,
  LinkPreviewRequest,
  LinkPreviewResponse,
  ResourceLinkKindOptionResponse,
  ResourceLinkResponse,
  ResourceLinksResponse,
  ResourceResponse,
} from '../../core/api/generated';
import type { LinkImpactResponse } from '../../core/api/generated/models/link-impact-response';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';
import { resourceImpactMessage } from './resource-impact-message';

@Component({
  selector: 'app-resource-links-panel',
  imports: [CdkTrapFocus, DsButton, FormsModule, TranslocoPipe],
  templateUrl: './resource-links-panel.html',
  styleUrl: './resource-links-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceLinksPanel implements OnChanges, OnInit {
  private readonly identityKindTranslationKeys: Record<LinkIdentityKind, string> = {
    None: 'resource.identityKind.None',
    System: 'resource.identityKind.System',
    UserAssigned: 'resource.identityKind.UserAssigned',
  };

  private readonly dataAccessTranslationKeys: Record<DataAccessLevel, string> = {
    Lecture: 'resource.dataAccess.Lecture',
    LectureEcriture: 'resource.dataAccess.LectureEcriture',
    Schema: 'resource.dataAccess.Schema',
  };

  readonly projectId = input.required<string>();
  readonly component = input.required<ComponentResponse>();
  readonly resource = input.required<ResourceResponse>();
  readonly canEdit = input(false);

  protected readonly links = signal<ResourceLinksResponse | null>(null);
  protected readonly currentComponent = signal<ComponentResponse | null>(null);
  protected readonly options = signal<ResourceLinkKindOptionResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly actionError = signal(false);
  protected readonly addOpen = signal(false);
  protected readonly previewBusy = signal(false);
  protected readonly previewError = signal(false);
  protected readonly previewResult = signal<LinkPreviewResponse | null>(null);
  protected readonly selectedKindKey = signal('');
  protected readonly selectedTargetId = signal('');
  protected readonly selectedIdentityKey = signal('');
  protected readonly selectedRoles = signal<string[]>([]);
  protected readonly selectedDataAccess = signal<DataAccessLevel | null>(null);
  protected readonly removal = signal<{
    link: ResourceLinkResponse;
    impact: LinkImpactResponse;
  } | null>(null);
  protected readonly removalBusy = signal(false);
  protected readonly removalError = signal(false);
  protected readonly removalConflict = signal(false);

  private previewSequence = 0;
  private previewRequestKey: string | null = null;
  private previewComponentVersion: number | null = null;

  protected readonly selectedKind = computed(
    () => this.options().find((option) => option.kind === this.selectedKindKey()) ?? null,
  );
  protected readonly selectedTarget = computed(
    () =>
      this.selectedKind()?.targets.find(
        (target) => target.resourceId === this.selectedTargetId(),
      ) ?? null,
  );
  protected readonly selectedIdentity = computed(
    () =>
      this.selectedTarget()?.identities.find(
        (option) => this.identityKey(option.kind, option.resourceId) === this.selectedIdentityKey(),
      ) ?? null,
  );
  protected readonly canPreview = computed(
    () =>
      !!this.selectedKind() &&
      !!this.selectedTarget() &&
      !this.previewBusy() &&
      !this.loading() &&
      !this.loadError() &&
      this.currentComponent() !== null,
  );

  private readonly resources = inject(ResourcesService);
  private readonly components = inject(ComponentsService);
  private readonly transloco = inject(TranslocoService);

  ngOnInit(): void {
    void this.load();
  }

  ngOnChanges(changes: SimpleChanges): void {
    const componentChange = changes['component'];
    if (!componentChange || componentChange.firstChange) return;

    const previousComponent = componentChange.previousValue as ComponentResponse;
    const currentComponent = componentChange.currentValue as ComponentResponse;
    if (
      previousComponent.id !== currentComponent.id ||
      Number(previousComponent.version) !== Number(currentComponent.version)
    )
      this.invalidatePreview();
  }

  protected openAdd(): void {
    this.addOpen.set(true);
    const firstKind = this.options()[0];
    if (firstKind) this.selectKind(firstKind.kind);
  }

  protected closeAdd(): void {
    if (this.saving()) return;
    this.addOpen.set(false);
    this.invalidatePreview();
    this.actionError.set(false);
  }

  protected selectKind(kind: string): void {
    this.selectedKindKey.set(kind);
    this.selectedTargetId.set('');
    this.selectedIdentityKey.set('');
    this.selectedRoles.set([]);
    this.selectedDataAccess.set(null);
    this.invalidatePreview();
    const firstTarget = this.options().find((option) => option.kind === kind)?.targets[0];
    if (firstTarget) this.selectTarget(firstTarget.resourceId);
  }

  protected selectTarget(resourceId: string): void {
    const target = this.selectedKind()?.targets.find((option) => option.resourceId === resourceId);
    this.selectedTargetId.set(resourceId);
    this.selectedIdentityKey.set(
      target
        ? this.identityKey(
            (target.identities.find((identity) => identity.isDefault) ?? target.identities[0])
              ?.kind ?? 'None',
            (target.identities.find((identity) => identity.isDefault) ?? target.identities[0])
              ?.resourceId ?? null,
          )
        : '',
    );
    this.selectedRoles.set(target?.roles.slice(0, 1) ?? []);
    this.selectedDataAccess.set(target?.dataAccessLevels[0] ?? null);
    this.invalidatePreview();
  }

  protected selectIdentity(value: string): void {
    this.selectedIdentityKey.set(value);
    this.invalidatePreview();
  }

  protected toggleRole(role: string, checked: boolean): void {
    this.selectedRoles.update((roles) =>
      checked ? [...new Set([...roles, role])] : roles.filter((value) => value !== role),
    );
    this.invalidatePreview();
  }

  protected selectDataAccess(value: DataAccessLevel | null): void {
    this.selectedDataAccess.set(value);
    this.invalidatePreview();
  }

  protected async preview(): Promise<void> {
    const request = this.previewRequest();
    if (
      !request ||
      this.previewBusy() ||
      this.loading() ||
      this.loadError() ||
      !this.currentComponent()
    )
      return;
    const sequence = ++this.previewSequence;
    const requestKey = JSON.stringify(request);
    const componentVersion = this.currentComponentVersion();
    this.previewRequestKey = null;
    this.previewBusy.set(true);
    this.previewError.set(false);
    this.previewResult.set(null);
    try {
      const result = await firstValueFrom(
        this.resources.previewLink(
          this.projectId(),
          this.component().id,
          this.resource().id,
          request,
        ),
      );
      if (
        sequence !== this.previewSequence ||
        requestKey !== this.currentPreviewRequestKey() ||
        componentVersion !== this.currentComponentVersion()
      )
        return;
      this.previewResult.set(result);
      this.previewRequestKey = requestKey;
      this.previewComponentVersion = componentVersion;
    } catch {
      if (sequence === this.previewSequence) this.previewError.set(true);
    } finally {
      if (sequence === this.previewSequence) this.previewBusy.set(false);
    }
  }

  protected async addLink(): Promise<void> {
    const request = this.previewRequest();
    const identity = this.selectedIdentity();
    const target = this.selectedTarget();
    if (
      !request ||
      !identity ||
      !target ||
      this.saving() ||
      this.loading() ||
      this.loadError() ||
      !this.currentComponent() ||
      !this.previewResult() ||
      this.previewRequestKey !== JSON.stringify(request)
    )
      return;
    if (this.previewComponentVersion !== this.currentComponentVersion()) {
      this.invalidatePreview();
      return;
    }
    this.saving.set(true);
    this.actionError.set(false);
    try {
      await firstValueFrom(
        this.resources.addLink(this.projectId(), this.component().id, this.resource().id, {
          ...request,
          expectedComponentVersion: this.currentComponentVersion(),
        }),
      );
      this.addOpen.set(false);
      this.invalidatePreview();
      await this.load();
    } catch (error) {
      this.actionError.set(true);
      if (error instanceof ApiError && error.status === 409) {
        this.invalidatePreview();
        await this.load();
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected async requestRemoval(link: ResourceLinkResponse): Promise<void> {
    if (this.saving() || this.removalBusy()) return;
    this.removalError.set(false);
    this.removalConflict.set(false);
    try {
      const impact = await firstValueFrom(
        this.resources.linkRemovalImpact(
          this.projectId(),
          this.component().id,
          this.resource().id,
          link.id,
        ),
      );
      this.removal.set({ link, impact });
    } catch {
      this.removalError.set(true);
    }
  }

  protected closeRemoval(): void {
    if (this.removalBusy()) return;
    this.removal.set(null);
    this.removalError.set(false);
    this.removalConflict.set(false);
  }

  protected async confirmRemoval(): Promise<void> {
    const pending = this.removal();
    if (!pending || this.removalBusy()) return;
    this.removalBusy.set(true);
    this.removalError.set(false);
    this.removalConflict.set(false);
    try {
      await firstValueFrom(
        this.resources.removeLink(
          this.projectId(),
          this.component().id,
          this.resource().id,
          pending.link.id,
          {
            expectedLinkVersion: Number(pending.link.version),
            expectedModelVersion: pending.impact.modelVersion,
          },
        ),
      );
      this.removal.set(null);
      await this.load();
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        await this.refreshRemoval(pending.link.id);
      } else {
        this.removalError.set(true);
      }
    } finally {
      this.removalBusy.set(false);
    }
  }

  protected linkName(link: ResourceLinkResponse, outgoing: boolean): string {
    return outgoing ? link.targetResourceName : link.sourceResourceName;
  }

  protected linkComponentCode(link: ResourceLinkResponse, outgoing: boolean): string {
    return outgoing ? link.targetComponentCode : link.sourceComponentCode;
  }

  protected localizedLabel(labelFr: string, labelEn: string): string {
    return this.transloco.getActiveLang().startsWith('fr') ? labelFr : labelEn;
  }

  protected targetOptionLabel(target: ResourceLinkKindOptionResponse['targets'][number]): string {
    const duplicates =
      this.selectedKind()?.targets.filter(
        (candidate) =>
          candidate.componentCode === target.componentCode &&
          candidate.resourceName === target.resourceName,
      ).length ?? 0;
    const label = `${target.componentCode} / ${target.resourceName}`;
    return duplicates > 1 ? `${label} · ${target.resourceType}` : label;
  }

  protected dataAccessLabel(level: DataAccessLevel): string {
    return this.transloco.translate(this.dataAccessTranslationKeys[level]);
  }

  protected implicitOriginLabel(link: ResourceLinkResponse): string {
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

  protected identityLabel(kind: LinkIdentityKind, resourceName: string | null): string {
    const name = resourceName
      ? ((this.currentComponent() ?? this.component()).resources.find(
          (resource) => resource.id === resourceName,
        )?.logicalName ?? resourceName)
      : '';
    return this.transloco.translate(this.identityKindTranslationKeys[kind], { name });
  }

  protected impactMessage(code: string, fallback: string): string {
    return resourceImpactMessage(this.transloco, code, fallback);
  }

  protected async reload(): Promise<void> {
    await this.load();
  }

  private previewRequest(): LinkPreviewRequest | null {
    const kind = this.selectedKind();
    const target = this.selectedTarget();
    const identity = this.selectedIdentity();
    if (!kind || !target || !identity) return null;
    return {
      kind: kind.kind,
      targetComponentId: target.componentId,
      targetResourceId: target.resourceId,
      identityKind: identity.kind,
      identityResourceId: identity.resourceId,
      accessScope: 'Resource',
      dataAccessLevel: this.selectedDataAccess(),
      roles: this.selectedRoles(),
    };
  }

  private identityKey(kind: LinkIdentityKind, resourceId: string | null): string {
    return `${kind}:${resourceId ?? ''}`;
  }

  private currentPreviewRequestKey(): string | null {
    const request = this.previewRequest();
    return request ? JSON.stringify(request) : null;
  }

  private invalidatePreview(): void {
    this.previewSequence++;
    this.previewBusy.set(false);
    this.previewError.set(false);
    this.previewResult.set(null);
    this.previewRequestKey = null;
    this.previewComponentVersion = null;
  }

  private currentComponentVersion(): number {
    return Math.max(
      Number(this.currentComponent()?.version ?? 0),
      Number(this.component().version),
    );
  }

  private async refreshRemoval(linkId: string): Promise<void> {
    await this.load();
    const link = this.links()?.outgoing.find((candidate) => candidate.id === linkId);
    if (this.loadError() || !link) {
      this.removal.set(null);
      this.removalError.set(true);
      return;
    }

    try {
      const impact = await firstValueFrom(
        this.resources.linkRemovalImpact(
          this.projectId(),
          this.component().id,
          this.resource().id,
          link.id,
        ),
      );
      this.removal.set({ link, impact });
      this.removalConflict.set(true);
    } catch {
      this.removal.set(null);
      this.removalError.set(true);
    }
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
      const previousComponent = this.currentComponent();
      if (
        previousComponent &&
        (previousComponent.id !== component.id ||
          Number(previousComponent.version) !== Number(component.version))
      )
        this.invalidatePreview();
      this.currentComponent.set(component);
      this.links.set(links);
      if (this.resource().isExisting) {
        this.options.set([]);
      } else {
        const options = await firstValueFrom(
          this.resources.linkOptions(this.projectId(), this.component().id, this.resource().id),
        );
        this.options.set(options.kinds);
      }
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
