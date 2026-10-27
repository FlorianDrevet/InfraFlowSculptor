import { inject } from '@angular/core';
import { CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { PendingNavigationStore } from './pending-navigation.store';

export const authenticatedGuard: CanActivateFn = (_route, state: RouterStateSnapshot) => {
  const router = inject(Router);
  const pendingNavigation = inject(PendingNavigationStore);

  if (inject(OidcSecurityService).authenticated().isAuthenticated) {
    if (state.url === '/' || state.url === '') {
      const returnUrl = pendingNavigation.consume();
      if (returnUrl) {
        return router.parseUrl(returnUrl);
      }
    }

    return true;
  }

  pendingNavigation.save(state.url);
  return router.parseUrl('/login');
};
