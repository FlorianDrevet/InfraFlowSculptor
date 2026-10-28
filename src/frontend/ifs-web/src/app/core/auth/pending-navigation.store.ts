import { Injectable } from '@angular/core';

const PENDING_NAVIGATION_KEY = 'ifs.pending-navigation';

@Injectable({ providedIn: 'root' })
export class PendingNavigationStore {
  save(path: string): void {
    if (!this.isLocalPath(path)) {
      return;
    }

    try {
      globalThis.sessionStorage.setItem(PENDING_NAVIGATION_KEY, path);
    } catch {
      // The login flow can still continue to the home page when storage is unavailable.
    }
  }

  consume(): string | null {
    try {
      const path = globalThis.sessionStorage.getItem(PENDING_NAVIGATION_KEY);
      globalThis.sessionStorage.removeItem(PENDING_NAVIGATION_KEY);
      return path && this.isLocalPath(path) ? path : null;
    } catch {
      return null;
    }
  }

  private isLocalPath(path: string): boolean {
    return path.startsWith('/') && !path.startsWith('//') && !path.includes('\\');
  }
}
