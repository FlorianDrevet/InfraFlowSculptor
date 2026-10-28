import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';
import { DsToggle } from '../../ds/toggle/toggle';
import type {
  CatalogRegion,
  CreateProjectFromWizardResponse,
  JsonElement,
  NamingPreviewResponse,
} from '../../core/api/generated';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import type { ProjectWizardMetadata } from '../../core/projects/project-wizard-metadata';
import { ProjectWizardService } from '../../core/projects/project-wizard.service';

type WizardStep = 1 | 2 | 3 | 4 | 5 | 6;
type NamingPreset = 'Compact' | 'CAF' | 'Custom';
type WizardEnvironment = {
  name: string;
  code: string;
  subscriptionId: string;
  region: string;
  isProtected: boolean;
  approvers: string;
  description: string;
};
type WizardState = {
  step: WizardStep;
  name: string;
  code: string;
  description: string;
  codeManuallyEdited: boolean;
  environments: WizardEnvironment[];
  namingPreset: NamingPreset;
  defaultNameTemplate: string;
  resourceGroupNameTemplate: string;
  publishingPreset: 'MonoRepo' | null;
};

const COMPACT_DEFAULT_TEMPLATE = '{abbr}-{project}-{name}-{env}';
const COMPACT_RESOURCE_GROUP_TEMPLATE = 'rg-{project}-{component}-{name}-{env}';
const CAF_DEFAULT_TEMPLATE = '{abbr}-{project}-{name}-{env}-{region}';
const CAF_RESOURCE_GROUP_TEMPLATE = 'rg-{project}-{component}-{name}-{env}-{region}';
const PROJECT_CODE_PATTERN = /^[a-z][a-z0-9]{1,9}$/;
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

const STEP_ITEMS = [
  {
    value: 1 as const,
    labelKey: 'projectWizard.steps.identity',
    titleKey: 'projectWizard.titles.identity',
  },
  {
    value: 2 as const,
    labelKey: 'projectWizard.steps.tools',
    titleKey: 'projectWizard.titles.tools',
  },
  {
    value: 3 as const,
    labelKey: 'projectWizard.steps.environments',
    titleKey: 'projectWizard.titles.environments',
  },
  {
    value: 4 as const,
    labelKey: 'projectWizard.steps.naming',
    titleKey: 'projectWizard.titles.naming',
  },
  {
    value: 5 as const,
    labelKey: 'projectWizard.steps.publishing',
    titleKey: 'projectWizard.titles.publishing',
  },
  {
    value: 6 as const,
    labelKey: 'projectWizard.steps.reviewStep',
    titleKey: 'projectWizard.titles.reviewStep',
  },
];

const PRESET_LABEL_KEYS: Record<NamingPreset, string> = {
  Compact: 'projectWizard.compactPreset',
  CAF: 'projectWizard.cafPreset',
  Custom: 'projectWizard.customPreset',
};

function createInitialState(): WizardState {
  return {
    step: 1,
    name: '',
    code: '',
    description: '',
    codeManuallyEdited: false,
    environments: [
      {
        name: 'Développement',
        code: 'dev',
        subscriptionId: '',
        region: '',
        isProtected: false,
        approvers: '',
        description: '',
      },
      {
        name: 'Production',
        code: 'prd',
        subscriptionId: '',
        region: '',
        isProtected: true,
        approvers: '',
        description: '',
      },
    ],
    namingPreset: 'Compact',
    defaultNameTemplate: COMPACT_DEFAULT_TEMPLATE,
    resourceGroupNameTemplate: COMPACT_RESOURCE_GROUP_TEMPLATE,
    publishingPreset: null,
  };
}

function isJsonRecord(value: unknown): value is Record<string, JsonElement> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function stringValue(value: JsonElement | undefined, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
}

function proposeCode(name: string): string {
  const normalized = name
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
    .replace(/^[^a-z]+/, '');
  return normalized.slice(0, 10);
}

