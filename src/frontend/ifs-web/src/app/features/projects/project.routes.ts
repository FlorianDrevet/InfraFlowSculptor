import type { Routes } from '@angular/router';
import { featureGuard } from '../../core/features/feature.guard';
import { FEATURES } from '../../core/features/feature-keys';

export const PROJECT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./project-overview').then((module) => module.ProjectOverview),
    title: 'Vue d’ensemble du projet',
  },
  {
    path: 'environments',
    loadComponent: () =>
      import('./project-environments').then((module) => module.ProjectEnvironments),
    title: 'Environnements du projet',
  },
  {
    path: 'environments/:environmentId',
    loadComponent: () => import('./environment-edit').then((module) => module.EnvironmentEdit),
    title: 'Modifier un environnement',
  },
  {
    path: 'components',
    loadChildren: () => import('./components.routes').then((module) => module.COMPONENT_ROUTES),
  },
  {
    path: 'findings',
    loadComponent: () => import('./project-findings').then((module) => module.ProjectFindings),
    title: 'Constats de validation',
  },
  {
    path: 'settings',
    loadComponent: () => import('./project-settings').then((module) => module.ProjectSettings),
    title: 'Paramètres du projet',
  },
  {
    path: 'naming',
    loadComponent: () => import('./project-naming').then((module) => module.ProjectNaming),
    title: 'Nommage du projet',
  },
  {
    path: 'members',
    loadComponent: () => import('./project-members').then((module) => module.ProjectMembers),
    title: 'Membres du projet',
  },
  {
    path: 'revisions/:revisionId',
    canActivate: [featureGuard(FEATURES.revisions)],
    loadComponent: () =>
      import('../revisions/revision-detail').then((module) => module.RevisionDetail),
    title: 'Détail de la révision',
  },
  {
    path: 'revisions',
    canActivate: [featureGuard(FEATURES.revisions)],
    loadComponent: () =>
      import('../revisions/revisions-page').then((module) => module.RevisionsPage),
    title: 'Révisions',
  },
];
