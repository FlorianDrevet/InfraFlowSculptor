import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { InvitationPreviewResponse, OrganizationRole } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { Brand } from '../../core/layout/brand';
import { DsButton } from '../../ds/button/button';

@Component({
  selector: 'app-invite-accept',
  standalone: true,
  imports: [Brand, DsButton, TranslocoPipe],
  templateUrl: './invite-accept.html',
  styleUrl: './invite-accept.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InviteAccept implements OnInit {
  readonly token = input.required<string>();
  private readonly organizations = inject(OrganizationsService);
  private readonly transloco = inject(TranslocoService);

  protected readonly preview = signal<InvitationPreviewResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly accepting = signal(false);
  protected readonly accepted = signal(false);
  protected readonly acceptError = signal<InvitationError | null>(null);
  protected readonly canAccept = computed(() => {
    const invitation = this.preview();
    return invitation?.status === 'Pending'
      && invitation.emailVerified
      && invitation.emailMatches
      && invitation.tenantAllowed;
  });
  protected readonly statusMessage = computed((): InvitationStatusMessage | null => {
    const invitation = this.preview();
    if (!invitation) {
      return null;
    }

    if (invitation.status === 'Expired') {
      return 'expired';
    }

    if (invitation.status === 'Revoked') {
      return 'revoked';
    }

    if (invitation.status === 'Accepted') {
      return 'alreadyAccepted';
    }

    if (!invitation.emailVerified) {
      return 'unverified';
    }

    if (!invitation.emailMatches) {
      return 'mismatch';
    }

    if (!invitation.tenantAllowed) {
      return 'tenantDenied';
    }

    return 'matching';
  });

  ngOnInit(): void {
    void this.loadPreview();
  }

  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat(this.transloco.getActiveLang(), { dateStyle: 'medium' })
      .format(new Date(value));
  }

  protected roleLabel(role: OrganizationRole): string {
    const key: Record<OrganizationRole, string> = {
      Administrator: 'organization.roleAdministrator',
      ConnectionManager: 'organization.roleConnectionManager',
      Auditor: 'organization.roleAuditor',
      Member: 'organization.roleMember',
    };
    return this.transloco.translate(key[role]);
  }

  protected async accept(): Promise<void> {
    if (!this.canAccept() || this.accepting()) {
      return;
    }

    this.accepting.set(true);
    this.acceptError.set(null);
    try {
      await firstValueFrom(this.organizations.acceptInvitation(this.token()));
      this.accepted.set(true);
    } catch (error) {
      this.acceptError.set(this.errorFor(error));
    } finally {
      this.accepting.set(false);
    }
  }

  private async loadPreview(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      this.preview.set(await firstValueFrom(this.organizations.getInvitationPreview(this.token())));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private errorFor(error: unknown): InvitationError {
    if (!(error instanceof ApiError)) {
      return 'generic';
    }

    const errors: Record<string, InvitationError> = {
      INVITATION_EMAIL_NOT_VERIFIED: 'unverified',
      INVITATION_EMAIL_MISMATCH: 'mismatch',
      INVITATION_EXPIRED: 'expired',
      INVITATION_REVOKED: 'revoked',
      INVITATION_ALREADY_ACCEPTED: 'alreadyAccepted',
      'RG-ORG-06': 'tenantDenied',
    };
    return errors[error.code] ?? 'generic';
  }
}

type InvitationStatusMessage = 'expired' | 'revoked' | 'alreadyAccepted' | 'unverified' | 'mismatch' | 'tenantDenied' | 'matching';
type InvitationError = Exclude<InvitationStatusMessage, 'matching'> | 'generic';
