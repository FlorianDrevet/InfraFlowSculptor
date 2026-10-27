import { ActivatedRoute, Router } from '@angular/router';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular/zoneless';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { ProjectWizardService } from '../../core/projects/project-wizard.service';
import { ProjectWizard } from './project-wizard';

const organizationId = '33333333-3333-4333-8333-333333333333';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      projectWizard: {
        navigation: 'Étapes de création du projet',
        quit: 'Quitter l’assistant',
        newProject: 'Nouveau projet',
        loading: 'Chargement de l’assistant…',
        savedDraft: 'Brouillon enregistré sur le serveur',
        saveError: 'Le brouillon n’a pas pu être enregistré. Réessayez avant de quitter.',
        stepCount: 'Étape {{ step }} sur 6',
        optionalStep: 'Étape 5 sur 6 · facultative',
        steps: {
          identity: 'Identité',
          tools: 'Outils',
          environments: 'Environnements',
          naming: 'Nommage',
          publishing: 'Publication',
          reviewStep: 'Récapitulatif',
        },
        titles: {
          identity: 'Identité du projet',
          tools: 'Vos outils',
          environments: 'Chaîne d’environnements',
          naming: 'Convention de nommage',
          publishing: 'Où publier les fichiers',
          reviewStep: 'Récapitulatif',
        },
        toolsHeading: 'Vos outils',
        identityHelper:
          'Le code apparaît dans tous les noms Azure, les dossiers et les pipelines. Choisissez-le court et stable.',
        projectNameHint: '3 à 80 caractères, unique dans {{ organization }}',
        projectCodeHint: '2 à 10 caractères : minuscules et chiffres, commence par une lettre.',
        projectName: 'Nom',
        projectCode: 'Code',
        description: 'Description',
        publishingHelper: 'Le plan de publication est facultatif.',
        publishingPreset: 'Préréglage',
        monoRepoDescription: 'Le plan de publication pourra être complété plus tard.',
        monoRepoPreset: 'Mono-dépôt',
        monoRepoDetails: 'Un dépôt pour tous les composants.',
        repositoriesUnavailable: 'Les dépôts ne sont pas encore disponibles.',
        back: 'Retour',
        continueTools: 'Continuer : outils',
        continueEnvironments: 'Continuer : environnements',
        continueNaming: 'Continuer : nommage',
        continuePublishing: 'Continuer : publication',
        continueReview: 'Continuer : récapitulatif',
        namingHeading: 'Convention de nommage',
        publishingHeading: 'Où publier les fichiers',
        reviewHeading: 'Récapitulatif',
        optional: 'Étape 5 sur 6 · facultative',
        skip: 'Passer',
        create: 'Créer le projet',
        noExternalChanges:
          'Rien n’est créé hors d’IFS à cette étape : ni dépôt, ni ressource Azure, ni pipeline.',
      },
    });
  }
}

function renderWizard(draft: unknown = null, saveFails = false) {
  const service = {
    loadDraft: vi.fn().mockReturnValue(of(draft)),
    loadMetadata: vi.fn().mockReturnValue(
      of({
        tenantId: '11111111-1111-1111-1111-111111111111',
        catalogVersion: '2026.10-pilot',
        regions: [{ name: 'francecentral', code: 'fc' }],
      }),
    ),
    saveDraft: vi
      .fn()
      .mockReturnValue(
        saveFails
          ? throwError(() => new Error('Draft storage unavailable.'))
          : of({ content: {}, expiresAt: '2026-11-26T12:00:00Z' }),
      ),
    discardDraft: vi.fn().mockReturnValue(of(undefined)),
    previewNaming: vi.fn().mockReturnValue(of({ items: [], issues: [] })),
    createProject: vi
      .fn()
      .mockReturnValue(
        of({ id: '77777777-7777-4777-8777-777777777777', name: 'Mon projet', code: 'monprojet' }),
      ),
  };
  const organizations = {
    get: vi.fn().mockReturnValue(of({ id: organizationId, name: 'Contoso' })),
  };
  const router = { navigateByUrl: vi.fn(), navigate: vi.fn() };

  return render(ProjectWizard, {
    providers: [
      { provide: ProjectWizardService, useValue: service },
      { provide: OrganizationsService, useValue: organizations },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: { get: () => organizationId } } },
      },
      { provide: Router, useValue: router },
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
  }).then((result) => ({ ...result, service }));
}

describe('ProjectWizard', () => {
  it('offers only the Bicep and Azure DevOps tools for the pilot', async () => {
    const { service } = await renderWizard();
    fireEvent.input(await screen.findByLabelText('Nom'), { target: { value: 'Mon projet' } });
    fireEvent.input(await screen.findByLabelText('Code'), { target: { value: 'monprojet' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continuer : outils' }));

    expect(await screen.findByRole('heading', { name: 'Vos outils' })).toBeTruthy();
    expect(screen.getByText('Bicep')).toBeTruthy();
    expect(screen.getByText('Azure DevOps Pipelines')).toBeTruthy();
    expect(screen.queryByText('Terraform')).toBeNull();
    expect(screen.queryByText('GitHub Actions')).toBeNull();
    await waitFor(() => expect(service.saveDraft).toHaveBeenCalled());
  });

  it('resumes the saved step on another workstation and offers only the mono-repository preset', async () => {
    const { service } = await renderWizard({
      content: {
        step: 5,
        name: 'Mon projet',
        code: 'monprojet',
        description: '',
        codeManuallyEdited: true,
        environments: [],
        namingPreset: 'Compact',
        defaultNameTemplate: '{abbr}-{project}-{name}-{env}',
        resourceGroupNameTemplate: 'rg-{project}-{component}-{name}-{env}',
        publishingPreset: 'MonoRepo',
      },
      expiresAt: '2026-11-26T12:00:00Z',
    });

    expect(await screen.findByRole('heading', { name: 'Où publier les fichiers' })).toBeTruthy();
    expect(screen.getByText('Mono-dépôt')).toBeTruthy();
    expect(screen.queryByText('Infra et code séparés')).toBeNull();
    expect(screen.queryByText('Un dépôt par composant')).toBeNull();
    expect(service.loadDraft).toHaveBeenCalled();
  });

  it('keeps the current step when saving the next step fails', async () => {
    const { service } = await renderWizard(null, true);
    fireEvent.input(await screen.findByLabelText('Nom'), { target: { value: 'Mon projet' } });
    fireEvent.input(await screen.findByLabelText('Code'), { target: { value: 'monprojet' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continuer : outils' }));

    expect(await screen.findByRole('heading', { name: 'Identité du projet' })).toBeTruthy();
    expect((await screen.findByRole('alert')).textContent).toContain(
      'Le brouillon n’a pas pu être enregistré',
    );
    expect(service.saveDraft).toHaveBeenCalled();
  });
});
