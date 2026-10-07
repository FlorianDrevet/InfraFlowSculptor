import { Injectable, signal } from '@angular/core';

export type NavigationScope = 'workspace' | 'project' | 'organization' | 'backoffice';

export interface NavigationEntry {
  id: string;
  labelKey: string;
  path: string;
  scope: NavigationScope;
}

@Injectable({ providedIn: 'root' })
export class NavigationRegistry {
  private readonly entryState = signal<NavigationEntry[]>([]);
  readonly entries = this.entryState.asReadonly();

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
