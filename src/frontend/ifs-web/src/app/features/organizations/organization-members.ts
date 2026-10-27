import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, input, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';
import { OrganizationInvitations } from './organization-invitations';
import type { OrganizationMemberResponse, OrganizationRole, OrganizationSummaryResponse } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { OrganizationsService } from '../../core/organizations/organizations.service';

@Component({
  selector: 'app-organization-members',
  imports: [DsButton, DsTextField, FormsModule, OrganizationInvitations, TranslocoPipe],
  templateUrl: './organization-members.html',
  styleUrl: './organization-members.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrganizationMembers implements OnInit {
  readonly id = input.required<string>();
  private readonly organizations = inject(OrganizationsService);
  @ViewChild('removeDialog') private removeDialog?: ElementRef<HTMLDialogElement>;

  protected readonly organization = signal<OrganizationSummaryResponse | null>(null);
  protected readonly members = signal<OrganizationMemberResponse[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly lastAdministratorError = signal(false);
  protected readonly search = signal('');
  protected readonly editingMemberId = signal<string | null>(null);
  protected readonly selectedRoles = signal<OrganizationRole[]>([]);
  protected readonly savingMemberId = signal<string | null>(null);
  protected readonly pendingRemoval = signal<OrganizationMemberResponse | null>(null);
  protected readonly isAdministrator = computed(() =>
    this.organization()?.roles.includes('Administrator') ?? false,
  );
  protected readonly filteredMembers = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    if (!query) {
      return this.members();
    }

    return this.members().filter(member =>
      `${member.displayName} ${member.email ?? ''}`.toLocaleLowerCase().includes(query),
    );
  });
  protected readonly roleChoices: OrganizationRole[] = [
    'Administrator',
    'ConnectionManager',
    'Auditor',
    'Member',
  ];

  ngOnInit(): void {
    void this.loadMembers();
  }

  protected editRoles(member: OrganizationMemberResponse): void {
    this.saveError.set(false);
    this.lastAdministratorError.set(false);
    this.selectedRoles.set([...member.roles]);
    this.editingMemberId.set(member.id);
  }

  protected toggleRole(role: OrganizationRole, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedRoles.update(roles =>
      checked
        ? roles.includes(role) ? roles : [...roles, role]
        : roles.filter(selected => selected !== role),
    );
  }

  protected cancelRoleEdit(): void {
    this.editingMemberId.set(null);
    this.selectedRoles.set([]);
    this.saveError.set(false);
    this.lastAdministratorError.set(false);
  }

  protected async saveRoles(member: OrganizationMemberResponse): Promise<void> {
    if (this.savingMemberId()) {
      return;
    }

    this.savingMemberId.set(member.id);
    this.saveError.set(false);
    this.lastAdministratorError.set(false);
    try {
      const updated = await firstValueFrom(
        this.organizations.changeMemberRoles(this.id(), member.id, this.selectedRoles()),
      );
      this.members.update(current => current.map(item => item.id === updated.id ? updated : item));
      this.cancelRoleEdit();
    } catch (error) {
      this.recordSaveError(error);
    } finally {
      this.savingMemberId.set(null);
    }
  }

  protected requestRemoval(member: OrganizationMemberResponse): void {
    this.pendingRemoval.set(member);
    this.saveError.set(false);
    this.lastAdministratorError.set(false);
    this.removeDialog?.nativeElement.showModal();
  }

  protected cancelRemoval(): void {
    if (this.removeDialog?.nativeElement.open) {
      this.removeDialog.nativeElement.close();
    }
    this.pendingRemoval.set(null);
    this.saveError.set(false);
    this.lastAdministratorError.set(false);
  }

  protected async confirmRemoval(): Promise<void> {
    const member = this.pendingRemoval();
    if (!member || this.savingMemberId()) {
      return;
    }

    this.savingMemberId.set(member.id);
    this.saveError.set(false);
    this.lastAdministratorError.set(false);
    try {
      await firstValueFrom(this.organizations.removeMember(this.id(), member.id));
      this.members.update(current => current.filter(item => item.id !== member.id));
      this.cancelRemoval();
    } catch (error) {
      this.recordSaveError(error);
    } finally {
      this.savingMemberId.set(null);
    }
  }

  private async loadMembers(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const [organization, members] = await Promise.all([
        firstValueFrom(this.organizations.get(this.id())),
        firstValueFrom(this.organizations.listMembers(this.id())),
      ]);
      this.organization.set(organization);
      this.members.set(members);
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private recordSaveError(error: unknown): void {
    this.saveError.set(true);
    if (!(error instanceof ApiError)) {
      this.lastAdministratorError.set(false);
      return;
    }

    this.lastAdministratorError.set(error.code === 'RG-ORG-05');
  }
}
