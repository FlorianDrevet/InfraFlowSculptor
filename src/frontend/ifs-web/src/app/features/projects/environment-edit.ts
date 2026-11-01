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
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';
import { DsToggle } from '../../ds/toggle/toggle';
import type {
  ProjectEnvironmentResponse,
  ProjectSummaryResponse,
  UpdateProjectEnvironmentRequest,
} from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { ProjectsService } from '../../core/projects/projects.service';

type EnvironmentForm = {
  name: string;
  code: string;
  subscriptionId: string;
  region: string;
  deploymentConnection: string;
  isProtected: boolean;
  approvers: string;
  runners: string;
  description: string;
  tags: string;
};

function fromEnvironment(environment: ProjectEnvironmentResponse): EnvironmentForm {
  return {
    name: environment.name,
    code: environment.code,
    subscriptionId: environment.subscriptionId,
    region: environment.region,
    deploymentConnection: environment.deploymentConnection,
    isProtected: environment.isProtected,
    approvers: environment.approvers.join(', '),
    runners: environment.runners.join(', '),
    description: environment.description,
    tags: Object.entries(environment.tags)
      .map(([key, value]) => `${key}=${value}`)
      .join('\n'),
  };
}

function splitValues(value: string): string[] {
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

function asLines(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) =>
      typeof item === 'string' ? item : (JSON.stringify(item) ?? String(item)),
    );
  }
  if (typeof value === 'object' && value !== null) {
    return Object.entries(value).map(
      ([key, item]) => `${key}: ${Array.isArray(item) ? item.join(', ') : String(item)}`,
    );
  }
  return typeof value === 'string' ? [value] : [];
}

