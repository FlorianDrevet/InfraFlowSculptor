import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ComponentDeploymentMode,
  ComponentResponse,
  ProjectEnvironmentResponse,
  ProjectSummaryResponse,
  RemovedResourceBehavior,
  SaveComponentRequest,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';
import { DsToggle } from '../../ds/toggle/toggle';

interface ComponentForm {
  name: string;
  code: string;
  description: string;
  mode: ComponentDeploymentMode;
  targetsAllEnvironments: boolean;
  targetEnvironmentIds: string[];
  singleTargetCode: string;
  subscriptionId: string;
  region: string;
  deploymentConnection: string;
  singleTargetIsProtected: boolean;
  approvers: string;
  runners: string;
  removedResourceBehavior: RemovedResourceBehavior;
  deletionProtectionEnabled: boolean;
  tags: string;
}

function parseList(value: string): string[] {
  return value
    .split(/[\r\n,]+/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function parseTags(value: string): Record<string, string> {
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

function fromComponent(component: ComponentResponse): ComponentForm {
  const target = component.singleTarget;
  return {
    name: component.name,
    code: component.code,
    description: component.description,
    mode: component.mode,
    targetsAllEnvironments: component.targetsAllEnvironments,
    targetEnvironmentIds: [...component.targetEnvironmentIds],
    singleTargetCode: target?.code ?? 'shared',
    subscriptionId: target?.subscriptionId ?? '',
    region: target?.region ?? '',
    deploymentConnection: target?.deploymentConnection ?? '',
    singleTargetIsProtected: target?.isProtected ?? true,
    approvers: target?.approvers.join(', ') ?? '',
    runners: target?.runners.join(', ') ?? '',
    removedResourceBehavior: component.removedResourceBehavior,
    deletionProtectionEnabled: component.deletionProtectionEnabled,
    tags: Object.entries(component.tags)
      .map(([key, value]) => `${key}=${value}`)
      .join('\n'),
  };
}

@Component({
  selector: 'app-component-editor',
  imports: [DsButton, DsTextField, DsToggle, FormsModule, RouterLink, TranslocoPipe],
  templateUrl: './component-editor.html',
  styleUrl: './component-editor.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComponentEditor implements OnInit {
  readonly orgId = input.required<string>();
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly component = signal<ComponentResponse | null>(null);
  protected readonly environments = signal<ProjectEnvironmentResponse[]>([]);
  protected readonly permissions = signal<string[]>([]);
  protected readonly form = signal<ComponentForm>(this.emptyForm());
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly versionConflict = signal(false);
  protected readonly saving = signal(false);
  protected readonly deleteConfirmationOpen = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal(false);
  private readonly projects = inject(ProjectsService);
  private readonly componentsService = inject(ComponentsService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.load();
  }

  protected isCreating(): boolean {
    return this.componentId() === 'new';
  }

  protected canManage(): boolean {
    return this.permissions().includes('components.manage');
  }

  protected identityLocked(): boolean {
    return !this.isCreating() && this.component()?.firstPublishedAt !== null;
  }

  protected updateField<K extends keyof ComponentForm>(field: K, value: ComponentForm[K]): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected setMode(mode: ComponentDeploymentMode): void {
    if (this.identityLocked()) {
      return;
    }
    this.form.update((current) => ({
      ...current,
      mode,
      targetsAllEnvironments: mode === 'PerEnvironment' ? current.targetsAllEnvironments : false,
      targetEnvironmentIds: mode === 'PerEnvironment' ? current.targetEnvironmentIds : [],
    }));
  }

  protected setAllEnvironments(enabled: boolean): void {
    this.form.update((current) => ({
      ...current,
      targetsAllEnvironments: enabled,
      targetEnvironmentIds: enabled ? [] : current.targetEnvironmentIds,
    }));
  }

  protected toggleEnvironment(environmentId: string, selected: boolean): void {
    this.form.update((current) => {
      const targets = new Set(current.targetEnvironmentIds);
      if (selected) {
        targets.add(environmentId);
      } else {
        targets.delete(environmentId);
      }
      return { ...current, targetEnvironmentIds: [...targets] };
    });
  }

  protected async save(): Promise<void> {
    const form = this.form();
    const component = this.component();
    if (!this.canManage() || this.saving()) {
      return;
    }
    if (
      !form.name.trim() ||
      !form.code.trim() ||
      (form.mode === 'PerEnvironment' &&
        !form.targetsAllEnvironments &&
        form.targetEnvironmentIds.length === 0) ||
      (form.mode === 'Single' && (!form.subscriptionId.trim() || !form.region.trim()))
    ) {
      this.saveError.set(true);
      return;
    }

    this.saving.set(true);
    this.saveError.set(false);
    this.versionConflict.set(false);
    const body: SaveComponentRequest = {
      name: form.name.trim(),
      code: form.code.trim(),
      description: form.description.trim(),
      mode: form.mode,
      targetsAllEnvironments: form.mode === 'PerEnvironment' && form.targetsAllEnvironments,
      targetEnvironmentIds:
        form.mode === 'PerEnvironment' && !form.targetsAllEnvironments
          ? form.targetEnvironmentIds
          : [],
      singleTarget:
        form.mode === 'Single'
          ? {
              code: form.singleTargetCode.trim() || 'shared',
              subscriptionId: form.subscriptionId.trim(),
              region: form.region.trim(),
              deploymentConnection: form.deploymentConnection.trim() || null,
              isProtected: form.singleTargetIsProtected,
              approvers: parseList(form.approvers),
              runners: parseList(form.runners),
            }
          : null,
      removedResourceBehavior: form.removedResourceBehavior,
      deletionProtectionEnabled: form.deletionProtectionEnabled,
      tags: parseTags(form.tags),
      expectedVersion: this.isCreating()
        ? Number(this.project()?.version ?? 0)
        : Number(component?.version ?? 0),
    };

    try {
      const saved = this.isCreating()
        ? await firstValueFrom(this.componentsService.create(this.projectId(), body))
        : await firstValueFrom(
            this.componentsService.update(this.projectId(), this.componentId(), body),
          );
      await this.router.navigate([
        '/o',
        this.orgId(),
        'projects',
        this.projectId(),
        'components',
        saved.id,
      ]);
    } catch (error) {
      if (error instanceof ApiError && error.code === 'CONFLICT_VERSION') {
        this.versionConflict.set(true);
      } else {
        this.saveError.set(true);
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteComponent(): Promise<void> {
    const component = this.component();
    if (!component || component.firstPublishedAt !== null || !this.canManage() || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(false);
    try {
      await firstValueFrom(
        this.componentsService.delete(this.projectId(), component.id, {
          expectedVersion: Number(component.version),
        }),
      );
      await this.navigateToComponents();
    } catch {
      this.deleteError.set(true);
    } finally {
      this.deleting.set(false);
    }
  }

  protected async navigateToComponents(): Promise<void> {
    await this.router.navigate(['/o', this.orgId(), 'projects', this.projectId(), 'components']);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, environments, permissions] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.projects.environments(this.projectId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
      ]);
      this.project.set(project);
      this.environments.set(environments);
      this.permissions.set(permissions.permissions);
      if (this.isCreating()) {
        const first = environments[0];
        const last = environments.at(-1);
        this.form.set({
          ...this.emptyForm(),
          subscriptionId: first?.subscriptionId ?? '',
          region: first?.region ?? '',
          deploymentConnection: `ifs-${project.code}-shared`,
          approvers: last?.approvers.join(', ') ?? '',
          runners: project.defaultRunners.join(', '),
        });
      } else {
        const component = await firstValueFrom(
          this.componentsService.get(this.projectId(), this.componentId()),
        );
        this.component.set(component);
        this.form.set(fromComponent(component));
      }
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private emptyForm(): ComponentForm {
    return {
      name: '',
      code: '',
      description: '',
      mode: 'PerEnvironment',
      targetsAllEnvironments: true,
      targetEnvironmentIds: [],
      singleTargetCode: 'shared',
      subscriptionId: '',
      region: '',
      deploymentConnection: '',
      singleTargetIsProtected: true,
      approvers: '',
      runners: '',
      removedResourceBehavior: 'Detach',
      deletionProtectionEnabled: true,
      tags: '',
    };
  }
}
