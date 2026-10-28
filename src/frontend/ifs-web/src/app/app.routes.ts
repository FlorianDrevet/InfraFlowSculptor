import { isDevMode } from '@angular/core';
import { Routes } from '@angular/router';
import { authenticatedGuard } from './core/auth/authenticated.guard';
import { featureGuard } from './core/features/feature.guard';
import { FEATURES } from './core/features/feature-keys';
import {
  organizationMembershipGuard,
  organizationSelectionGuard,
} from './core/organizations/organization.guards';

export const routes: Routes = [
  {
    path: 'invitations/:token',
    canActivate: [authenticatedGuard],
    loadComponent: () =>
      import('./features/organizations/invite-accept').then((m) => m.InviteAccept),
    title: 'Accepter une invitation',
  },
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
        canActivate: [organizationSelectionGuard],
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
        title: 'Accueil',
      },
      {
        path: 'organizations/new',
        canActivate: [featureGuard(FEATURES.organizations)],
        loadComponent: () =>
          import('./features/organizations/organization-create').then((m) => m.OrganizationCreate),
        title: 'Créer une organisation',
      },
      {
        path: 'organizations/:id',
        canActivate: [organizationMembershipGuard],
        children: [
          {
            path: 'settings',
            canActivate: [featureGuard(FEATURES.organizations)],
            loadComponent: () =>
              import('./features/organizations/organization-settings').then(
                (m) => m.OrganizationSettings,
              ),
            title: 'Organisation — paramètres',
          },
          {
            path: 'members',
            canActivate: [featureGuard(FEATURES.organizations)],
            loadComponent: () =>
              import('./features/organizations/organization-members').then(
                (m) => m.OrganizationMembers,
              ),
            title: 'Membres de l’organisation',
          },
          {
            path: 'audit',
            canActivate: [featureGuard(FEATURES.organizations)],
            loadComponent: () =>
              import('./features/organizations/organization-audit').then(
                (m) => m.OrganizationAudit,
              ),
            title: 'Journal d’audit',
          },
        ],
      },
      {
        path: 'o/:orgId',
        canActivate: [organizationMembershipGuard],
        children: [
          {
            path: 'projects',
            canActivate: [
              featureGuard(FEATURES.organizations),
              featureGuard(FEATURES.projectSetup),
            ],
            loadComponent: () =>
              import('./features/projects/projects-page').then((m) => m.ProjectsPage),
            title: 'Projets',
          },
          {
            path: 'projects/deleted',
            canActivate: [
              featureGuard(FEATURES.organizations),
              featureGuard(FEATURES.projectSetup),
            ],
            data: { includeDeleted: true },
            loadComponent: () =>
              import('./features/projects/projects-page').then((m) => m.ProjectsPage),
            title: 'Projets supprimés',
          },
          {
            path: 'projects/new',
            canActivate: [
              featureGuard(FEATURES.organizations),
              featureGuard(FEATURES.projectSetup),
            ],
            loadComponent: () =>
              import('./features/projects/project-wizard').then((m) => m.ProjectWizard),
            title: 'Assistant de création de projet',
          },
          {
            path: 'projects/:projectId',
            canActivate: [
              featureGuard(FEATURES.organizations),
              featureGuard(FEATURES.projectSetup),
            ],
            loadComponent: () =>
              import('./features/projects/project-workspace').then((m) => m.ProjectWorkspace),
            children: [
              {
                path: '',
                loadComponent: () =>
                  import('./features/projects/project-overview').then((m) => m.ProjectOverview),
                title: 'Vue d’ensemble du projet',
              },
              {
                path: 'environments',
                loadComponent: () =>
                  import('./features/projects/project-environments').then(
                    (m) => m.ProjectEnvironments,
                  ),
                title: 'Environnements du projet',
              },
              {
                path: 'environments/:environmentId',
                loadComponent: () =>
                  import('./features/projects/environment-edit').then((m) => m.EnvironmentEdit),
                title: 'Modifier un environnement',
              },
              {
                path: 'settings',
                loadComponent: () =>
                  import('./features/projects/project-settings').then((m) => m.ProjectSettings),
                title: 'Paramètres du projet',
              },
              {
                path: 'members',
                loadComponent: () =>
                  import('./features/projects/project-members').then((m) => m.ProjectMembers),
                title: 'Membres du projet',
              },
            ],
          },
        ],
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
