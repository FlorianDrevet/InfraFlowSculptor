import { describe, expect, it } from 'vitest';
import { authenticatedGuard } from './core/auth/authenticated.guard';
import { routes } from './app.routes';

describe('routes', () => {
  it('provides an authenticated route for accepting an invitation', () => {
    const route = routes.find(candidate => candidate.path === 'invitations/:token');

    expect(route?.canActivate).toContain(authenticatedGuard);
  });
});
