import { provideRouter } from '@angular/router';
import { render, screen, fireEvent } from '@testing-library/angular';
import { TranslocoLoader, Translation, provideTransloco } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  ProjectValidationFindingResponse,
  ProjectValidationResponse,
  RevisionSummaryResponse,
} from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { RevisionsService } from '../../core/revisions/revisions.service';
import { RevisionsPage } from './revisions-page';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      revisions: {
        title: 'Révisions',
        description: 'Chaque révision est un instantané immuable.',
        generate: 'Générer la révision {{ number }}',
        loading: 'Chargement des révisions…',
        loadError: 'Impossible de charger les révisions.',
        loadErrorDescription: 'Rechargez la page.',
        generationError: 'La génération n’a pas pu démarrer.',
        generationErrorDescription: 'Vérifiez les constats.',
        blockedTitle: 'Génération impossible',
        blockedDescription: 'La révision {{ number }} ne peut pas être générée.',
        nonBlockingTitle: 'Non bloquants',
        blockedSummaryOne: '1 erreur bloque la génération.',
        blockedSummaryMany: '{{ count }} erreurs bloquent la génération.',
        blockedWarningsReported: 'Les avertissements, eux, seront reportés dans la révision.',
        closeBlocked: 'Fermer',
        viewFindings: 'Voir tous les constats',
        listTitle: 'Révisions',
        retention: 'Les fichiers sont conservés 90 jours.',
        empty: 'Aucune révision.',
        tableLabel: 'Révisions du projet',
        columnNumber: 'N°',
        columnGenerated: 'Générée',
        columnModel: 'Modèle',
        columnCatalog: 'Catalogue',
        columnChanges: 'Changements',
        columnState: 'État',
        author: 'Auteur',
        revisionNumber: 'Révision {{ number }}',
        stateQueued: 'En file',
        stateGenerating: 'Génération en cours',
        stateGenerated: 'Générée',
        stateFailed: 'Échec',
        stateUnknown: 'État inconnu',
        queued: 'Révision {{ number }} en file, position {{ position }}.',
        generationRequested: 'La demande est enregistrée.',
        noChanges: 'Aucun changement depuis la révision {{ number }}.',
        changeCountOne: '{{ count }} changement',
        changeCountMany: '{{ count }} changements',
        stale: 'Périmée',
        queuePosition: 'Position {{ position }} dans la file',
        incident: 'Incident IFS',
      },
    });
  }
}

const revision: RevisionSummaryResponse = {
  authorObjectId: 'object-id',
  authorTenantId: 'tenant-id',
  catalogVersion: '2026.09',
  changesCount: 0,
  ciPlatform: 'AzureDevOps',
  completedAt: '2026-11-13T10:00:00Z',
  createdAt: '2026-11-13T09:59:00Z',
  failureCode: null,
  filesCount: 0,
  findingsCount: 0,
  id: 'revision-id',
  infrastructureLanguage: 'Bicep',
  inputSha256: 'sha256',
  isStale: false,
  modelVersion: 3,
  number: 1,
  queuePosition: null,
  startedAt: '2026-11-13T09:59:10Z',
  state: 'Generated',
};

function validation(
  revisionGenerable: boolean,
  findings: ProjectValidationFindingResponse[] = [],
): ProjectValidationResponse {
  return {
    acknowledgedCount: 0,
    catalogVersion: '2026.09',
    errorCount: findings.filter((finding) => finding.severity === 'Error').length,
    findings,
    informationCount: 0,
    modelVersion: 3,
    orderedComponents: [],
    projectId: 'project-id',
    publicationErrorCount: 0,
    readiness: {
      deployable: false,
      modelValid: revisionGenerable,
      publishable: false,
      revisionGenerable,
    },
    warningCount: 0,
  };
}

async function renderPage(
  revisionGenerable: boolean,
  findings: ProjectValidationFindingResponse[] = [],
) {
  const revisions = {
    list: vi.fn().mockReturnValue(of({ items: [revision], nextNumber: 2 })),
    generate: vi.fn().mockReturnValue(
      of({
        isUnchanged: true,
        number: 1,
        queuePosition: null,
        revisionId: revision.id,
        state: 'Generated',
      }),
    ),
  };
  const projects = {
    validation: vi.fn().mockReturnValue(of(validation(revisionGenerable, findings))),
  };
  await render(RevisionsPage, {
    inputs: { projectId: 'project-id' },
    providers: [
      provideRouter([]),
      { provide: RevisionsService, useValue: revisions },
      { provide: ProjectsService, useValue: projects },
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
  });
  return revisions;
}

describe('RevisionsPage', () => {
  it('shows the server supplied next number and immutable revisions', async () => {
    await renderPage(true);

    expect(await screen.findByRole('button', { name: 'Générer la révision 2' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Révision 1' })).toBeTruthy();
    expect(screen.getByText('2026.09')).toBeTruthy();
  });

  it('reports an unchanged request without creating a new revision', async () => {
    const revisions = await renderPage(true);

    fireEvent.click(await screen.findByRole('button', { name: 'Générer la révision 2' }));

    expect(revisions.generate).toHaveBeenCalledWith('project-id');
    expect(await screen.findByText('Aucun changement depuis la révision 1.')).toBeTruthy();
  });

  it('shows a blocking dialog with blocking and non-blocking findings', async () => {
    const blockingFinding = {
      code: 'VAL-RES-EXISTANTE-ID',
      messageFr: 'Identifiant Azure manquant en prd.',
      messageEn: 'Missing Azure identifier in prd.',
      severity: 'Error',
      subjectId: 'resource-id',
      subjectContext: 'core / log corp',
    } as ProjectValidationFindingResponse;
    const warningFinding = {
      code: 'VAL-LIA-SQL-ADMIN',
      messageFr: 'L’appartenance au groupe SQL reste à confirmer.',
      messageEn: 'SQL group membership is not confirmed.',
      severity: 'Warning',
      subjectId: 'sql-admin-link',
      subjectContext: 'orders / sql',
    } as ProjectValidationFindingResponse;
    await renderPage(false, [blockingFinding, warningFinding]);

    const dialog = await screen.findByRole('dialog', {
      name: 'La révision 2 ne peut pas être générée.',
    });
    expect(dialog).toBeTruthy();
    expect(screen.getByText(/1 erreur bloque la génération/)).toBeTruthy();
    expect(screen.getByText('core / log corp')).toBeTruthy();
    expect(screen.getByText('Identifiant Azure manquant en prd.')).toBeTruthy();
    expect(screen.getByText(/Les avertissements, eux, seront reportés dans la révision/)).toBeTruthy();
    expect(screen.getByText('L’appartenance au groupe SQL reste à confirmer.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Générer la révision 2' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Voir tous les constats' })).toBeTruthy();
  });
});
