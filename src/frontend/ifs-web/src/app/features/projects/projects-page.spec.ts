import { ActivatedRoute, Router } from '@angular/router';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular/zoneless';
import { Observable, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectsPage } from './projects-page';

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      project: {
        projectsTitle: 'Projets',
        projectsDescription: 'Les projets de l’organisation active.',
        filterProjects: 'Filtrer les projets',
        filterByNameOrCode: 'Filtrer par nom ou code',
        projectFilters: 'Filtres des projets',
        allProjectsCount: 'Tous · {{count}}',
        favoritesCount: 'Favoris · {{count}}',
        addFavorite: 'Ajouter aux favoris',
        removeFavorite: 'Retirer des favoris',
        favoriteError: 'Le favori n’a pas pu être enregistré.',
        noMatchingProjects: 'Aucun projet ne correspond à ces filtres.',
        deletedProjectsTitle: 'Projets supprimés',
        deletedProjectsDescription: 'Projets récupérables pendant 30 jours.',
        newProject: 'Nouveau projet',
        deletedProjectsLink: 'Voir les projets supprimés',
        activeProjects: 'Projets actifs',
        loading: 'Chargement des projets…',
        loadError: 'Impossible de charger les projets.',
        retry: 'Réessayer',
        noProjects: 'Aucun projet pour le moment.',
        noDeletedProjects: 'Aucun projet supprimé.',
        createFirstProject: 'Créer un projet',
        environmentCount: 'Environnements',
        modelVersion: 'Version du modèle',
        publication: 'Publication',
        published: 'Publié',
        notPublished: 'Pas encore publié',
        deletedAt: 'Supprimé le',
        restoring: 'Restauration…',
        restore: 'Restaurer',
        restoreError: 'Le projet n’a pas pu être restauré.',
      },
    });
  }
}

describe('ProjectsPage', () => {
  it('shows server-provided project details in the organization list', async () => {
    const service = {
      list: vi.fn().mockReturnValue(
        of([
          {
            id: '77777777-7777-4777-8777-777777777777',
            version: 3,
            name: 'shop',
            code: 'shop',
            description: 'Boutique en ligne',
            environmentCount: 3,
            modelVersion: 2,
            firstPublishedAt: null,
            deletedAt: null,
          },
        ]),
      ),
    };

    await render(ProjectsPage, {
      providers: [
        { provide: ProjectsService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { data: { includeDeleted: false } },
            parent: {
              snapshot: { paramMap: { get: () => '33333333-3333-4333-8333-333333333333' } },
            },
          },
        },
        { provide: Router, useValue: { navigate: vi.fn() } },
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

    expect(await screen.findByRole('heading', { name: 'Projets' })).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'shop' })).toBeTruthy();
    expect(screen.getByText('Boutique en ligne')).toBeTruthy();
    expect(screen.getByText('3', { selector: 'dd' })).toBeTruthy();
    expect(service.list).toHaveBeenCalledWith(false);
  });

  it('shows only deleted projects on the restoration route', async () => {
    const service = {
      list: vi.fn().mockReturnValue(
        of([
          {
            id: '77777777-7777-4777-8777-777777777777',
            version: 3,
            name: 'Active project',
            code: 'active',
            description: '',
            environmentCount: 1,
            modelVersion: 0,
            firstPublishedAt: null,
            deletedAt: null,
          },
          {
            id: '88888888-8888-4888-8888-888888888888',
            version: 4,
            name: 'Deleted project',
            code: 'deleted',
            description: '',
            environmentCount: 1,
            modelVersion: 0,
            firstPublishedAt: null,
            deletedAt: '2026-10-27T12:00:00Z',
          },
        ]),
      ),
    };

    await render(ProjectsPage, {
      providers: [
        { provide: ProjectsService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { data: { includeDeleted: true } },
            parent: {
              snapshot: { paramMap: { get: () => '33333333-3333-4333-8333-333333333333' } },
            },
          },
        },
        { provide: Router, useValue: { navigate: vi.fn() } },
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

    expect(await screen.findByRole('heading', { name: 'Projets supprimés' })).toBeTruthy();
    expect(await screen.findByText('Deleted project')).toBeTruthy();
    expect(screen.queryByText('Active project')).toBeNull();
    expect(service.list).toHaveBeenCalledWith(true);
  });

  it('filters favorite projects and saves a favorite through the server', async () => {
    const shopProject = {
      id: '77777777-7777-4777-8777-777777777777',
      version: 3,
      name: 'shop',
      code: 'shop',
      description: 'Boutique en ligne',
      environmentCount: 2,
      modelVersion: 1,
      firstPublishedAt: null,
      deletedAt: null,
      isFavorite: false,
    };
    const platformProject = {
      ...shopProject,
      id: '88888888-8888-4888-8888-888888888888',
      name: 'platform',
      code: 'platform',
      isFavorite: true,
    };
    const service = {
      list: vi.fn().mockReturnValueOnce(of([shopProject, platformProject])).mockReturnValueOnce(
        of([{ ...shopProject, isFavorite: true }, platformProject]),
      ),
      setFavorite: vi.fn().mockReturnValue(of(undefined)),
      removeFavorite: vi.fn().mockReturnValue(of(undefined)),
    };

    await render(ProjectsPage, {
      providers: [
        { provide: ProjectsService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { data: { includeDeleted: false } },
            parent: {
              snapshot: { paramMap: { get: () => '33333333-3333-4333-8333-333333333333' } },
            },
          },
        },
        { provide: Router, useValue: { navigate: vi.fn() } },
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

    fireEvent.click(await screen.findByRole('button', { name: 'Favoris · 1' }));
    expect(screen.getByRole('button', { name: 'platform' })).toBeTruthy();
    await waitFor(() => expect(screen.queryByRole('button', { name: 'shop' })).toBeNull());

    fireEvent.click(screen.getByRole('button', { name: 'Tous · 2' }));
    expect(await screen.findByRole('button', { name: 'shop' })).toBeTruthy();
    fireEvent.input(screen.getByRole('searchbox', { name: 'Filtrer les projets' }), {
      target: { value: 'sho' },
    });
    fireEvent.click(await screen.findByRole('button', { name: 'Ajouter aux favoris' }));

    expect(service.setFavorite).toHaveBeenCalledWith(shopProject.id);
    expect(await screen.findByRole('button', { name: 'Retirer des favoris' })).toBeTruthy();
  });
});