function parseSavedState(content: JsonElement): WizardState {
  const initial = createInitialState();
  if (!isJsonRecord(content)) {
    return initial;
  }

  const stepValue = content['step'];
  const savedStep = typeof stepValue === 'number' ? Math.floor(stepValue) : initial.step;
  const environmentsValue = content['environments'];
  const environments = Array.isArray(environmentsValue)
    ? environmentsValue.filter(isJsonRecord).map((environment, index) => ({
        name: stringValue(environment['name'], `Environnement ${index + 1}`),
        code: stringValue(environment['code'], ''),
        subscriptionId: stringValue(environment['subscriptionId'], ''),
        region: stringValue(environment['region'], ''),
        isProtected: environment['isProtected'] === true,
        approvers: Array.isArray(environment['approvers'])
          ? environment['approvers']
              .filter((approver): approver is string => typeof approver === 'string')
              .join(', ')
          : stringValue(environment['approvers'], ''),
        description: stringValue(environment['description'], ''),
      }))
    : initial.environments;
  const savedPreset = stringValue(content['namingPreset'], initial.namingPreset);
  const namingPreset: NamingPreset =
    savedPreset === 'CAF' || savedPreset === 'Custom' ? savedPreset : 'Compact';
  const publishingPreset = content['publishingPreset'] === 'MonoRepo' ? 'MonoRepo' : null;

  return {
    step: Math.min(6, Math.max(1, savedStep)) as WizardStep,
    name: stringValue(content['name'], initial.name),
    code: stringValue(content['code'], initial.code),
    description: stringValue(content['description'], initial.description),
    codeManuallyEdited: content['codeManuallyEdited'] === true,
    environments,
    namingPreset,
    defaultNameTemplate: stringValue(content['defaultNameTemplate'], initial.defaultNameTemplate),
    resourceGroupNameTemplate: stringValue(
      content['resourceGroupNameTemplate'],
      initial.resourceGroupNameTemplate,
    ),
    publishingPreset,
  };
}

