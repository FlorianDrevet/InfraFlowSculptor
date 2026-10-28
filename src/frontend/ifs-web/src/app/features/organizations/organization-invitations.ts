import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { InvitationResponse, OrganizationRole } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { OrganizationsService } from '../../core/organizations/organizations.service';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';

@Component({
  selector: 'app-organization-invitations',
  standalone: true,
  imports: [DsButton, DsTextField, FormsModule, TranslocoPipe],
  templateUrl: './organization-invitations.html',
  styleUrl: './organization-invitations.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationInvitations implements OnInit {
  readonly id = input.required<string>();
  readonly canManage = input(false);
  readonly allowedEmailDomains = input<string[]>([]);
  private readonly organizations = inject(OrganizationsService);

  protected readonly invitations = signal<InvitationResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly email = signal('');
  protected readonly selectedRoles = signal<OrganizationRole[]>(['Member']);
  protected readonly saving = signal(false);
  protected readonly actionId = signal<string | null>(null);
  protected readonly actionError = signal<InvitationActionError | null>(null);
  protected readonly sendSucceeded = signal(false);
  protected readonly pendingInvitations = computed(() =>
    this.invitations().filter(invitation => invitation.status === 'Pending' || invitation.status === 'Expired'),
  );
  protected readonly roleChoices: OrganizationRole[] = [
    'Administrator',
    'ConnectionManager',
    'Auditor',
    'Member',
  ];

  ngOnInit(): void {
    void this.loadInvitations();
  }

  protected toggleRole(role: OrganizationRole, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedRoles.update(roles =>
      checked
        ? roles.includes(role) ? roles : [...roles, role]
        : roles.filter(selected => selected !== role),
    );
    this.actionError.set(null);
  }

  protected async invite(): Promise<void> {
    const email = this.email().trim();
    if (this.saving() || email.length === 0 || this.selectedRoles().length === 0) {
      this.actionError.set('form');
      return;
    }

    this.saving.set(true);
    this.actionError.set(null);
    this.sendSucceeded.set(false);
    try {
      const invitation = await firstValueFrom(
        this.organizations.invite(this.id(), email, this.selectedRoles()),
      );
      this.invitations.update(current => [invitation, ...current.filter(item => item.id !== invitation.id)]);
      this.email.set('');
      this.selectedRoles.set(['Member']);
      this.sendSucceeded.set(true);
    } catch (error) {
      this.actionError.set(this.errorFor(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async resend(invitation: InvitationResponse): Promise<void> {
    if (this.actionId() || !this.canManage()) {
      return;
    }

    this.actionId.set(invitation.id);
    this.actionError.set(null);
    this.sendSucceeded.set(false);
    try {
      const updated = await firstValueFrom(this.organizations.resendInvitation(this.id(), invitation.id));
      this.invitations.update(current => current.map(item => item.id === updated.id ? updated : item));
      this.sendSucceeded.set(true);
    } catch (error) {
      this.actionError.set(this.errorFor(error));
    } finally {
      this.actionId.set(null);
    }
  }

  protected async revoke(invitation: InvitationResponse): Promise<void> {
    if (this.actionId() || !this.canManage() || invitation.status !== 'Pending') {
      return;
    }

    this.actionId.set(invitation.id);
    this.actionError.set(null);
    this.sendSucceeded.set(false);
    try {
      await firstValueFrom(this.organizations.revokeInvitation(this.id(), invitation.id));
      this.invitations.update(current => current.filter(item => item.id !== invitation.id));
    } catch (error) {
      this.actionError.set(this.errorFor(error));
    } finally {
      this.actionId.set(null);
    }
  }

  protected daysUntilExpiry(invitation: InvitationResponse): number {
    const expiry = new Date(invitation.expiresAt).getTime();
    return Math.max(0, Math.ceil((expiry - Date.now()) / 86_400_000));
  }

  private async loadInvitations(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      this.invitations.set(await firstValueFrom(this.organizations.listInvitations(this.id())));
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private errorFor(error: unknown): InvitationActionError {
    if (!(error instanceof ApiError)) {
      return 'generic';
    }

    if (error.code === 'RG-ORG-06') {
      return 'access';
    }

    if (error.code === 'INVITATION_ALREADY_PENDING') {
      return 'alreadyPending';
    }

    return 'generic';
  }
}

type InvitationActionError = 'form' | 'access' | 'alreadyPending' | 'generic';
