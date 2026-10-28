import type { Routes } from '@angular/router';

export const COMPONENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./project-components').then((module) => module.ProjectComponents),
    title: 'Composants du projet',
  },
  {
    path: ':componentId/settings',
    loadComponent: () => import('./component-editor').then((module) => module.ComponentEditor),
    title: (route) =>
      route.params['componentId'] === 'new' ? 'Nouveau composant' : 'Réglages du composant',
  },
  {
    path: ':componentId',
    loadComponent: () => import('./component-detail').then((module) => module.ComponentDetail),
    title: 'Composant',
  },
];
