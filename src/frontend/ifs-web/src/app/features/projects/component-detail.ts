import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ComponentResponse,
  ProjectEnvironmentResponse,
  ResourceGroupResponse,
} from '../../core/api/generated';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { DsButton } from '../../ds/button/button';

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
  imports: [DsButton, FormsModule, RouterLink, TranslocoPipe],
  templateUrl: './component-detail.html',
  styleUrl: './component-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComponentDetail implements OnInit {
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
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
  private readonly projects = inject(ProjectsService);
  private readonly componentService = inject(ComponentsService);

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
      const [component, environments, permissions] = await Promise.all([
        firstValueFrom(this.componentService.get(this.projectId(), this.componentId())),
        firstValueFrom(this.projects.environments(this.projectId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      this.component.set(component);
      this.environments.set(environments);
      this.canManageComponents.set(permissions.permissions.includes('components.manage'));
      this.canEditModel.set(permissions.permissions.includes('model.edit'));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
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
