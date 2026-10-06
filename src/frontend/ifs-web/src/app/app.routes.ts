import { isDevMode } from '@angular/core';
import { Routes } from '@angular/router';
import { autoLoginPartialRoutesGuard } from 'angular-auth-oidc-client';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
    canActivate: [autoLoginPartialRoutesGuard],
  },
  {
    path: 'profile',
    loadComponent: () => import('./features/profile/profile').then((m) => m.Profile),
    canActivate: [autoLoginPartialRoutesGuard],
  },
  {
    path: 'unauthorized',
    loadComponent: () => import('./features/unauthorized/unauthorized').then((m) => m.Unauthorized),
  },
  ...(isDevMode()
    ? [
        {
          path: 'dev/design-system',
          loadComponent: () => import('./features/dev/design-system/design-system').then((m) => m.DesignSystem),
        },
      ]
    : []),
  {
    path: '**',
    loadComponent: () => import('./features/not-found/not-found').then((m) => m.NotFound),
  },
];
