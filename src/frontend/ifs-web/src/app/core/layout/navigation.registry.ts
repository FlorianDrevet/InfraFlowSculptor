import { computed, inject, Injectable, signal } from '@angular/core';
import type { IconName } from '../../ds/icon/icon';
import { FeatureAvailabilityService } from '../features/feature-availability.service';

export type NavigationScope = 'workspace' | 'project' | 'organization' | 'backoffice';

export interface NavigationEntry {
  id: string;
  labelKey: string;
  path: string;
  scope: NavigationScope;
  featureKey?: string;
  iconName?: IconName;
}

@Injectable({ providedIn: 'root' })
export class NavigationRegistry {
  private readonly features = inject(FeatureAvailabilityService);
  private readonly entryState = signal<NavigationEntry[]>([]);
  readonly entries = computed(() =>
    this.entryState().filter(entry => !entry.featureKey || this.features.isEnabled(entry.featureKey)),
  );

  register(entry: NavigationEntry): () => void {
    this.entryState.update((current) => [
      ...current.filter((registered) => registered.id !== entry.id),
      entry,
    ]);

    return () =>
      this.entryState.update((current) =>
        current.filter((registered) => registered.id !== entry.id),
      );
  }
}
