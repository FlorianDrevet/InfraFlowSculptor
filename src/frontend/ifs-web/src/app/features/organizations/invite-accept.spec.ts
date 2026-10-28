import { inputBinding } from '@angular/core';
import { provideTransloco, TranslocoLoader, Translation } from '@jsverse/transloco';
import { fireEvent, render, screen, waitFor } from '@testing-library/angular/zoneless';
import { Observable, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type {
  InvitationAcceptanceResponse,
  InvitationPreviewResponse,
} from '../../core/api/generated';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { ApiError } from '../../core/http/api-error';
import { InviteAccept } from './invite-accept';

const token = 'a'.repeat(43);
const accepted: InvitationAcceptanceResponse = {
  organizationId: '33333333-3333-4333-8333-333333333333',
  organizationName: 'Contoso',
  email: 'nina@contoso.example',
  roles: ['Member'],
};

const basePreview: InvitationPreviewResponse = {
  organizationId: accepted.organizationId,
  organizationName: accepted.organizationName,
  email: accepted.email,
  roles: ['Member'],
  status: 'Pending',
  createdAt: '2026-10-27T12:00:00Z',
  expiresAt: '2026-11-03T12:00:00Z',
  emailVerified: true,
  emailMatches: true,
  tenantAllowed: true,
};

class StaticTranslationLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({
      organization: {
        invitationAcceptTitle: 'Accepter une invitation',
        invitationJoining: 'Rejoindre {{ organizationName }}',
        invitationDescription: 'Invitation envoyée à {{ email }}.',
        invitationBenefits: 'Ce que vous recevrez',
        invitationOrganizationRole: 'Organisation',
        roleMember: 'Membre',
        invitationSignedInAs: 'Vous êtes connecté en tant que {{ email }}',
        invitationMatchingAddress: 'L’adresse vérifiée correspond à l’invitation.',
        invitationUnverifiedEmail: 'Adresse non vérifiée.',
        invitationEmailMismatch: 'Cette invitation a été envoyée à {{ email }}. Connectez-vous avec ce compte.',
        invitationExpiredDetails: 'Invitation expirée. Demandez à un administrateur de {{ organizationName }} de vous la renvoyer.',
        invitationRevoked: 'Cette invitation a été révoquée.',
        invitationTenantDenied: 'Cette organisation n’accepte que les comptes de ses tenants autorisés.',
        invitationAcceptAction: 'Rejoindre {{ organizationName }}',
        invitationAccepted: 'Vous avez rejoint {{ organizationName }}.',
        invitationAlreadyAccepted: 'Cette invitation a déjà été acceptée.',
        invitationLoading: 'Chargement de l’invitation…',
        invitationPreviewLoadError: 'Cette invitation ne peut pas être affichée.',
        invitationAcceptError: 'Impossible d’accepter cette invitation.',
      },
    });
  }
}

function renderPage(
  preview: InvitationPreviewResponse,
  acceptInvitation = vi.fn().mockReturnValue(of(accepted)),
) {
  const service = {
    getInvitationPreview: vi.fn().mockReturnValue(of(preview)),
    acceptInvitation,
  };

  return render(InviteAccept, {
    bindings: [inputBinding('token', () => token)],
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
  }).then(result => ({ ...result, service }));
}

describe('InviteAccept', () => {
  it('shows the invitation and accepts it for a matching verified account', async () => {
    const { service } = await renderPage(basePreview);

    expect(await screen.findByRole('heading', { name: 'Rejoindre Contoso' })).toBeDefined();
    expect(screen.getByText(/Invitation envoyée à nina@contoso\.example/)).toBeDefined();
    fireEvent.click(screen.getByRole('button', { name: 'Rejoindre Contoso' }));

    await waitFor(() => expect(service.acceptInvitation).toHaveBeenCalledWith(token));
    expect(await screen.findByText('Vous avez rejoint Contoso.')).toBeDefined();
  });

  it('shows a generic access message when an invitation acceptance is rejected by RG-ORG-06', async () => {
    const { service } = await renderPage(
      basePreview,
      vi.fn().mockReturnValue(throwError(() => new ApiError(403, 'RG-ORG-06', 'Access restrictions changed'))),
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Rejoindre Contoso' }));

    expect((await screen.findByRole('alert')).textContent).toContain('Impossible d’accepter cette invitation.');
    expect(screen.getByRole('alert').textContent).not.toContain('tenants autorisés');
    expect(service.acceptInvitation).toHaveBeenCalledWith(token);
  });

  it.each([
    {
      name: 'an unverified address',
      preview: { ...basePreview, emailVerified: false, emailMatches: false },
      message: 'Adresse non vérifiée.',
    },
    {
      name: 'a different verified address',
      preview: { ...basePreview, emailMatches: false },
      message: 'Cette invitation a été envoyée à nina@contoso.example. Connectez-vous avec ce compte.',
    },
    {
      name: 'an expired invitation',
      preview: { ...basePreview, status: 'Expired' as const },
      message: 'Invitation expirée. Demandez à un administrateur de Contoso de vous la renvoyer.',
    },
    {
      name: 'a revoked invitation',
      preview: { ...basePreview, status: 'Revoked' as const },
      message: 'Cette invitation a été révoquée.',
    },
  ])('explains why it cannot accept $name', async ({ preview, message }) => {
    const { service } = await renderPage(preview);

    expect((await screen.findByRole('alert')).textContent).toContain(message);
    expect(screen.queryByRole('button', { name: 'Rejoindre Contoso' })).toBeNull();
    expect(service.acceptInvitation).not.toHaveBeenCalled();
  });
});
