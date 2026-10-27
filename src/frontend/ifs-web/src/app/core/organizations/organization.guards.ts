import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ActiveOrganizationStore } from '../http/active-organization.store';
import { OrganizationsService } from './organizations.service';

export const organizationSelectionGuard: CanActivateFn = async () => {
  const organizations = inject(OrganizationsService);
  const router = inject(Router);

  try {
    const availableOrganizations = await organizations.ensureLoaded();
    return availableOrganizations.length > 0 || router.parseUrl('/organizations/new');
  } catch {
    return router.parseUrl('/error');
  }
};

export const organizationMembershipGuard: CanActivateFn = async route => {
  const organizations = inject(OrganizationsService);
  const activeOrganization = inject(ActiveOrganizationStore);
  const router = inject(Router);

  try {
    const availableOrganizations = await organizations.ensureLoaded();
    const organizationId = route.paramMap.get('id');
    if (!organizationId || !availableOrganizations.some(organization => organization.id === organizationId)) {
      return router.parseUrl('/not-found');
    }

    activeOrganization.setOrganizationId(organizationId);
    return true;
  } catch {
    return router.parseUrl('/error');
  }
};
