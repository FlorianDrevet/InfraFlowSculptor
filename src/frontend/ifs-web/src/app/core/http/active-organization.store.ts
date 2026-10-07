import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ActiveOrganizationStore {
  private readonly organizationIdState = signal<string | null>(null);

  readonly organizationId = this.organizationIdState.asReadonly();

  setOrganizationId(organizationId: string | null): void {
    this.organizationIdState.set(organizationId?.trim() || null);
  }
}
