import type { Routes } from '@angular/router';

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
];
