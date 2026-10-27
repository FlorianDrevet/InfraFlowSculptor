import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { FeatureAvailabilityService } from './feature-availability.service';

export function featureGuard(featureKey: string): CanActivateFn {
  return async () => {
    const features = inject(FeatureAvailabilityService);
    const router = inject(Router);

    try {
      await features.ensureLoaded();
      return features.isEnabled(featureKey) || router.parseUrl('/not-found');
    } catch {
      return router.parseUrl('/error');
    }
  };
}
