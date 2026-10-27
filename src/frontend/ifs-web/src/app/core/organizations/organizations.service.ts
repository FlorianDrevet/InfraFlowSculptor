import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, Observable, tap } from 'rxjs';
import {
  Api,
  createOrganization,
  getOrganization,
  listMyOrganizations,
  renameOrganization,
} from '../api/generated';
import type { OrganizationSummaryResponse } from '../api/generated';
import { ActiveOrganizationStore } from '../http/active-organization.store';

@Injectable({ providedIn: 'root' })
export class OrganizationsService {
  private readonly api = inject(Api);
  private readonly activeOrganization = inject(ActiveOrganizationStore);
  private readonly organizationState = signal<OrganizationSummaryResponse[]>([]);
  private readonly loadingState = signal(false);
  private loaded = false;
  private loadPromise?: Promise<OrganizationSummaryResponse[]>;

  readonly organizations = this.organizationState.asReadonly();
  readonly loading = this.loadingState.asReadonly();

  ensureLoaded(): Promise<OrganizationSummaryResponse[]> {
    if (this.loaded) {
      return Promise.resolve(this.organizationState());
    }

    if (this.loadPromise) {
      return this.loadPromise;
    }

    this.loadingState.set(true);
    this.loadPromise = firstValueFrom(this.api.invoke(listMyOrganizations, {}))
      .then((organizations) => {
        this.organizationState.set(organizations);
        this.loaded = true;
        const selectedId = this.activeOrganization.organizationId();
        if (!selectedId || !organizations.some(organization => organization.id === selectedId)) {
          this.activeOrganization.setOrganizationId(organizations[0]?.id ?? null);
        }
        return organizations;
      })
      .catch((error: unknown) => {
        this.loadPromise = undefined;
        throw error;
      })
      .finally(() => this.loadingState.set(false));

    return this.loadPromise;
  }

  create(name: string): Observable<OrganizationSummaryResponse> {
    return this.api.invoke(createOrganization, { body: { name } }).pipe(
      tap((organization) => {
        this.organizationState.update(organizations => [...organizations, organization]);
        this.loaded = true;
        this.activeOrganization.setOrganizationId(organization.id);
      }),
    );
  }

  get(id: string): Observable<OrganizationSummaryResponse> {
    return this.api.invoke(getOrganization, { id });
  }

  rename(id: string, name: string): Observable<OrganizationSummaryResponse> {
    return this.api.invoke(renameOrganization, { id, body: { name } }).pipe(
      tap((updated) =>
        this.organizationState.update(organizations =>
          organizations.map(organization => organization.id === updated.id ? updated : organization),
        ),
      ),
    );
  }
}