@Component({
  selector: 'app-environment-edit',
  imports: [DsButton, DsTextField, DsToggle, FormsModule, TranslocoPipe],
  templateUrl: './environment-edit.html',
  styleUrl: './environment-edit.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnvironmentEdit implements OnInit {
  readonly projectId = input.required<string>();
  readonly environmentId = input.required<string>();
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ProjectsService);
  private readonly router = inject(Router);
  @ViewChild('impactDialog') private impactDialog?: ElementRef<HTMLDialogElement>;

  protected readonly project = signal<ProjectSummaryResponse | null>(null);
  protected readonly form = signal<EnvironmentForm>(this.emptyForm());
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly saving = signal(false);
  protected readonly projectVersion = signal(0);
  protected readonly permissions = signal<string[]>([]);
  protected readonly originalIsProtected = signal(false);
  protected readonly affectedResources = signal<string[]>([]);
  protected readonly affectedData = signal<string[]>([]);
  protected readonly versionConflict = signal<{
    stored: EnvironmentForm;
    yours: EnvironmentForm;
  } | null>(null);
  protected readonly isCreating = computed(() => this.environmentId() === 'new');
  protected readonly canSave = computed(() => {
    const permissions = this.permissions();
    const requiresProtectedPermission = this.originalIsProtected() || this.form().isProtected;
    return (
      permissions.includes('environments.manage') &&
      (!requiresProtectedPermission || permissions.includes('environments.protected.manage'))
    );
  });

  ngOnInit(): void {
    void this.load();
  }

  protected updateField<K extends keyof EnvironmentForm>(
    field: K,
    value: EnvironmentForm[K],
  ): void {
    this.form.update((current) => ({ ...current, [field]: value }));
  }

  protected cancelImpact(): void {
    if (this.impactDialog?.nativeElement.open) {
      this.impactDialog.nativeElement.close();
    }
    this.affectedResources.set([]);
    this.affectedData.set([]);
  }

  protected async save(confirmTargetImpact = false): Promise<void> {
    if (!this.canSave() || this.saving()) {
      return;
    }

    const value = this.form();
    if (
      !value.name.trim() ||
      !value.code.trim() ||
      !value.subscriptionId.trim() ||
      !value.region.trim()
    ) {
      this.saveError.set(true);
      return;
    }

    this.saving.set(true);
    this.saveError.set(false);
    this.versionConflict.set(null);
    const environmentTags = parseTags(value.tags);
    try {
      if (this.isCreating()) {
        const environments = await firstValueFrom(this.service.environments(this.projectId()));
        await firstValueFrom(
          this.service.addEnvironment(this.projectId(), {
            name: value.name.trim(),
            code: value.code.trim(),
            position: environments.length + 1,
            subscriptionId: value.subscriptionId.trim(),
            region: value.region.trim(),
            isProtected: value.isProtected,
            approvers: splitValues(value.approvers),
            runners: splitValues(value.runners),
            description: value.description.trim(),
            deploymentConnection: value.deploymentConnection.trim() || undefined,
            tags: environmentTags,
            expectedVersion: this.projectVersion(),
          }),
        );
      } else {
        const body: UpdateProjectEnvironmentRequest = {
          name: value.name.trim(),
          code: value.code.trim(),
          subscriptionId: value.subscriptionId.trim(),
          region: value.region.trim(),
          isProtected: value.isProtected,
          approvers: splitValues(value.approvers),
          runners: splitValues(value.runners),
          description: value.description.trim(),
          deploymentConnection: value.deploymentConnection.trim() || undefined,
          tags: environmentTags,
          confirmTargetImpact,
          expectedVersion: this.projectVersion(),
        };
        await firstValueFrom(
          this.service.updateEnvironment(this.projectId(), this.environmentId(), body),
        );
      }
      await this.router.navigate(['..'], { relativeTo: this.route });
    } catch (error) {
      if (
        !confirmTargetImpact &&
        error instanceof ApiError &&
        error.code === 'RG-ENV-03-CONFIRMATION-REQUIRED'
      ) {
        this.affectedResources.set(asLines(error.metadata['affectedResources']));
        this.affectedData.set(asLines(error.metadata['affectedData']));
        this.impactDialog?.nativeElement.showModal();
      } else if (error instanceof ApiError && error.code === 'CONFLICT_VERSION') {
        await this.loadConflict();
      } else {
        this.saveError.set(true);
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected async confirmImpact(): Promise<void> {
    this.cancelImpact();
    await this.save(true);
  }

  protected cancel(): void {
    void this.router.navigate(['..'], { relativeTo: this.route });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [project, environments, permissions] = await Promise.all([
        firstValueFrom(this.service.get(this.projectId())),
        firstValueFrom(this.service.environments(this.projectId())),
        firstValueFrom(this.service.myPermissions(this.projectId())),
      ]);
      this.project.set(project);
      this.projectVersion.set(Number(project.version));
      this.permissions.set(permissions.permissions);
      if (this.isCreating()) {
        this.originalIsProtected.set(false);
        const first = environments[0];
        this.form.set({
          ...this.emptyForm(),
          subscriptionId: first?.subscriptionId ?? '',
          region: first?.region ?? '',
        });
      } else {
        const environment = environments.find((item) => item.id === this.environmentId());
        if (!environment) {
          throw new Error('Environment not found.');
        }
        this.originalIsProtected.set(environment.isProtected);
        this.form.set(fromEnvironment(environment));
      }
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadConflict(): Promise<void> {
    const yours = this.form();
    try {
      const environments = await firstValueFrom(this.service.environments(this.projectId()));
      const stored = environments.find((item) => item.id === this.environmentId());
      const project = await firstValueFrom(this.service.get(this.projectId()));
      this.projectVersion.set(Number(project.version));
      if (stored) {
        this.originalIsProtected.set(stored.isProtected);
        this.versionConflict.set({ stored: fromEnvironment(stored), yours });
      } else {
        this.saveError.set(true);
      }
    } catch {
      this.saveError.set(true);
    }
  }

  private emptyForm(): EnvironmentForm {
    return {
      name: '',
      code: '',
      subscriptionId: '',
      region: '',
      deploymentConnection: '',
      isProtected: false,
      approvers: '',
      runners: '',
      description: '',
      tags: '',
    };
  }
}
