import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { ApiError } from '../../core/http/api-error';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ComponentResponse,
  ProjectEnvironmentResponse,
  ProjectValidationComponentResponse,
  ResourceDeletionImpactResponse,
  ResourceResponse,
  ResourceGroupResponse,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';
import { ResourceDeleteDialog } from './resource-delete-dialog';

interface ResourceGroupForm {
  name: string;
  region: string;
  tags: string;
  forcedNames: string;
}

function entriesText(entries: Record<string, string>): string {
  return Object.entries(entries)
    .map(([key, value]) => `${key}=${value}`)
    .join('\n');
}

function parseEntries(value: string): Record<string, string> {
  return Object.fromEntries(
    value.split(/\r?\n/).flatMap((line) => {
      const separator = line.indexOf('=');
      if (separator < 1) {
        return [];
      }
      const key = line.slice(0, separator).trim();
      return key ? [[key, line.slice(separator + 1).trim()]] : [];
    }),
  );
}

function parseForcedNames(
  value: string,
  environments: ProjectEnvironmentResponse[],
): Record<string, string> | null {
  const environmentIds = new Map(
    environments.map((environment) => [environment.code.toLowerCase(), environment.id]),
  );
  const result: Record<string, string> = {};
  for (const [code, name] of Object.entries(parseEntries(value))) {
    const environmentId = environmentIds.get(code.toLowerCase());
    if (!environmentId) {
      return null;
    }
    result[environmentId] = name;
  }
  return result;
}