@Component({
  selector: 'app-project-wizard',
  imports: [DsButton, DsTextField, DsToggle, TranslocoPipe],
  templateUrl: './project-wizard.html',
  styleUrl: './project-wizard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectWizard implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly organizations = inject(OrganizationsService);
  private readonly wizard = inject(ProjectWizardService);
  private saveTimer?: ReturnType<typeof setTimeout>;
  private previewTimer?: ReturnType<typeof setTimeout>;
  private saveQueue: Promise<boolean> = Promise.resolve(true);
  private organizationId = '';
  private metadata?: ProjectWizardMetadata;

  protected readonly state = signal<WizardState>(createInitialState());
  protected readonly steps = STEP_ITEMS;
  protected readonly namingPresets: readonly NamingPreset[] = ['Compact', 'CAF', 'Custom'];
  protected readonly loading = signal(true);
  protected readonly ready = signal(false);
  protected readonly loadError = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal(false);
  protected readonly previewing = signal(false);
  protected readonly previewError = signal(false);
  protected readonly preview = signal<NamingPreviewResponse | null>(null);
  protected readonly validationError = signal<'identity' | 'environments' | 'naming' | null>(null);
  protected readonly creating = signal(false);
  protected readonly createError = signal(false);
  protected readonly createdProject = signal<CreateProjectFromWizardResponse | null>(null);
  protected readonly organizationName = signal('');
  protected readonly catalogVersion = signal('');
  protected readonly regions = signal<CatalogRegion[]>([]);
  protected readonly draftSaved = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }
    if (this.previewTimer) {
      clearTimeout(this.previewTimer);
    }
  }

  protected async load(): Promise<void> {
    this.organizationId =
      this.route.snapshot.paramMap.get('orgId') ??
      this.route.parent?.snapshot.paramMap.get('orgId') ??
      '';
    if (!this.organizationId) {
      this.loading.set(false);
      this.loadError.set(true);
      return;
    }

    try {
      const [draft, metadata, organization] = await Promise.all([
        firstValueFrom(this.wizard.loadDraft()),
        firstValueFrom(this.wizard.loadMetadata()),
        firstValueFrom(this.organizations.get(this.organizationId)),
      ]);
      this.metadata = metadata;
      this.organizationName.set(organization.name);
      this.catalogVersion.set(metadata.catalogVersion);
      this.regions.set(metadata.regions);
      this.state.set(draft ? parseSavedState(draft.content) : createInitialState());
      this.draftSaved.set(draft !== null);
      this.ready.set(true);
      this.loading.set(false);
      if (this.state().step === 4) {
        await this.refreshPreview();
      }
    } catch {
      this.loading.set(false);
      this.loadError.set(true);
    }
  }

  protected updateName(value: string | undefined): void {
    const state = this.state();
    const name = value ?? '';
    this.setState({
      ...state,
      name,
      code: state.codeManuallyEdited ? state.code : proposeCode(name),
    });
  }

  protected updateCode(value: string | undefined): void {
    const code = value ?? '';
    this.setState({ ...this.state(), code, codeManuallyEdited: true });
  }

  protected updateDescription(description: string): void {
    this.setState({ ...this.state(), description });
  }

  protected changeDescription(event: Event): void {
    this.updateDescription((event.target as HTMLTextAreaElement).value);
  }

  protected changeRegion(index: number, event: Event): void {
    this.updateEnvironment(index, { region: (event.target as HTMLSelectElement).value });
  }

  protected updateEnvironment(index: number, update: Partial<WizardEnvironment>): void {
    const environments = this.state().environments.map((environment, currentIndex) =>
      currentIndex === index ? { ...environment, ...update } : environment,
    );
    this.setState({ ...this.state(), environments });
  }

  protected addEnvironment(): void {
    const next = this.state().environments.length + 1;
    const environments = [
      ...this.state().environments,
      {
        name: '',
        code: `env${next}`,
        subscriptionId: '',
        region: '',
        isProtected: false,
        approvers: '',
        description: '',
      },
    ];
    this.setState({ ...this.state(), environments });
  }

  protected removeEnvironment(index: number): void {
    if (this.state().environments.length <= 1) {
      return;
    }
    this.setState({
      ...this.state(),
      environments: this.state().environments.filter((_, currentIndex) => currentIndex !== index),
    });
  }

  protected selectNamingPreset(preset: NamingPreset): void {
    const templates =
      preset === 'Compact'
        ? {
            defaultNameTemplate: COMPACT_DEFAULT_TEMPLATE,
            resourceGroupNameTemplate: COMPACT_RESOURCE_GROUP_TEMPLATE,
          }
        : preset === 'CAF'
          ? {
              defaultNameTemplate: CAF_DEFAULT_TEMPLATE,
              resourceGroupNameTemplate: CAF_RESOURCE_GROUP_TEMPLATE,
            }
          : {
              defaultNameTemplate: this.state().defaultNameTemplate,
              resourceGroupNameTemplate: this.state().resourceGroupNameTemplate,
            };
    this.setState({ ...this.state(), namingPreset: preset, ...templates });
  }

  protected updateTemplate(
    field: 'defaultNameTemplate' | 'resourceGroupNameTemplate',
    value: string | undefined,
  ): void {
    this.setState({ ...this.state(), namingPreset: 'Custom', [field]: value ?? '' });
  }

  protected async goToStep(step: WizardStep): Promise<void> {
    if (this.state().step === step) {
      return;
    }
    const nextState = { ...this.state(), step };
    if (!(await this.persistDraft(nextState))) {
      return;
    }
    this.validationError.set(null);
    this.state.set(nextState);
    if (step === 4) {
      await this.refreshPreview();
    }
  }

  protected async previousStep(): Promise<void> {
    if (this.state().step > 1) {
      await this.goToStep((this.state().step - 1) as WizardStep);
    }
  }

  protected async nextStep(): Promise<void> {
    const step = this.state().step;
    if (!(await this.validateStep(step))) {
      return;
    }
    if (step < 6) {
      await this.goToStep((step + 1) as WizardStep);
    }
  }

  protected submitStep(event: Event): void {
    event.preventDefault();
    void this.nextStep();
  }

  protected async skipPublishing(): Promise<void> {
    const nextState = { ...this.state(), publishingPreset: null, step: 6 as const };
    if (await this.persistDraft(nextState)) {
      this.state.set(nextState);
    }
  }

  protected async selectPublishingPreset(): Promise<void> {
    this.setState({ ...this.state(), publishingPreset: 'MonoRepo' });
    await this.persistDraft();
  }

  protected async exitAssistant(): Promise<void> {
    if (!(await this.persistDraft())) {
      return;
    }
    await this.router.navigateByUrl('/');
  }

  protected async openProject(): Promise<void> {
    const project = this.createdProject();
    if (!project) {
      return;
    }
    await this.router.navigateByUrl(`/o/${this.organizationId}/projects/${project.id}`);
  }

  protected async retryLoad(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    await this.load();
  }

  protected async createProject(): Promise<void> {
    if (!(await this.validateStep(1)) || !(await this.validateStep(3))) {
      return;
    }
    if (!(await this.validateStep(4))) {
      return;
    }
    if (!this.metadata) {
      this.createError.set(true);
      return;
    }

    await this.persistDraft();
    this.creating.set(true);
    this.createError.set(false);
    const state = this.state();
    try {
      const result = await firstValueFrom(
        this.wizard.createProject({
          name: state.name.trim(),
          code: state.code.trim(),
          tenantId: this.metadata.tenantId,
          description: state.description.trim(),
          catalogVersion: this.metadata.catalogVersion,
          namingPreset: state.namingPreset,
          defaultNameTemplate: state.defaultNameTemplate,
          resourceGroupNameTemplate: state.resourceGroupNameTemplate,
          environments: state.environments.map((environment) => ({
            name: environment.name.trim(),
            code: environment.code.trim(),
            subscriptionId: environment.subscriptionId.trim(),
            region: environment.region,
            isProtected: environment.isProtected,
            approvers: this.parseApprovers(environment.approvers),
            description: environment.description.trim(),
          })),
          tags: {},
          publishingPreset: state.publishingPreset,
        }),
      );
      this.createdProject.set(result);
      this.validationError.set(null);
    } catch {
      this.createError.set(true);
    } finally {
      this.creating.set(false);
    }
  }

  protected stepTitleKey(): string {
    return (
      STEP_ITEMS.find((step) => step.value === this.state().step)?.titleKey ??
      'projectWizard.titles.identity'
    );
  }

  protected continueLabelKey(): string {
    const labels: Record<WizardStep, string> = {
      1: 'projectWizard.continueTools',
      2: 'projectWizard.continueEnvironments',
      3: 'projectWizard.continueNaming',
      4: 'projectWizard.continuePublishing',
      5: 'projectWizard.continueReview',
      6: 'projectWizard.create',
    };
    return labels[this.state().step];
  }

  protected hasValidIdentity(): boolean {
    const { name, code } = this.state();
    return (
      name.trim().length >= 3 && name.trim().length <= 80 && PROJECT_CODE_PATTERN.test(code.trim())
    );
  }

  protected environmentValid(environment: WizardEnvironment): boolean {
    return (
      environment.name.trim().length > 0 &&
      /^[a-z][a-z0-9]{1,7}$/.test(environment.code.trim()) &&
      GUID_PATTERN.test(environment.subscriptionId.trim()) &&
      this.metadata?.regions.some((region) => region.name === environment.region) === true &&
      (!environment.isProtected || this.parseApprovers(environment.approvers).length > 0)
    );
  }

  protected presetButtonLabel(preset: NamingPreset): string {
    return PRESET_LABEL_KEYS[preset];
  }

  private async validateStep(step: WizardStep): Promise<boolean> {
    this.validationError.set(null);
    if (step === 1 && !this.hasValidIdentity()) {
      this.validationError.set('identity');
      return false;
    }
    if (
      step === 3 &&
      (this.state().environments.length === 0 ||
        this.state().environments.some((environment) => !this.environmentValid(environment)))
    ) {
      this.validationError.set('environments');
      return false;
    }
    if (step === 4) {
      const preview = await this.refreshPreview();
      if (!preview || preview.issues.length > 0) {
        this.validationError.set('naming');
        return false;
      }
    }
    return true;
  }

  private setState(state: WizardState): void {
    this.state.set(state);
    this.scheduleSave();
    if (state.step === 4) {
      this.schedulePreview();
    }
  }

  private scheduleSave(): void {
    if (!this.ready()) {
      return;
    }
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
    }
    this.saveTimer = setTimeout(() => void this.persistDraft(), 400);
  }

  private schedulePreview(): void {
    if (this.previewTimer) {
      clearTimeout(this.previewTimer);
    }
    this.previewTimer = setTimeout(() => void this.refreshPreview(), 250);
  }

  private persistDraft(state = this.state()): Promise<boolean> {
    if (!this.ready()) {
      return Promise.resolve(false);
    }
    if (this.saveTimer) {
      clearTimeout(this.saveTimer);
      this.saveTimer = undefined;
    }
    const content = this.serializeState(state);
    this.saveQueue = this.saveQueue
      .catch(() => false)
      .then(async () => {
        this.saving.set(true);
        this.saveError.set(false);
        try {
          await firstValueFrom(this.wizard.saveDraft(content));
          this.draftSaved.set(true);
          return true;
        } catch {
          this.saveError.set(true);
          return false;
        } finally {
          this.saving.set(false);
        }
      });
    return this.saveQueue;
  }

  private serializeState(state: WizardState): JsonElement {
    return {
      step: state.step,
      name: state.name,
      code: state.code,
      description: state.description,
      codeManuallyEdited: state.codeManuallyEdited,
      environments: state.environments.map((environment) => ({ ...environment })),
      namingPreset: state.namingPreset,
      defaultNameTemplate: state.defaultNameTemplate,
      resourceGroupNameTemplate: state.resourceGroupNameTemplate,
      publishingPreset: state.publishingPreset,
    };
  }

  private async refreshPreview(): Promise<NamingPreviewResponse | null> {
    if (
      !this.metadata ||
      !this.state().name ||
      !this.state().code ||
      this.state().environments.length === 0
    ) {
      return null;
    }
    this.previewing.set(true);
    this.previewError.set(false);
    try {
      const state = this.state();
      const result = await firstValueFrom(
        this.wizard.previewNaming({
          projectName: state.name,
          projectCode: state.code,
          namingPreset: state.namingPreset,
          defaultNameTemplate: state.defaultNameTemplate,
          resourceGroupNameTemplate: state.resourceGroupNameTemplate,
          environments: state.environments.map((environment) => ({
            code: environment.code,
            region: environment.region,
          })),
          tags: {},
        }),
      );
      this.preview.set(result);
      return result;
    } catch {
      this.previewError.set(true);
      return null;
    } finally {
      this.previewing.set(false);
    }
  }

  private parseApprovers(value: string): string[] {
    return value
      .split(/[\n,;]/)
      .map((approver) => approver.trim())
      .filter(Boolean);
  }
}
