import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import {
  provideTransloco,
  TranslocoLoader,
  TranslocoService,
  Translation,
} from '@jsverse/transloco';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../core/http/api-error';
import { ComponentsService } from '../../core/projects/components.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectFindings } from './project-findings';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        loading: 'Chargement du projet…',
        validationTitle: 'Constats de validation',
        validationSummaryLabel: 'État de validation',
        validationCountsLabel: 'Nombre de constats par gravité',
        validationDescription:
          "Recalculés à chaque modification, version de modèle {{ version }}. Les mêmes constats sont visibles par l'API et les agents IA.",
        validationErrorOne: '{{ count }} erreur',
        validationErrors: '{{ count }} erreurs',
        validationGenerable: 'génération possible',
        validationBlocked: 'génération bloquée',
        validationWarningOne: '{{ count }} avertissement',
        validationWarnings: '{{ count }} avertissements',
        validationInfoOne: '{{ count }} info',
        validationInfos: '{{ count }} infos',
        validationAcknowledgedOne: '{{ count }} acquitté',
        validationAcknowledgedMany: '{{ count }} acquittés',
        validationPublicationErrorOne: '{{ count }} erreur à la publication',
        validationPublicationErrors: '{{ count }} erreurs à la publication',
        validationWarning: 'Avertissement',
        validationError: 'Erreur',
        validationPublishError: 'Erreur à la publication',
        validationInfo: 'Info',
        validationSeverityError: 'Erreur',
        validationSeverityPublishError: 'Erreur à la publication',
        validationSeverityWarning: 'Avertissement',
        validationSeverityInfo: 'Info',
        validationAcknowledged: 'Acquitté',
        validationApplyFix: 'Corriger',
        validationOpenNaming: 'Ouvrir le nommage',
        validationOpenSubject: 'Ouvrir l’objet concerné : {{ subject }}',
        validationFix: 'Corriger',
        validationAcknowledge: 'Acquitter…',
        validationAcknowledgmentTitle: 'Acquitter cet avertissement',
        validationComment: 'Commentaire obligatoire',
        validationSaveAcknowledgment: 'Enregistrer l’acquittement',
        validationCancel: 'Annuler',
        validationLoadError: 'Les constats n’ont pas pu être chargés.',
        validationActionError: 'L’action n’a pas pu être enregistrée.',
        validationConflict:
          'Le modèle a changé. Les constats ont été rechargés ; vérifiez-les avant de réessayer.',
        validationActionSaved: 'Action enregistrée.',
        retry: 'Réessayer',
      },
    });
  }
}

const projectId = '77777777-7777-4777-8777-777777777777';
const resourceId = '99999999-9999-4999-8999-999999999999';
const environmentId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

function response(overrides: Record<string, unknown> = {}) {
  return {
    projectId,
    modelVersion: 12,
    catalogVersion: '2026.10-pilot.3',
    errorCount: 0,
    publicationErrorCount: 0,
    warningCount: 1,
    informationCount: 1,
    acknowledgedCount: 0,
    readiness: {
      modelValid: true,
      revisionGenerable: true,
      publishable: true,
      deployable: false,
    },
    orderedComponents: [],
    findings: [
      {
        code: 'VAL-LIA-SQL-ADMIN',
        severity: 'Warning',
        lot: 'lot1',
        subjectType: 'Resource',
        subjectId: resourceId,
        environmentId,
        messageFr: 'Le contrôle du groupe administrateur SQL reste à confirmer.',
        messageEn: 'SQL administrator group membership still needs confirmation.',
        objectVersion: 4,
        fixCode: null,
        canApplyFix: false,
        relatedLinkIds: [],
        acknowledgment: null,
        detailKey: null,
        subjectContext: 'orders / sql orders',
        subjectComponentId: '88888888-8888-4888-8888-888888888888',
        subjectResourceId: resourceId,
        environmentCode: 'prd',
      },
      {
        code: 'VAL-LIA-PORTEE-EXTERNE',
        severity: 'Info',
        lot: 'lot1',
        subjectType: 'Resource',
        subjectId: resourceId,
        environmentId: null,
        messageFr: 'Une attribution de rôle est faite hors de l’abonnement du composant.',
        messageEn: 'A role assignment is made outside the component subscription.',
        objectVersion: 4,
        fixCode: null,
        canApplyFix: false,
        relatedLinkIds: [],
        acknowledgment: null,
        detailKey: null,
        subjectContext: 'orders / sql orders',
        subjectComponentId: '88888888-8888-4888-8888-888888888888',
        subjectResourceId: resourceId,
        environmentCode: null,
      },
    ],
    ...overrides,
  };
}

