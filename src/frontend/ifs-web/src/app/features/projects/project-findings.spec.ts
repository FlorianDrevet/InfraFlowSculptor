import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
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
        validationComment: 'Commentaire obligatoire',
        validationSaveAcknowledgment: 'Enregistrer l’acquittement',
        validationCancel: 'Annuler',
        validationLoadError: 'Les constats n’ont pas pu être chargés.',
        validationActionError: 'L’action n’a pas pu être enregistrée.',
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
          reRenderOnLangChange: false,
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
    expect(element.textContent).toContain("version de modèle 12");
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
      .mockReturnValueOnce(of(response()))
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
    const dialog = element.querySelector<HTMLElement>('[role="dialog"]')!;
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
    const { element } = await renderFindings({ validation: vi.fn().mockReturnValue(of(fixableWarning)) });

    const actionLabels = [...element.querySelectorAll<HTMLButtonElement>('app-ds-button button')]
      .map((button) => button.textContent?.trim());
    expect(actionLabels).toContain('Corriger');
    expect(actionLabels).toContain('Acquitter…');
  });
});