@Component({
  selector: 'app-component-detail',
  imports: [DsButton, FormsModule, ResourceDeleteDialog, RouterLink, TranslocoPipe],
  templateUrl: './component-detail.html',
  styleUrl: './component-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComponentDetail implements OnInit {
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
  protected readonly orderedComponents = signal<ProjectValidationComponentResponse[]>([]);
  protected readonly componentDependencies = computed(() =>
    this.orderedComponents().find((item) => item.id === this.component()?.id),
  );
  protected readonly canManageComponents = signal(false);
  protected readonly canEditModel = signal(false);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly actionError = signal(false);
  protected readonly groupFormOpen = signal(false);
  protected readonly editingGroupId = signal<string | null>(null);
  protected readonly groupForm = signal<ResourceGroupForm>(this.emptyGroupForm());
  protected readonly pendingDeletionGroupId = signal<string | null>(null);
  protected readonly deletingGroup = signal(false);
  protected readonly pendingDeleteResource = signal<ResourceResponse | null>(null);
  protected readonly deletionImpact = signal<ResourceDeletionImpactResponse | null>(null);
  protected readonly deletionConfirmation = signal('');
  protected readonly deletionLoading = signal(false);
  protected readonly deletingResource = signal(false);
  protected readonly deletionError = signal(false);
  protected readonly deletionConflict = signal(false);
  protected readonly resourceDeletionNotice = signal<'removed' | 'alreadyRemoved' | null>(null);
  @ViewChild('resourcesHeading') private resourcesHeading?: ElementRef<HTMLHeadingElement>;
  @ViewChild('resourceDeletionStatus')
  private resourceDeletionStatus?: ElementRef<HTMLParagraphElement>;
  private deletionImpactRequestId = 0;
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly projects = inject(ProjectsService);
  private readonly componentService = inject(ComponentsService);
  private readonly resources = inject(ResourcesService);

  ngOnInit(): void {
    void this.load();
  }

  protected openCreateGroup(): void {
    this.editingGroupId.set(null);
    this.groupForm.set(this.emptyGroupForm());
    this.groupFormOpen.set(true);
    this.actionError.set(false);
  }

  protected openEditGroup(group: ResourceGroupResponse): void {
    this.editingGroupId.set(group.id);
    this.groupForm.set({
      name: group.name,
      region: group.region ?? '',
      tags: entriesText(group.tags),
      forcedNames: entriesText(this.forcedNamesByCode(group.forcedNames)),
    });
    this.groupFormOpen.set(true);
    this.actionError.set(false);
  }

  protected updateGroupField<K extends keyof ResourceGroupForm>(
    field: K,
    value: ResourceGroupForm[K],
  ): void {
    this.groupForm.update((current) => ({ ...current, [field]: value }));
  }

  protected closeGroupForm(): void {
    this.groupFormOpen.set(false);
    this.editingGroupId.set(null);
    this.actionError.set(false);
  }

  protected requestGroupDeletion(groupId: string): void {
    this.pendingDeletionGroupId.set(groupId);
    this.actionError.set(false);
  }

  protected cancelGroupDeletion(): void {
    this.pendingDeletionGroupId.set(null);
  }

  protected async requestResourceDeletion(resource: ResourceResponse): Promise<void> {
    if (this.deletingResource()) return;
    const requestId = ++this.deletionImpactRequestId;
    this.resourceDeletionNotice.set(null);
    this.pendingDeleteResource.set(resource);
    this.deletionImpact.set(null);
    this.deletionConfirmation.set('');
    this.deletionError.set(false);
    this.deletionConflict.set(false);
    this.deletionLoading.set(true);
    try {
      const impact = await firstValueFrom(
        this.resources.deletionImpact(this.projectId(), this.componentId(), resource.id),
      );
      if (requestId !== this.deletionImpactRequestId) return;
      this.deletionImpact.set(impact);
    } catch (error) {
      if (requestId !== this.deletionImpactRequestId) return;
      if (error instanceof ApiError && error.status === 404) {
        this.cancelResourceDeletion(false);
        this.resourceDeletionNotice.set('alreadyRemoved');
        await this.load();
        this.focusDeletionResult();
      } else {
        this.deletionError.set(true);
      }
    } finally {
      if (requestId === this.deletionImpactRequestId) this.deletionLoading.set(false);
    }
  }

  protected updateDeletionConfirmation(value: string): void {
    this.deletionConfirmation.set(value);
  }

  protected cancelResourceDeletion(restoreFocus = true): void {
    this.deletionImpactRequestId++;
    this.pendingDeleteResource.set(null);
    this.deletionImpact.set(null);
    this.deletionLoading.set(false);
    this.deletionError.set(false);
    this.deletionConflict.set(false);
    if (restoreFocus) this.focusResourcesHeading();
  }

  protected async deleteResource(): Promise<void> {
    const resource = this.pendingDeleteResource();
    const impact = this.deletionImpact();
    if (
      !resource ||
      !impact ||
      this.deletionConfirmation() !== resource.logicalName ||
      this.deletingResource()
    )
      return;

    const deleteRequestId = this.deletionImpactRequestId;
    this.deletingResource.set(true);
    this.deletionError.set(false);
    this.deletionConflict.set(false);
    try {
      await firstValueFrom(
        this.resources.delete(this.projectId(), this.componentId(), resource.id, {
          expectedVersion: impact.resourceVersion,
        }),
      );
      const closedCurrentDialog = deleteRequestId === this.deletionImpactRequestId;
      if (closedCurrentDialog) {
        this.cancelResourceDeletion(false);
        this.resourceDeletionNotice.set('removed');
      }
      await this.load();
      if (closedCurrentDialog) this.focusDeletionResult();
    } catch (error) {
      this.deletingResource.set(false);
      if (deleteRequestId !== this.deletionImpactRequestId) {
        await this.load();
        return;
      }

      if (error instanceof ApiError && error.status === 404) {
        this.cancelResourceDeletion(false);
        this.resourceDeletionNotice.set('alreadyRemoved');
        await this.load();
        this.focusDeletionResult();
        return;
      }

      const conflict = error instanceof ApiError && error.status === 409;
      this.deletionConflict.set(conflict);
      this.deletionError.set(true);
      if (conflict) {
        this.deletionImpact.set(null);
        this.deletionConfirmation.set('');
        this.deletionLoading.set(true);
        const requestId = ++this.deletionImpactRequestId;
        try {
          const refreshedComponent = await firstValueFrom(
            this.componentService.get(this.projectId(), this.componentId()),
          );
          if (requestId !== this.deletionImpactRequestId) return;
          this.component.set(refreshedComponent);
          const currentResource = refreshedComponent.resources.find(
            (item) => item.id === resource.id,
          );
          if (!currentResource) {
            this.cancelResourceDeletion(false);
            this.resourceDeletionNotice.set('alreadyRemoved');
            this.focusDeletionResult();
            return;
          }

          this.pendingDeleteResource.set(currentResource);
          const refreshedImpact = await firstValueFrom(
            this.resources.deletionImpact(this.projectId(), this.componentId(), currentResource.id),
          );
          if (requestId !== this.deletionImpactRequestId) return;
          this.deletionImpact.set(refreshedImpact);
        } catch (refreshError) {
          if (requestId !== this.deletionImpactRequestId) return;
          if (refreshError instanceof ApiError && refreshError.status === 404) {
            this.cancelResourceDeletion(false);
            this.resourceDeletionNotice.set('alreadyRemoved');
            await this.load();
            this.focusDeletionResult();
          } else {
            this.deletionError.set(true);
          }
        } finally {
          if (requestId === this.deletionImpactRequestId) this.deletionLoading.set(false);
        }
      } else {
        const requestId = this.deletionImpactRequestId;
        this.deletionLoading.set(true);
        try {
          await this.load();
        } finally {
          if (requestId === this.deletionImpactRequestId) this.deletionLoading.set(false);
        }
      }
    } finally {
      this.deletingResource.set(false);
    }
  }

  protected async saveGroup(): Promise<void> {
    const component = this.component();
    const forcedNames = parseForcedNames(this.groupForm().forcedNames, this.environments());
    if (
      !component ||
      !this.canEditModel() ||
      !this.groupForm().name.trim() ||
      forcedNames === null
    ) {
      this.actionError.set(true);
      return;
    }

    this.actionError.set(false);
    const body = {
      name: this.groupForm().name.trim(),
      region: this.groupForm().region.trim() || null,
      tags: parseEntries(this.groupForm().tags),
      forcedNames,
      expectedVersion: Number(component.version),
    };
    try {
      const editingGroupId = this.editingGroupId();
      if (editingGroupId) {
        await firstValueFrom(
          this.componentService.updateGroup(this.projectId(), component.id, editingGroupId, body),
        );
      } else {
        await firstValueFrom(
          this.componentService.createGroup(this.projectId(), component.id, body),
        );
      }
      this.closeGroupForm();
      await this.load();
    } catch {
      this.actionError.set(true);
      await this.load();
    }
  }

  protected async deleteGroup(): Promise<void> {
    const component = this.component();
    const groupId = this.pendingDeletionGroupId();
    if (!component || !groupId || !this.canEditModel() || this.deletingGroup()) {
      return;
    }

    this.deletingGroup.set(true);
    this.actionError.set(false);
    try {
      await firstValueFrom(
        this.componentService.deleteGroup(this.projectId(), component.id, groupId, {
          expectedVersion: Number(component.version),
        }),
      );
      this.cancelGroupDeletion();
      await this.load();
    } catch {
      this.actionError.set(true);
      await this.load();
    } finally {
      this.deletingGroup.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [component, environments, permissions, validation] = await Promise.all([
        firstValueFrom(this.componentService.get(this.projectId(), this.componentId())),
        firstValueFrom(this.projects.environments(this.projectId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
        firstValueFrom(this.projects.validation(this.projectId())),
      ]);
      this.component.set(component);
      this.environments.set(environments);
      this.orderedComponents.set(validation.orderedComponents);
      this.canManageComponents.set(permissions.permissions.includes('components.manage'));
      this.canEditModel.set(permissions.permissions.includes('model.edit'));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private focusDeletionResult(): void {
    this.changeDetector.detectChanges();
    const target =
      this.resourceDeletionStatus?.nativeElement ?? this.resourcesHeading?.nativeElement;
    target?.focus();
  }

  private focusResourcesHeading(): void {
    this.changeDetector.detectChanges();
    this.resourcesHeading?.nativeElement.focus();
  }

  private forcedNamesByCode(values: Record<string, string>): Record<string, string> {
    const codesById = new Map(
      this.environments().map((environment) => [environment.id, environment.code]),
    );
    return Object.fromEntries(
      Object.entries(values).flatMap(([id, name]) => {
        const code = codesById.get(id);
        return code ? [[code, name]] : [];
      }),
    );
  }

  private emptyGroupForm(): ResourceGroupForm {
    return { name: '', region: '', tags: '', forcedNames: '' };
  }
}
