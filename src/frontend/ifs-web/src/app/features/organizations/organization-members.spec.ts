import { inputBinding } from '@angular/core';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/angular/zoneless';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { InvitationResponse, OrganizationMemberResponse, OrganizationSummaryResponse } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { OrganizationMembers } from './organization-members';

const organizationId = '33333333-3333-4333-8333-333333333333';

const organization: OrganizationSummaryResponse = {
  id: organizationId,
  name: 'Contoso',
  roles: ['Administrator'],
  allowedEmailDomains: ['contoso.example'],
  allowedTenantIds: [],
  version: 1,
};

const member: OrganizationMemberResponse = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  displayName: 'Alice Martin',
  email: 'alice@contoso.example',
  roles: ['Administrator'],
  isOutsideAccessRestrictions: false,
};

const invitation: InvitationResponse = {
  id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  email: 'nina@contoso.example',
  roles: ['Member'],
  status: 'Pending',
  createdAt: '2026-10-27T12:00:00Z',
  expiresAt: new Date(Date.now() + 5 * 86_400_000).toISOString(),
};

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      organization: {
        membersTitle: 'Membres',
        loading: 'Chargement',
        membersLoadError: 'Erreur de chargement',
        searchMember: 'Rechercher un membre',
        searchMemberPlaceholder: 'Nom ou adresse',
        noMatchingMembers: 'Aucun membre',
        memberColumn: 'Membre',
        rolesColumn: 'Rôles d’organisation',
        actionsColumn: 'Actions',
        roleAdministrator: 'Administrateur',
        roleMember: 'Membre',
        modifyMember: 'Modifier',
        removeMember: 'Retirer',
        cancel: 'Annuler',
        confirmRemoval: 'Confirmation',
        membersSaveError: 'Erreur de sauvegarde',
        lastAdministratorError: 'Dernier administrateur',
        lastProjectOwnerError: 'RG-ORG-11 — Attribuez le rôle Propriétaire du projet à un autre membre avant de retirer cette personne.',
        saveAction: 'Enregistrer',
        outsideRestrictions: 'Hors restriction',
        invitationsTitle: 'Invitations en attente',
        invitePeopleTitle: 'Inviter des personnes',
        invitationEmail: 'Adresse e-mail',
        invitationRoles: 'Rôles d’organisation',
        roleConnectionManager: 'Gestionnaire des connexions',
        roleAuditor: 'Auditeur d’organisation',
        inviteAction: 'Inviter',
        invitationValidityDomains: 'Valables 7 jours · domaines autorisés : {{ domains }}',
        invitationValidityAll: 'Valables 7 jours',
        noPendingInvitations: 'Aucune invitation en attente.',
        invitationExpiry: 'Expire dans {{ days }} j',
        invitationExpired: 'Invitation expirée',
        resendInvitation: 'Renvoyer',
        revokeInvitation: 'Révoquer',
        invitationSendSuccess: 'Invitation créée. E-mail mis en file.',
        invitationAccessError: 'Domaine ou tenant non autorisé.',
        invitationPendingError: 'Une invitation est déjà en attente.',
        invitationActionError: 'Impossible de modifier cette invitation.',
        invitationLoadError: 'Impossible de charger les invitations.',
        invitationFormError: 'Saisissez une adresse et au moins un rôle.',
      },
    });
  }
}

