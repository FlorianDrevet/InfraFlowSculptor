import { describe, expect, it } from 'vitest';
import { authenticatedGuard } from './core/auth/authenticated.guard';
import { organizationMembershipGuard } from './core/organizations/organization.guards';
import { routes } from './app.routes';

describe('routes', () => {
  it('provides an authenticated route for accepting an invitation', () => {
    const route = routes.find((candidate) => candidate.path === 'invitations/:token');

    expect(route?.canActivate).toContain(authenticatedGuard);
  });

  it('provides an organization route for reading its audit history', () => {
    const organizationRoute = routes
      .find((candidate) => candidate.path === '')
      ?.children?.find((candidate) => candidate.path === 'organizations/:id');
    const auditRoute = organizationRoute?.children?.find((candidate) => candidate.path === 'audit');

    expect(auditRoute?.loadComponent).toBeDefined();
  });

  it('provides the organization-scoped project wizard route', () => {
    const organizationRoute = routes
      .find((candidate) => candidate.path === '')
      ?.children?.find((candidate) => candidate.path === 'o/:orgId');
    const wizardRoute = organizationRoute?.children?.find(
      (candidate) => candidate.path === 'projects/new',
    );

    expect(wizardRoute?.loadComponent).toBeDefined();
  });

  it('exposes the organization project list and the project workspace sections', () => {
    const shellRoute = routes.find((route) => route.path === '');
    const organizationRoute = shellRoute?.children?.find((route) => route.path === 'o/:orgId');
    const projectListRoute = organizationRoute?.children?.find(
      (route) => route.path === 'projects',
    );
    const deletedProjectsRoute = organizationRoute?.children?.find(
      (route) => route.path === 'projects/deleted',
    );
    const projectWorkspaceRoute = organizationRoute?.children?.find(
      (route) => route.path === 'projects/:projectId',
    );
    const projectPaths = organizationRoute?.children?.map((route) => route.path) ?? [];

    expect(organizationRoute?.canActivate).toContain(organizationMembershipGuard);
    expect(projectPaths.indexOf('projects/deleted')).toBeLessThan(
      projectPaths.indexOf('projects/:projectId'),
    );
    expect(projectPaths.indexOf('projects/new')).toBeLessThan(
      projectPaths.indexOf('projects/:projectId'),
    );
    expect(projectListRoute?.loadComponent).toBeDefined();
    expect(deletedProjectsRoute?.data).toEqual({ includeDeleted: true });
    expect(projectWorkspaceRoute?.children?.map((route) => route.path)).toEqual([
      '',
      'environments',
      'environments/:environmentId',
      'settings',
      'members',
    ]);
  });
});