async function renderFindings(
  validationService: { validation: ReturnType<typeof vi.fn> },
  projectService = {
    myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
    acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
    applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
  },
) {
  await TestBed.configureTestingModule({
    imports: [ProjectFindings],
    providers: [
      provideRouter(
        [{ path: 'o/:orgId/projects/:projectId/findings', component: ProjectFindings }],
        withComponentInputBinding(),
      ),
      {
        provide: ProjectsService,
        useValue: {
          ...projectService,
          validation: validationService.validation,
          environments: vi.fn().mockReturnValue(of([])),
        },
      },
      { provide: ComponentsService, useValue: { list: vi.fn().mockReturnValue(of([])) } },
      provideTransloco({
        config: {
          availableLangs: ['fr'],
          defaultLang: 'fr',
          fallbackLang: 'fr',
          reRenderOnLangChange: true,
          prodMode: true,
        },
        loader: StaticTranslationLoader,
      }),
    ],
  }).compileComponents();

  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(
    `/o/33333333-3333-4333-8333-333333333333/projects/${projectId}/findings`,
    ProjectFindings,
  );
  await harness.fixture.whenStable();
  harness.fixture.detectChanges();
  return { harness, element: harness.routeNativeElement!, projectService };
}

describe('ProjectFindings', () => {
  it('renders API findings with readiness and current model context', async () => {
    const validationService = { validation: vi.fn().mockReturnValue(of(response())) };
    const { element } = await renderFindings(validationService);

    expect(element.textContent).toContain('Constats de validation');
    expect(element.textContent).toContain('version de modèle 12');
    expect(element.textContent).toContain('0 erreur · génération possible');
    expect(element.textContent).toContain('1 avertissement');
    expect(element.textContent).toContain('1 info');
    expect(element.textContent).toContain('orders / sql orders');
    expect(element.textContent).toContain('prd');

    const summary = element.querySelector('.project-findings__summary');
    const readinessBadge = summary?.querySelector('.project-findings__readiness');
    expect(readinessBadge?.tagName.toLowerCase()).toBe('app-ds-badge');
    expect(readinessBadge?.querySelector('.st-badge-success')).not.toBeNull();
    expect(summary?.querySelectorAll('app-ds-badge').length).toBe(3);
  });

  it('shows blocked readiness with its error count in one danger badge', async () => {
    const blockedResponse = response({
      errorCount: 1,
      warningCount: 0,
      informationCount: 0,
      readiness: {
        modelValid: false,
        revisionGenerable: false,
        publishable: false,
        deployable: false,
      },
      findings: [
        {
          ...response().findings[0],
          code: 'VAL-CMP-CYCLE',
          severity: 'Error',
        },
      ],
    });
    const validationService = { validation: vi.fn().mockReturnValue(of(blockedResponse)) };
    const { element } = await renderFindings(validationService);
    const summary = element.querySelector('.project-findings__summary');
    const readinessBadge = summary?.querySelector('.project-findings__readiness');

    expect(readinessBadge?.querySelector('.st-badge-danger')).not.toBeNull();
    expect(readinessBadge?.textContent).toContain('1 erreur · génération bloquée');
    expect(summary?.querySelectorAll('app-ds-badge').length).toBe(1);
  });

  it('labels a finding context link with the affected object name', async () => {
    const validationService = { validation: vi.fn().mockReturnValue(of(response())) };
    const { element } = await renderFindings(validationService);
    const subjectLink = element.querySelector<HTMLAnchorElement>('.project-findings__context a');

    expect(subjectLink?.getAttribute('aria-label')).toBe(
      'Ouvrir l’objet concerné : orders / sql orders',
    );
  });

  it('routes link and environment findings to their related model screens', async () => {
    const linkFinding = {
      ...response().findings[0],
      code: 'VAL-LIA-LINK',
      subjectType: 'Link',
      subjectComponentId: '88888888-8888-4888-8888-888888888888',
      subjectResourceId: resourceId,
      environmentId: null,
      environmentCode: null,
      subjectContext: 'api → core / main',
    };
    const environmentFinding = {
      ...response().findings[1],
      code: 'VAL-ENVIRONMENT',
      subjectType: 'Environment',
      environmentId,
      subjectComponentId: null,
      subjectResourceId: null,
      subjectContext: 'prd',
    };
    const { element } = await renderFindings({
      validation: vi
        .fn()
        .mockReturnValue(of(response({ findings: [linkFinding, environmentFinding] }))),
    });
    const contextLinks = [
      ...element.querySelectorAll<HTMLAnchorElement>('.project-findings__context a'),
    ];

    expect(contextLinks).toHaveLength(2);
    expect(contextLinks[0].getAttribute('href')).toContain(
      `/components/88888888-8888-4888-8888-888888888888/resources/${resourceId}?tab=links`,
    );
    expect(contextLinks[1].getAttribute('href')).toContain(`/environments/${environmentId}`);
  });

  it('requires a comment and records an acknowledgment for the current finding version', async () => {
    const acknowledgedResponse = response({
      acknowledgedCount: 1,
      findings: [
        {
          ...response().findings[0],
          acknowledgment: {
            comment: 'Vérifié avec l’équipe plateforme.',
            acknowledgedBy: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
            acknowledgedByDisplayName: 'Alice Martin',
            createdAt: '2026-10-29T12:00:00Z',
          },
        },
        response().findings[1],
      ],
    });
    const validation = vi
      .fn()
      .mockReturnValueOnce(
        of(
          response({
            findings: [{ ...response().findings[0], objectVersion: '4' }, response().findings[1]],
          }),
        ),
      )
      .mockReturnValueOnce(of(acknowledgedResponse));
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings({ validation }, projectService);

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = '  Vérifié avec l’équipe plateforme.  ';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.acknowledgeFinding).toHaveBeenCalledWith(
      projectId,
      'VAL-LIA-SQL-ADMIN',
      expect.objectContaining({
        comment: 'Vérifié avec l’équipe plateforme.',
        subjectId: resourceId,
        subjectType: 'Resource',
        environmentId,
        expectedObjectVersion: 4,
      }),
    );
    expect(element.textContent).toContain('Acquitté');
    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
    const savedStatus = element.querySelector<HTMLParagraphElement>('.project-findings__saved');
    expect(savedStatus).not.toBeNull();
    expect(document.activeElement).toBe(savedStatus);
  });

  it('rejects a comment containing only whitespace', async () => {
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(response())) },
      projectService,
    );

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = '   ';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.acknowledgeFinding).not.toHaveBeenCalled();
    expect(comment.getAttribute('aria-invalid')).toBe('true');
    expect(comment.getAttribute('aria-describedby')).toBe('finding-acknowledgment-comment-error');
    expect(dialog.textContent).toContain('Commentaire obligatoire');
  });

  it('does not offer acknowledgment when the finding version is missing', async () => {
    const findingWithoutVersion = {
      ...response().findings[0],
      objectVersion: null,
    };
    const { element } = await renderFindings({
      validation: vi
        .fn()
        .mockReturnValue(
          of(response({ findings: [findingWithoutVersion, response().findings[1]] })),
        ),
    });

    expect(
      [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')].some(
        (button) => button.textContent?.trim() === 'Acquitter…',
      ),
    ).toBe(false);
  });

  it('does not offer acknowledgment for blank or unsafe finding versions', async () => {
    const defaultFinding = response().findings[0];
    const findingsWithInvalidVersions = [
      { ...defaultFinding, objectVersion: ' ' },
      { ...defaultFinding, code: 'VAL-UNSAFE-VERSION', objectVersion: '9007199254740993' },
      { ...defaultFinding, code: 'VAL-ZERO-VERSION', objectVersion: 0 },
      response().findings[1],
    ];
    const { element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response({ findings: findingsWithInvalidVersions }))),
    });

    expect(
      [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')].some(
        (button) => button.textContent?.trim() === 'Acquitter…',
      ),
    ).toBe(false);
  });

  it('renders duplicate finding identities without an Angular tracking warning', async () => {
    const duplicate = {
      ...response().findings[0],
      messageFr: 'Second detail on the same resource.',
      messageEn: 'Second detail on the same resource.',
    };
    const first = response().findings[0];
    const exactDuplicate = { ...first };
    const { harness, element } = await renderFindings({
      validation: vi
        .fn()
        .mockReturnValue(
          of(response({ findings: [first, duplicate, exactDuplicate, response().findings[1]] })),
        ),
    });
    const component = harness.routeDebugElement?.componentInstance as ProjectFindings;
    expect(component).toBeDefined();
    const componentWithFindingKey = component as unknown as {
      findingKey: (
        finding: ReturnType<typeof response>['findings'][number],
        index: number,
      ) => string;
    };
    const keys = [first, duplicate, exactDuplicate].map((finding, index) =>
      componentWithFindingKey.findingKey(finding, index),
    );

    expect(new Set(keys).size).toBe(3);
    expect(element.querySelectorAll('.project-findings__item')).toHaveLength(4);
  });

  it('keeps a unique finding tracking key stable when its list position changes', async () => {
    const findings = response().findings;
    const { harness } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response({ findings }))),
    });
    const component = harness.routeDebugElement?.componentInstance as ProjectFindings;
    const componentWithFindingKey = component as unknown as {
      findingKey: (
        finding: ReturnType<typeof response>['findings'][number],
        index: number,
      ) => string;
    };

    expect(componentWithFindingKey.findingKey(findings[1], 1)).toBe(
      componentWithFindingKey.findingKey(findings[1], 20),
    );
  });

  it('does not offer actions for a finding with an unknown subject type', async () => {
    const invalidFinding = {
      ...response().findings[0],
      subjectType: 'UnknownObjectType',
      fixCode: 'VAL-PAR-SECRET-SUSPECT',
      canApplyFix: true,
    };
    const { element } = await renderFindings({
      validation: vi
        .fn()
        .mockReturnValue(of(response({ findings: [invalidFinding, response().findings[1]] }))),
    });

    const actionButtons = [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')];
    expect(actionButtons.some((button) => button.textContent?.trim() === 'Acquitter…')).toBe(false);
    expect(actionButtons.some((button) => button.textContent?.trim() === 'Corriger')).toBe(false);
  });

  it('shows an error if edit permission disappears while the acknowledgment dialog is open', async () => {
    const projectService = {
      myPermissions: vi
        .fn()
        .mockReturnValueOnce(of({ permissions: ['model.edit'] }))
        .mockReturnValueOnce(of({ permissions: [] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(response())) },
      projectService,
    );
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const component = harness.routeDebugElement?.componentInstance as ProjectFindings;
    (component as unknown as { retry: () => void }).retry();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    await harness.fixture.whenStable();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.acknowledgeFinding).not.toHaveBeenCalled();
    expect(dialog.querySelector('[role="alert"]')?.textContent).toContain(
      'L’action n’a pas pu être enregistrée.',
    );
  });

  it('updates finding messages when the active language changes', async () => {
    const { element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response())),
    });
    const transloco = TestBed.inject(TranslocoService);

    expect(element.textContent).toContain(
      'Le contrôle du groupe administrateur SQL reste à confirmer.',
    );
    transloco.setActiveLang('en');
    await vi.waitFor(() =>
      expect(element.textContent).toContain(
        'SQL administrator group membership still needs confirmation.',
      ),
    );
  });

  it('retries loading after the initial validation request fails', async () => {
    const validation = vi
      .fn()
      .mockReturnValueOnce(throwError(() => new Error('temporary failure')))
      .mockReturnValueOnce(of(response()));
    const { harness, element } = await renderFindings({ validation });
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Les constats n’ont pas pu être chargés.',
    );

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Réessayer')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(validation).toHaveBeenCalledTimes(2);
    expect(element.querySelector('[role="alert"]')).toBeNull();
    expect(element.textContent).toContain('Constats de validation');
  });

  it('reloads findings after an acknowledgment version conflict', async () => {
    const validation = vi
      .fn()
      .mockReturnValueOnce(of(response()))
      .mockReturnValueOnce(of(response({ modelVersion: 13 })));
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi
        .fn()
        .mockReturnValue(
          throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'The model changed.')),
        ),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings({ validation }, projectService);
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    await harness.fixture.whenStable();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    await harness.fixture.whenStable();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
    expect(validation).toHaveBeenCalledTimes(2);
    expect(element.textContent).toContain('version de modèle 13');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Le modèle a changé.');

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    await harness.fixture.whenStable();
    const retryDialog = document.querySelector<HTMLElement>(
      '.cdk-overlay-container [role="dialog"]',
    )!;
    expect(retryDialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')?.value).toBe(
      'Commentaire de recette',
    );
  });

  it('applies a supported correction and reloads the current findings', async () => {
    const fixableWarning = response({
      findings: [
        {
          ...response().findings[0],
          fixCode: 'VAL-PAR-SECRET-SUSPECT',
          canApplyFix: true,
        },
        response().findings[1],
      ],
    });
    const refreshed = response({
      modelVersion: 13,
      warningCount: 0,
      findings: [response().findings[1]],
    });
    const validation = vi
      .fn()
      .mockReturnValueOnce(of(fixableWarning))
      .mockReturnValueOnce(of(refreshed));
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings({ validation }, projectService);
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Corriger')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.applyFindingFix).toHaveBeenCalledOnce();
    expect(validation).toHaveBeenCalledTimes(2);
    expect(element.textContent).toContain('version de modèle 13');
    expect(element.querySelector('.project-findings__saved')?.textContent).toContain(
      'Action enregistrée.',
    );
  });

  it('reloads findings after a correction version conflict', async () => {
    const fixableWarning = response({
      findings: [
        {
          ...response().findings[0],
          fixCode: 'VAL-PAR-SECRET-SUSPECT',
          canApplyFix: true,
        },
        response().findings[1],
      ],
    });
    const validation = vi
      .fn()
      .mockReturnValueOnce(of(fixableWarning))
      .mockReturnValueOnce(of(response({ modelVersion: 13 })));
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi
        .fn()
        .mockReturnValue(
          throwError(() => new ApiError(409, 'CONFLICT_VERSION', 'The model changed.')),
        ),
    };
    const { harness, element } = await renderFindings({ validation }, projectService);
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Corriger')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.applyFindingFix).toHaveBeenCalledOnce();
    expect(validation).toHaveBeenCalledTimes(2);
    expect(element.textContent).toContain('version de modèle 13');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Le modèle a changé.');
  });

  it('limits acknowledgment comments to the API maximum length', async () => {
    const { harness, element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response())),
    });
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    await harness.fixture.whenStable();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;

    expect(dialog.querySelector('textarea')?.getAttribute('maxlength')).toBe('2000');
  });

  it('updates the acknowledgment dialog from signals without manual fixture detection', async () => {
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(throwError(() => new Error('request failed'))),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(response())) },
      projectService,
    );
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    await harness.fixture.whenStable();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    await harness.fixture.whenStable();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();

    expect(dialog.querySelector('[role="alert"]')?.textContent).toContain(
      'L’action n’a pas pu être enregistrée.',
    );
    expect(document.querySelectorAll('[role="alert"]')).toHaveLength(1);
  });

  it('shows correction errors and re-enables the action', async () => {
    const fixableWarning = response({
      findings: [
        {
          ...response().findings[0],
          fixCode: 'VAL-PAR-SECRET-SUSPECT',
          canApplyFix: true,
        },
        response().findings[1],
      ],
    });
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(throwError(() => new Error('request failed'))),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(fixableWarning)) },
      projectService,
    );
    const fixButton = [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')].find(
      (button) => button.textContent?.trim() === 'Corriger',
    )!;
    fixButton.click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.applyFindingFix).toHaveBeenCalledOnce();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'L’action n’a pas pu être enregistrée.',
    );
    expect(fixButton.disabled).toBe(false);
  });

  it('closes the acknowledgment dialog on Escape and restores focus to its trigger', async () => {
    const { harness, element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response())),
    });
    const trigger = [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')].find(
      (button) => button.textContent?.trim() === 'Acquitter…',
    )!;
    trigger.click();
    harness.fixture.detectChanges();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    await vi.waitFor(() => expect(dialog.contains(document.activeElement)).toBe(true));
    document.activeElement?.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('closes the acknowledgment dialog when the component is destroyed', async () => {
    const { harness, element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response())),
    });
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).not.toBeNull();

    harness.fixture.destroy();

    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
  });

  it('closes the acknowledgment dialog when the project changes', async () => {
    const { harness, element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(response())),
    });
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).not.toBeNull();

    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/88888888-8888-4888-8888-888888888888/findings`,
      ProjectFindings,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
  });

  it('does not show success on another project when the acknowledgment request finishes late', async () => {
    let finishAcknowledgment: (() => void) | undefined;
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(
        new Observable<void>((subscriber) => {
          finishAcknowledgment = () => {
            subscriber.next();
            subscriber.complete();
          };
        }),
      ),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(response())) },
      projectService,
    );
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    harness.fixture.detectChanges();

    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/88888888-8888-4888-8888-888888888888/findings`,
      ProjectFindings,
    );
    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
    finishAcknowledgment?.();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(element.querySelector('.project-findings__saved')).toBeNull();
    expect(document.activeElement).not.toBe(element.querySelector('.project-findings__saved'));
  });

  it('keeps the acknowledgment dialog open and shows an error when saving fails', async () => {
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(throwError(() => new Error('request failed'))),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(response())) },
      projectService,
    );

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(projectService.acknowledgeFinding).toHaveBeenCalledOnce();
    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).not.toBeNull();
    expect(dialog.querySelector('[role="alert"]')?.textContent).toContain(
      'L’action n’a pas pu être enregistrée.',
    );
    expect(document.querySelectorAll('[role="alert"]')).toHaveLength(1);
  });

  it('keeps and focuses the success status when reloading findings fails', async () => {
    const validation = vi
      .fn()
      .mockReturnValueOnce(of(response()))
      .mockReturnValueOnce(throwError(() => new Error('refresh failed')));
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings({ validation }, projectService);
    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    const savedStatus = element.querySelector<HTMLParagraphElement>('.project-findings__saved');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Les constats n’ont pas pu être chargés.',
    );
    expect(savedStatus?.textContent).toContain('Action enregistrée.');
    expect(document.activeElement).toBe(savedStatus);
  });

  it('keeps the acknowledgment dialog open when Escape is pressed while saving', async () => {
    let finishAcknowledgment: (() => void) | undefined;
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(
        new Observable<void>((subscriber) => {
          finishAcknowledgment = () => {
            subscriber.next();
            subscriber.complete();
          };
        }),
      ),
      applyFindingFix: vi.fn().mockReturnValue(of(undefined)),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(response())) },
      projectService,
    );

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Acquitter…')!
      .click();
    harness.fixture.detectChanges();
    const dialog = document.querySelector<HTMLElement>('.cdk-overlay-container [role="dialog"]')!;
    const comment = dialog.querySelector<HTMLTextAreaElement>('textarea[name="comment"]')!;
    comment.value = 'Commentaire de recette';
    comment.dispatchEvent(new Event('input', { bubbles: true }));
    harness.fixture.detectChanges();
    [...dialog.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Enregistrer l’acquittement')!
      .click();
    harness.fixture.detectChanges();
    document.activeElement?.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );

    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).not.toBeNull();
    finishAcknowledgment?.();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    expect(document.querySelector('.cdk-overlay-container [role="dialog"]')).toBeNull();
  });

  it('keeps acknowledgment available alongside a safe one-click correction', async () => {
    const fixableWarning = response({
      findings: [
        {
          ...response().findings[0],
          code: 'VAL-PAR-SECRET-SUSPECT',
          fixCode: 'VAL-PAR-SECRET-SUSPECT',
          canApplyFix: true,
        },
        response().findings[1],
      ],
    });
    const { element } = await renderFindings({
      validation: vi.fn().mockReturnValue(of(fixableWarning)),
    });

    const actionLabels = [
      ...element.querySelectorAll<HTMLButtonElement>('app-ds-button button'),
    ].map((button) => button.textContent?.trim());
    expect(actionLabels).toContain('Corriger');
    expect(actionLabels).toContain('Acquitter…');
  });

  it('does not show a late correction result on another project', async () => {
    let finishFix: (() => void) | undefined;
    const fixableWarning = response({
      findings: [
        {
          ...response().findings[0],
          fixCode: 'VAL-PAR-SECRET-SUSPECT',
          canApplyFix: true,
        },
        response().findings[1],
      ],
    });
    const projectService = {
      myPermissions: vi.fn().mockReturnValue(of({ permissions: ['model.edit'] })),
      acknowledgeFinding: vi.fn().mockReturnValue(of(undefined)),
      applyFindingFix: vi.fn().mockReturnValue(
        new Observable<void>((subscriber) => {
          finishFix = () => {
            subscriber.next();
            subscriber.complete();
          };
        }),
      ),
    };
    const { harness, element } = await renderFindings(
      { validation: vi.fn().mockReturnValue(of(fixableWarning)) },
      projectService,
    );

    [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .find((button) => button.textContent?.trim() === 'Corriger')!
      .click();
    harness.fixture.detectChanges();
    const acknowledgmentButton = [
      ...element.querySelectorAll<HTMLButtonElement>('app-ds-button button'),
    ].find((button) => button.textContent?.trim() === 'Acquitter…')!;
    expect(acknowledgmentButton.disabled).toBe(true);

    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/88888888-8888-4888-8888-888888888888/findings`,
      ProjectFindings,
    );
    finishFix?.();
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(element.querySelector('.project-findings__saved')).toBeNull();
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it('ignores a validation response from the previous project when it arrives late', async () => {
    let finishPreviousProjectLoad: ((value: ReturnType<typeof response>) => void) | undefined;
    const validation = vi
      .fn()
      .mockReturnValueOnce(
        new Observable<ReturnType<typeof response>>((subscriber) => {
          finishPreviousProjectLoad = (value) => {
            subscriber.next(value);
            subscriber.complete();
          };
        }),
      )
      .mockReturnValueOnce(of(response({ modelVersion: 21 })));
    const { harness, element } = await renderFindings({ validation });
    await harness.navigateByUrl(
      `/o/33333333-3333-4333-8333-333333333333/projects/88888888-8888-4888-8888-888888888888/findings`,
      ProjectFindings,
    );
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();
    expect(element.textContent).toContain('version de modèle 21');

    finishPreviousProjectLoad?.(response({ modelVersion: 20 }));
    await harness.fixture.whenStable();
    harness.fixture.detectChanges();

    expect(element.textContent).toContain('version de modèle 21');
    expect(element.textContent).not.toContain('version de modèle 20');
  });
});
