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
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type {
  ApplicationConfigurationResponse,
  ApplicationStepRequest,
  ResourceApplicationResponse,
  ResourceResponse,
  SaveApplicationRequest,
} from '../../core/api/generated';
import { ResourcesService } from '../../core/resources/resources.service';
import { DsButton } from '../../ds/button/button';

const stepParameterLabelKeys: Readonly<Record<string, string>> = {
  tool: 'resource.stepParameter.tool',
  minimumSeverity: 'resource.stepParameter.minimumSeverity',
  exceptions: 'resource.stepParameter.exceptions',
};

@Component({
  selector: 'app-resource-application-panel',
  imports: [DsButton, FormsModule, TranslocoPipe],
  templateUrl: './resource-application-panel.html',
  styleUrl: './resource-application-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceApplicationPanel implements OnInit {
  readonly projectId = input.required<string>();
  readonly componentId = input.required<string>();
  readonly resource = input.required<ResourceResponse>();
  readonly canEdit = input(false);

  protected readonly response = signal<ResourceApplicationResponse | null>(null);
  protected readonly draft = signal<SaveApplicationRequest | null>(null);
  protected readonly configuration = computed<ApplicationConfigurationResponse | null>(
    () => this.response()?.application ?? null,
  );
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly actionError = signal(false);
  protected readonly canSave = computed(() => {
    const draft = this.draft();
    return (
      !!draft &&
      this.canEdit() &&
      !this.saving() &&
      draft.name.trim().length > 0 &&
      draft.codeSourcePath.trim().length > 0 &&
      draft.dockerfilePath.trim().length > 0 &&
      draft.buildContextPath.trim().length > 0 &&
      draft.imageRepository.trim().length > 0
    );
  });

  private readonly resources = inject(ResourcesService);

  ngOnInit(): void {
    void this.load();
  }

  protected updateText(
    field: 'name' | 'codeSourcePath' | 'dockerfilePath' | 'buildContextPath' | 'imageRepository',
    value: string,
  ): void {
    this.draft.update((current) => (current ? { ...current, [field]: value } : current));
  }

  protected updateHealthCheckPath(value: string): void {
    this.draft.update((current) =>
      current ? { ...current, healthCheckPath: value || null } : current,
    );
  }

  protected isChecked(event: Event): boolean {
    return (event.currentTarget as HTMLInputElement).checked;
  }

  protected stepParameterLabelKey(name: string): string {
    return stepParameterLabelKeys[name] ?? name;
  }

  protected updateBuildRegistryEnvironment(value: string): void {
    this.draft.update((current) =>
      current ? { ...current, buildRegistryEnvironmentId: value || null } : current,
    );
  }

  protected updateStep(index: number, patch: Partial<ApplicationStepRequest>): void {
    this.draft.update((current) => {
      if (!current) return current;
      const steps = current.steps.map((step, stepIndex) =>
        stepIndex === index ? { ...step, ...patch } : step,
      );
      return { ...current, steps };
    });
  }

  protected updateParameter(stepIndex: number, parameterIndex: number, value: string): void {
    this.draft.update((current) => {
      if (!current) return current;
      const steps = current.steps.map((step, index) =>
        index === stepIndex
          ? {
              ...step,
              parameters: step.parameters.map((parameter, itemIndex) =>
                itemIndex === parameterIndex ? { ...parameter, value } : parameter,
              ),
            }
          : step,
      );
      return { ...current, steps };
    });
  }

  protected async save(): Promise<void> {
    const draft = this.draft();
    if (!draft || !this.canSave()) return;
    this.saving.set(true);
    this.actionError.set(false);
    try {
      const saved = await firstValueFrom(
        this.resources.saveApplication(
          this.projectId(),
          this.componentId(),
          this.resource().id,
          draft,
        ),
      );
      this.setResponse(saved);
    } catch {
      this.actionError.set(true);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const response = await firstValueFrom(
        this.resources.application(this.projectId(), this.componentId(), this.resource().id),
      );
      this.setResponse(response);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private setResponse(response: ResourceApplicationResponse): void {
    this.response.set(response);
    const application = response.application;
    if (!response.available || !application) {
      this.draft.set(null);
      return;
    }

    this.draft.set({
      expectedVersion: Number(application.version),
      name: application.name,
      codeSourcePath: application.codeSourcePath,
      dockerfilePath: application.dockerfilePath,
      buildContextPath: application.buildContextPath,
      imageRepository: application.imageRepository,
      healthCheckPath: application.healthCheckPath,
      deploymentStrategy: application.deploymentStrategy,
      buildRegistryEnvironmentId: application.buildRegistry.selectedEnvironmentId,
      steps: application.steps.map((step) => ({
        name: step.name,
        enabled: step.enabled,
        blocking: step.blocking,
        position: step.position,
        parameters: step.parameters.map((parameter) => ({
          name: parameter.name,
          value: parameter.value,
        })),
      })),
    });
  }
}