describe('OrganizationMembers invitations', () => {
  it('explains when removing a member would leave a project without an owner', async () => {
    const service = {
      get: vi.fn().mockReturnValue(of(organization)),
      listMembers: vi.fn().mockReturnValue(of([member])),
      listInvitations: vi.fn().mockReturnValue(of([])),
      removeMember: vi.fn().mockReturnValue(throwError(() =>
        new ApiError(409, 'RG-ORG-11', 'Assign another project owner before removing this member.'),
      )),
    };

    await render(OrganizationMembers, {
      bindings: [inputBinding('id', () => organizationId)],
      providers: [
        { provide: OrganizationsService, useValue: service },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    });

    const row = await screen.findByRole('row', { name: /Alice Martin/ });
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true,
      value(this: HTMLDialogElement) {
        this.setAttribute('open', '');
      },
    });
    fireEvent.click(within(row).getByRole('button', { name: 'Retirer' }));
    const dialog = document.querySelector('dialog');
    expect(dialog).not.toBeNull();
    fireEvent.click(within(dialog!).getByRole('button', { name: 'Retirer' }));

    const alert = await within(dialog!).findByRole('alert');
    expect(alert.textContent).toContain(
      'RG-ORG-11 — Attribuez le rôle Propriétaire du projet à un autre membre avant de retirer cette personne.',
    );
  });

  it('shows pending invitations on the organization members screen', async () => {
    const service = {
      get: vi.fn().mockReturnValue(of(organization)),
      listMembers: vi.fn().mockReturnValue(of([member])),
      listInvitations: vi.fn().mockReturnValue(of([invitation])),
      invite: vi.fn().mockReturnValue(of({ ...invitation, id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc' })),
      resendInvitation: vi.fn().mockReturnValue(of(invitation)),
      revokeInvitation: vi.fn().mockReturnValue(of(undefined)),
    };

    await render(OrganizationMembers, {
      bindings: [inputBinding('id', () => organizationId)],
      providers: [
        { provide: OrganizationsService, useValue: service },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    });

    expect(await screen.findByRole('heading', { name: 'Invitations en attente' })).toBeDefined();
    expect(await screen.findByText('nina@contoso.example')).toBeDefined();
    expect(await screen.findByRole('button', { name: 'Renvoyer' })).toBeDefined();
    expect(await screen.findByRole('button', { name: 'Révoquer' })).toBeDefined();
  });

  it('creates, resends, and revokes invitations through organization actions', async () => {
    const service = {
      get: vi.fn().mockReturnValue(of(organization)),
      listMembers: vi.fn().mockReturnValue(of([member])),
      listInvitations: vi.fn().mockReturnValue(of([invitation])),
      invite: vi.fn().mockReturnValue(of({
        ...invitation,
        id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
        email: 'new@contoso.example',
        roles: ['Member', 'ConnectionManager'],
      })),
      resendInvitation: vi.fn().mockReturnValue(of(invitation)),
      revokeInvitation: vi.fn().mockReturnValue(of(undefined)),
    };

    await render(OrganizationMembers, {
      bindings: [inputBinding('id', () => organizationId)],
      providers: [
        { provide: OrganizationsService, useValue: service },
        provideTransloco({
          config: {
            availableLangs: ['fr'],
            defaultLang: 'fr',
            fallbackLang: 'fr',
            reRenderOnLangChange: false,
            prodMode: true,
          },
          loader: StaticTranslationLoader,
        }),
      ],
    });

    const address = await screen.findByLabelText<HTMLInputElement>('Adresse e-mail');
    fireEvent.input(address, { target: { value: 'new@contoso.example' } });
    fireEvent.click(screen.getByRole('checkbox', { name: 'Gestionnaire des connexions' }));
    fireEvent.click(screen.getByRole('button', { name: 'Inviter' }));
    await waitFor(() => expect(service.invite).toHaveBeenCalledWith(
      organizationId,
      'new@contoso.example',
      ['Member', 'ConnectionManager'],
    ));

    const invitationRow = screen.getByText('nina@contoso.example').closest('li');
    expect(invitationRow).not.toBeNull();
    fireEvent.click(within(invitationRow!).getByRole('button', { name: 'Renvoyer' }));
    await waitFor(() => expect(service.resendInvitation).toHaveBeenCalledWith(organizationId, invitation.id));
    fireEvent.click(within(invitationRow!).getByRole('button', { name: 'Révoquer' }));
    await waitFor(() => expect(service.revokeInvitation).toHaveBeenCalledWith(organizationId, invitation.id));
    await waitFor(() => expect(screen.queryByText('nina@contoso.example')).toBeNull());
  });
});
