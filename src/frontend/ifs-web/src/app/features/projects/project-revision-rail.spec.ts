import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { render, screen, waitFor } from '@testing-library/angular';
import { TranslocoLoader, Translation, provideTransloco } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { ProjectValidationResponse, RevisionGenerationResponse } from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { RevisionsService } from '../../core/revisions/revisions.service';
import { ProjectRevisionRail } from './project-revision-rail';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        revisionStepModel: 'Modéliser',
        revisionStepGenerate: 'Générer',
        revisionModelBlocked: 'Corrigez les erreurs de validation avant de générer.',
        noRevisionGenerated: 'Aucune révision générée',
        revisionRailLabel: 'Étapes de génération',
        revisionRailError: 'Les révisions n’ont pas pu être chargées.',
        revisionRailErrorDescription: 'Réessayez depuis la page des révisions.',
      },
      revisions: {
        generate: 'Générer la révision {{ number }}',
        loading: 'Chargement des révisions…',
        blockedTitle: 'Génération impossible',
        viewFindings: 'Voir tous les constats',
        stateGeneratedNumber: 'Révision {{ number }} générée',
      },
    });
  }
}

function validation(revisionGenerable: boolean): ProjectValidationResponse {
  return {
    acknowledgedCount: 0,
    catalogVersion: '2026.09',
    errorCount: 0,
    findings: [],
    informationCount: 0,
    modelVersion: 1,
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

async function renderRail(
  revisionGenerable: boolean,
  generationResult: RevisionGenerationResponse = {
    isUnchanged: false,
    number: 4,
    queuePosition: 1,
    revisionId: 'revision-id',
    state: 'Queued',
  },
) {
  await render(ProjectRevisionRail, {
    inputs: { projectId: 'project-id' },
    providers: [
      provideRouter([]),
      {
        provide: RevisionsService,
        useValue: {
          list: vi.fn().mockReturnValue(of({ items: [], nextNumber: 4 })),
          generate: vi.fn().mockReturnValue(of(generationResult)),
        },
      },
      {
        provide: ProjectsService,
        useValue: { validation: vi.fn().mockReturnValue(of(validation(revisionGenerable))) },
      },
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
}

describe('ProjectRevisionRail', () => {
  it('shows only the Modéliser and Générer steps and uses the server next number', async () => {
    await renderRail(true);

    expect(await screen.findByRole('button', { name: 'Générer la révision 4' })).toBeTruthy();
    const rail = screen.getByRole('list', { name: 'Étapes de génération' });
    expect(rail.textContent).toContain('Modéliser');
    expect(rail.textContent).toContain('Générer');
    expect(rail.textContent).not.toContain('Publier');
    expect(rail.textContent).not.toContain('Déployer');
  });

  it('removes the generation action when the server marks the model as blocked', async () => {
    await renderRail(false);

    expect(await screen.findByText('Génération impossible')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Générer la révision 4' })).toBeNull();
    expect(screen.getByRole('link', { name: 'Voir tous les constats' })).toBeTruthy();
  });

  it('navigates to the revision detail under the current project route', async () => {
    await renderRail(true);
    const generateButton = await screen.findByRole('button', { name: 'Générer la révision 4' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    generateButton.click();

    await waitFor(() => expect(navigate).toHaveBeenCalled());
    const [commands, options] = navigate.mock.calls[0] ?? [];
    expect(commands).toEqual(['revisions', 'revision-id']);
    expect(options?.relativeTo).toBeTruthy();
  });
});
