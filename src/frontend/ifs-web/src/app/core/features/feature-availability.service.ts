import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Api, getFeatures } from '../api/generated';
import { ActiveOrganizationStore } from '../http/active-organization.store';

@Injectable({ providedIn: 'root' })
export class FeatureAvailabilityService {
  private readonly api = inject(Api);
  private readonly activeOrganization = inject(ActiveOrganizationStore);
  private readonly enabledFeaturesState = signal<ReadonlySet<string>>(new Set());
  private loadedForOrganizationId: string | null | undefined;
  private loadingForOrganizationId: string | null | undefined;
  private loadPromise?: Promise<void>;

  ensureLoaded(): Promise<void> {
    const organizationId = this.activeOrganization.organizationId();
    if (this.loadedForOrganizationId === organizationId) {
      return Promise.resolve();
    }

    if (this.loadPromise && this.loadingForOrganizationId === organizationId) {
      return this.loadPromise;
    }

    this.enabledFeaturesState.set(new Set());
    this.loadingForOrganizationId = organizationId;
    this.loadPromise = firstValueFrom(this.api.invoke(getFeatures, {}))
      .then(features => {
        if (this.activeOrganization.organizationId() === organizationId) {
          this.loadedForOrganizationId = organizationId;
          this.enabledFeaturesState.set(
            new Set(features.filter(feature => feature.enabled).map(feature => feature.key)),
          );
        }
      })
      .catch((error: unknown) => {
        if (this.loadingForOrganizationId === organizationId) {
          this.loadPromise = undefined;
          this.loadingForOrganizationId = undefined;
        }
        throw error;
      })
      .finally(() => {
        if (this.loadingForOrganizationId === organizationId) {
          this.loadingForOrganizationId = undefined;
        }
      });

    return this.loadPromise;
  }

  isEnabled(featureKey: string): boolean {
    const enabledFeatures = this.enabledFeaturesState();
    return (
      this.loadedForOrganizationId === this.activeOrganization.organizationId() &&
      enabledFeatures.has(featureKey)
    );
  }
}
