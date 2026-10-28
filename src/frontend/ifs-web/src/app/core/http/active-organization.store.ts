import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';

const ORGANIZATION_STORAGE_KEY = 'ifs.activeOrganizationId';

function readStoredOrganizationId(isBrowser: boolean): string | null {
  if (!isBrowser) {
    return null;
  }

  try {
    return globalThis.localStorage.getItem(ORGANIZATION_STORAGE_KEY);
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class ActiveOrganizationStore {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly organizationIdState = signal<string | null>(
    readStoredOrganizationId(this.isBrowser),
  );

  readonly organizationId = this.organizationIdState.asReadonly();

  setOrganizationId(organizationId: string | null): void {
    const normalizedId = organizationId?.trim() || null;
    this.organizationIdState.set(normalizedId);

    if (!this.isBrowser) {
      return;
    }

    try {
      if (normalizedId === null) {
        globalThis.localStorage.removeItem(ORGANIZATION_STORAGE_KEY);
      } else {
        globalThis.localStorage.setItem(ORGANIZATION_STORAGE_KEY, normalizedId);
      }
    } catch {
      // Organization selection remains available for the current tab.
    }
  }
}
