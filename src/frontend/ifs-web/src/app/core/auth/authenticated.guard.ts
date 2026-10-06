import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';

export const authenticatedGuard: CanActivateFn = () => {
  if (inject(OidcSecurityService).authenticated().isAuthenticated) {
    return true;
  }

  return inject(Router).parseUrl('/login');
};
