import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen } from '@testing-library/angular';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { ProjectPublishPlanResponse, SetProjectPublishPlanRequest } from '../../core/api/generated';
import { ProjectPublishPlanService } from '../../core/projects/project-publish-plan.service';
import { ProjectPublishing } from './project-publishing';

const projectId = '77777777-7777-4777-8777-777777777777';
const giteaConnectionId = '11111111-1111-4111-8111-111111111111';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        publicationTitle: 'Plan de publication',
        publicationDescription: 'Où chaque partie de chaque composant est écrite.',
        publicationSave: 'Enregistrer',
        publicationSaveError: 'Échec de l’enregistrement.',
        publicationSaved: 'Le plan de publication est enregistré.',
        publicationIssuesTitle: 'Le plan contient des points à corriger',
        publicationPresetApplied: 'Préréglage appliqué',
        publicationMonoRepo: 'Mono-dépôt',
        publicationNewComponentsRule: 'Règle pour les nouveaux composants dans {{repository}}.',
        publicationDefaultRepository: 'Dépôt par défaut',
        publicationRepository: 'Dépôt',
        publicationChooseRepository: 'Choisir un dépôt',
        publicationNoRepositories: 'Aucun dépôt n’est accessible à ce projet.',
        publicationBasePath: 'Chemin de base',
        publicationComponent: 'Composant',
        publicationPart: 'Partie',
        publicationEffectiveFolder: 'Dossier effectif',
        publicationInfrastructure: 'Infrastructure',
        publicationApplications: 'Applications',
        publicationSourceCode: 'Code source',
        publicationNoComponents: 'Aucun composant dans ce projet.',
        loading: 'Chargement…',
        loadError: 'Erreur de chargement',
        retry: 'Réessayer',
      },
    });
  }
}

const plan: ProjectPublishPlanResponse = {
  projectId,
  projectVersion: 12,
  preset: 'MonoRepo',
  defaultInfrastructureDestinationId: null,
  defaultApplicationsDestinationId: null,
  defaultSourceCodeDestinationId: null,
  destinations: [],
  assignments: [],
  components: [],
  rows: [
    {
      componentId: '22222222-2222-4222-8222-222222222222',
      componentName: 'Core',
      componentCode: 'core',
      part: 'Infrastructure',
      destinationId: null,
      destinationName: '',
      basePath: '',
      effectiveFolder: '',
    },
  ],
  availableDestinations: [
    {
      gitConnectionId: giteaConnectionId,
      connectionName: 'Gitea',
      provider: 'Gitea',
      providerRepositoryId: 'contoso/shop',
      repositoryName: 'shop',
      defaultBranch: 'main',
    },
  ],
  issues: [],
};

describe('ProjectPublishing', () => {
  it('saves the selected mono-repository with an empty base path', async () => {
    const save = vi.fn<(id: string, body: SetProjectPublishPlanRequest) => Observable<void>>()
      .mockReturnValue(of(undefined));
    await render(ProjectPublishing, {
      inputs: { projectId },
      providers: [
        {
          provide: ProjectPublishPlanService,
          useValue: { get: vi.fn().mockReturnValue(of(plan)), save },
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

    expect(await screen.findByRole('heading', { name: 'Plan de publication' })).toBeTruthy();
    const repositorySelect = screen.getByLabelText<HTMLSelectElement>('Dépôt par défaut');
    expect(repositorySelect.selectedOptions[0]?.textContent?.trim()).toBe('Gitea/shop');
    fireEvent.click(screen.getByRole('button', { name: 'Enregistrer' }));

    await vi.waitFor(() => expect(save).toHaveBeenCalledOnce());
    const call = save.mock.calls[0];
    expect(call).toBeDefined();
    if (!call) return;
    const request = call[1];
    expect(request.preset).toBe('MonoRepo');
    expect(request.destinations).toHaveLength(1);
    expect(request.destinations[0]).toMatchObject({
      gitConnectionId: giteaConnectionId,
      providerRepositoryId: 'contoso/shop',
      basePath: '',
    });
    expect(request.defaultInfrastructureDestinationId).toBe(request.destinations[0].id);
    expect(request.defaultApplicationsDestinationId).toBe(request.destinations[0].id);
    expect(request.defaultSourceCodeDestinationId).toBe(request.destinations[0].id);
  });

  it('shows a repository only once when it is both saved and accessible', async () => {
    const destinationId = '33333333-3333-4333-8333-333333333333';
    const configuredPlan: ProjectPublishPlanResponse = {
      ...plan,
      defaultInfrastructureDestinationId: destinationId,
      destinations: [
        {
          id: destinationId,
          gitConnectionId: giteaConnectionId,
          providerRepositoryId: 'contoso/shop',
          connectionName: 'Gitea',
          repositoryName: 'shop',
          basePath: '',
          provider: 'Gitea',
          defaultBranch: 'main',
          isAccessible: true,
        },
      ],
      rows: plan.rows.map(row => ({ ...row, destinationId, destinationName: 'shop' })),
    };

    await render(ProjectPublishing, {
      inputs: { projectId },
      providers: [
        {
          provide: ProjectPublishPlanService,
          useValue: { get: vi.fn().mockReturnValue(of(configuredPlan)), save: vi.fn() },
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

    expect(await screen.findByRole('heading', { name: 'Plan de publication' })).toBeTruthy();
    const repositorySelect = screen.getByLabelText<HTMLSelectElement>('Dépôt par défaut');
    const shopOptions = [...repositorySelect.options].filter(option => option.textContent === 'Gitea/shop');
    expect(shopOptions).toHaveLength(1);
    expect(repositorySelect.selectedOptions[0]?.textContent).toBe('Gitea/shop');
  });
});
