import { describe, expect, it } from 'vitest';
import { authenticatedGuard } from './core/auth/authenticated.guard';
import { routes } from './app.routes';

describe('routes', () => {
  it('provides an authenticated route for accepting an invitation', () => {
    const route = routes.find(candidate => candidate.path === 'invitations/:token');

    expect(route?.canActivate).toContain(authenticatedGuard);
  });

  it('provides an organization route for reading its audit history', () => {
    const organizationRoute = routes
      .find(candidate => candidate.path === '')
      ?.children?.find(candidate => candidate.path === 'organizations/:id');
    const auditRoute = organizationRoute?.children?.find(candidate => candidate.path === 'audit');

    expect(auditRoute?.loadComponent).toBeDefined();
  });
});
