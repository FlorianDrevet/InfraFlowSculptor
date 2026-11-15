import { inputBinding } from '@angular/core';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular/zoneless';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { GitConnectionResponse, ProjectSummaryResponse } from '../../core/api/generated';
import { GitConnectionsService } from '../../core/git-connections/git-connections.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { OrganizationConnections } from './organization-connections';

const organizationId = '33333333-3333-4333-8333-333333333333';
const projectId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

const project: ProjectSummaryResponse = {
  id: projectId,
  code: 'shop',
  name: 'Shop',
  catalogVersion: '1',
  ciPlatform: 'AzureDevOps',
  componentCount: 0,
  coordinatorUserProfileId: null,
  defaultReviewers: [],
  defaultRunners: [],
  deletedAt: null,
  description: '',
  environmentCount: 0,
  firstPublishedAt: null,
  infrastructureLanguage: 'Bicep',
  isFavorite: false,
  modelVersion: 1,
  moduleSource: 'PublicRegistry',
  systemTagsEnabled: false,
  tags: {},
  tenantId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  version: 1,
};

const connection: GitConnectionResponse = {
  id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  providerKind: 'Gitea',
  name: 'Gitea (développement)',
  organization: 'contoso',
  tenantId: null,
  authenticationMode: 'DevelopmentToken',
  personalAccessTokenExpiresAt: null,
  version: 2,
  usedByProjectNames: [],
  repositories: [
    {
      providerRepositoryId: 'contoso/shop',
      name: 'shop',
      defaultBranch: 'main',
      openToAllProjects: false,
      allowedProjectIds: [],
    },
  ],
};

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      organization: { cancel: 'Annuler' },
      gitConnections: {
        title: 'Connexions',
        intro: 'Les dépôts Git utilisent des connexions d’organisation.',
        add: 'Ajouter une connexion',
        loading: 'Chargement',
        loadError: 'Erreur de chargement',
        providersHeading: 'Fournisseurs git',
        provider: 'Fournisseur',
        connectionName: 'Nom de la connexion',
        azureOrganization: 'Organisation Azure DevOps',
        giteaOrganization: 'Organisation Gitea',
        authenticationMode: 'Authentification',
        federated: 'Principal de service IFS',
        federatedHint: 'Ajoutez le principal à Azure DevOps.',
        tenantId: 'Identifiant du tenant',
        personalAccessToken: 'Jeton d’accès personnel',
        giteaTokenHint: 'Jeton de développement Gitea configuré localement',
        addTitle: 'Ajouter une connexion',
        formError: 'Informations manquantes',
        save: 'Enregistrer et tester',
        saving: 'Enregistrement',
        emptyTitle: 'Aucune connexion Git',
        emptyDescription: 'Ajoutez une connexion pour partager des dépôts.',
        operational: 'Opérationnelle',
        test: 'Tester',
        edit: 'Modifier',
        usedByProjects: 'Utilisée par {{ count }} projets',
        repositoriesHeading: 'Dépôts ouverts aux projets',
        allProjects: 'Ouvrir à tous les projets',
        noProjects: 'Aucun projet n’a accès à ce dépôt.',
        selectedProjects: '{{ count }} projets autorisés',
        saveAccess: 'Enregistrer les accès',
        removeDialogTitle: 'Retirer cette connexion ?',
        removeDialogDescription: 'La connexion {{ name }} sera retirée.',
        remove: 'Retirer',
      },
    });
  }
}

function renderPage(list: GitConnectionResponse[]) {
  return render(OrganizationConnections, {
    bindings: [inputBinding('id', () => organizationId)],
    providers: [
      {
        provide: GitConnectionsService,
        useValue: {
          providers: vi.fn().mockReturnValue(
            of([
              { kind: 'AzureRepos', displayName: 'Azure DevOps' },
              { kind: 'Gitea', displayName: 'Gitea (développement)' },
            ]),
          ),
          list: vi.fn().mockReturnValue(of(list)),
        },
      },
      { provide: ProjectsService, useValue: { list: vi.fn().mockReturnValue(of([project])) } },
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

describe('OrganizationConnections', () => {
  it('shows only connected provider data and the empty state', async () => {
    await renderPage([]);

    expect(await screen.findByRole('heading', { name: 'Connexions' })).toBeDefined();
    expect(await screen.findByRole('heading', { name: 'Aucune connexion Git' })).toBeDefined();
    expect(screen.getByRole('heading', { name: 'Fournisseurs git' })).toBeDefined();
  });

  it('shows repositories closed by default and opens the connection form', async () => {
    await renderPage([connection]);

    expect(
      await screen.findByRole('heading', { name: 'Gitea (développement) · Gitea (développement)' }),
    ).toBeDefined();
    expect(screen.getByText('Aucun projet n’a accès à ce dépôt.')).toBeDefined();
    expect(screen.getByRole('checkbox', { name: 'Ouvrir à tous les projets' })).toBeDefined();

    fireEvent.click(screen.getByRole('button', { name: 'Ajouter une connexion' }));
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Fournisseur' })).toBeDefined(),
    );
    fireEvent.change(screen.getByRole('combobox', { name: 'Fournisseur' }), {
      target: { value: 'Gitea' },
    });

    await waitFor(() => expect(screen.getByLabelText('Organisation Gitea')).toBeDefined());
    expect(screen.queryByLabelText('Jeton d’accès personnel')).toBeNull();
  });
});
