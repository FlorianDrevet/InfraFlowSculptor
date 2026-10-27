import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, Observable, tap } from 'rxjs';
import {
  Api,
  acceptInvitation as acceptInvitationRequest,
  changeOrganizationMemberRoles,
  createOrganization,
  getOrganization,
  getInvitationPreview,
  inviteOrganizationMember,
  leaveOrganization,
  listOrganizationMembers,
  listMyOrganizations,
  listOrganizationInvitations,
  removeOrganizationMember,
  resendOrganizationInvitation,
  revokeOrganizationInvitation,
  renameOrganization,
  updateOrganizationAccessRestrictions,
} from '../api/generated';
import type {
  InvitationAcceptanceResponse,
  InvitationPreviewResponse,
  InvitationResponse,
  OrganizationMemberResponse,
  OrganizationRole,
  OrganizationSummaryResponse,
} from '../api/generated';
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

  listMembers(id: string): Observable<OrganizationMemberResponse[]> {
    return this.api.invoke(listOrganizationMembers, { id });
  }

  changeMemberRoles(
    id: string,
    memberId: string,
    roles: OrganizationRole[],
  ): Observable<OrganizationMemberResponse> {
    return this.api.invoke(changeOrganizationMemberRoles, { id, memberId, body: { roles } });
  }

  removeMember(id: string, memberId: string): Observable<void> {
    return this.api.invoke(removeOrganizationMember, { id, memberId });
  }

  listInvitations(id: string): Observable<InvitationResponse[]> {
    return this.api.invoke(listOrganizationInvitations, { id });
  }

  invite(id: string, email: string, roles: OrganizationRole[]): Observable<InvitationResponse> {
    return this.api.invoke(inviteOrganizationMember, { id, body: { email, roles } });
  }

  resendInvitation(id: string, invitationId: string): Observable<InvitationResponse> {
    return this.api.invoke(resendOrganizationInvitation, { id, invitationId });
  }

  revokeInvitation(id: string, invitationId: string): Observable<void> {
    return this.api.invoke(revokeOrganizationInvitation, { id, invitationId });
  }

  getInvitationPreview(token: string): Observable<InvitationPreviewResponse> {
    return this.api.invoke(getInvitationPreview, { token });
  }

  acceptInvitation(token: string): Observable<InvitationAcceptanceResponse> {
    return this.api.invoke(acceptInvitationRequest, { token });
  }

  leave(id: string): Observable<void> {
    return this.api.invoke(leaveOrganization, { id }).pipe(
      tap(() => {
        this.organizationState.update(organizations => organizations.filter(item => item.id !== id));
        if (this.activeOrganization.organizationId() === id) {
          this.activeOrganization.setOrganizationId(null);
        }
      }),
    );
  }

  updateAccessRestrictions(
    id: string,
    allowedEmailDomains: string[],
    allowedTenantIds: string[],
  ): Observable<OrganizationSummaryResponse> {
    return this.api.invoke(updateOrganizationAccessRestrictions, {
      id,
      body: { allowedEmailDomains, allowedTenantIds },
    }).pipe(
      tap(updated =>
        this.organizationState.update(organizations =>
          organizations.map(organization => organization.id === id ? updated : organization),
        ),
      ),
    );
  }
}
