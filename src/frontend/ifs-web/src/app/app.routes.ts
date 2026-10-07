import { isDevMode } from '@angular/core';
import { Routes } from '@angular/router';
import { authenticatedGuard } from './core/auth/authenticated.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
    title: 'InfraFlowSculptor — Connexion',
  },
  {
    path: '',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
        title: 'Accueil',
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
        title: 'Profil',
      },
      {
        path: 'error',
        loadComponent: () => import('./features/error/error').then((m) => m.ErrorPage),
        title: 'Erreur',
      },
    ],
  },
  {
    path: 'unauthorized',
    loadComponent: () => import('./features/unauthorized/unauthorized').then((m) => m.Unauthorized),
  },
  ...(isDevMode()
    ? [
        {
          path: 'dev/design-system',
          loadComponent: () =>
            import('./features/dev/design-system/design-system').then((m) => m.DesignSystem),
        },
      ]
    : []),
  {
    path: '**',
    loadComponent: () => import('./features/not-found/not-found').then((m) => m.NotFound),
  },
];
