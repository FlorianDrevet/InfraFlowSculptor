import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DsButton } from '../../ds/button/button';
import { DsTextField } from '../../ds/text-field/text-field';
import type {
  OrganizationMemberResponse,
  ProjectRole,
  ProjectRoleAssignmentResponse,
} from '../../core/api/generated';
import { ProjectsService } from '../../core/projects/projects.service';
import { OrganizationsService } from '../../core/organizations/organizations.service';

type ProjectMember = {
  member: OrganizationMemberResponse;
  assignments: ProjectRoleAssignmentResponse[];
};

const ROLE_LABEL_KEYS: Record<ProjectRole, string> = {
  Reader: 'project.roleReader',
  Auditor: 'project.roleAuditor',
  Developer: 'project.roleDeveloper',
  Contributor: 'project.roleContributor',
  DeploymentManager: 'project.roleDeploymentManager',
  PlatformArchitect: 'project.rolePlatformArchitect',
  ProjectAdministrator: 'project.roleProjectAdministrator',
  Owner: 'project.roleOwner',
};

@Component({
  selector: 'app-project-members',
  imports: [DsButton, DsTextField, FormsModule, TranslocoPipe],
  templateUrl: './project-members.html',
  styleUrl: './project-members.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectMembers implements OnInit {
  readonly projectId = input.required<string>();
  private readonly route = inject(ActivatedRoute);
  private readonly projects = inject(ProjectsService);
  private readonly organizations = inject(OrganizationsService);

  protected readonly members = signal<ProjectMember[]>([]);
  protected readonly permissions = signal<string[]>([]);
  protected readonly projectVersion = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly saveError = signal(false);
  protected readonly search = signal('');
  protected readonly editingMemberId = signal<string | null>(null);
  protected readonly selectedRoles = signal<ProjectRole[]>([]);
  protected readonly savingMemberId = signal<string | null>(null);
  protected readonly isOwner = signal(false);
  protected readonly canManage = computed(() => this.permissions().includes('members.manage'));
  protected readonly roleChoices = computed<ProjectRole[]>(() =>
    this.isOwner()
      ? [
          'Reader',
          'Auditor',
          'Developer',
          'Contributor',
          'DeploymentManager',
          'PlatformArchitect',
          'ProjectAdministrator',
          'Owner',
        ]
      : [
          'Reader',
          'Auditor',
          'Developer',
          'Contributor',
          'DeploymentManager',
          'PlatformArchitect',
          'ProjectAdministrator',
        ],
  );
  protected readonly filteredMembers = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    if (!query) {
      return this.members();
    }
    return this.members().filter(({ member }) =>
      `${member.displayName} ${member.email ?? ''}`.toLocaleLowerCase().includes(query),
    );
  });

  ngOnInit(): void {
    void this.load();
  }

  protected roleLabelKey(role: ProjectRole): string {
    return ROLE_LABEL_KEYS[role];
  }

  protected editRoles(projectMember: ProjectMember): void {
    this.saveError.set(false);
    this.selectedRoles.set(
      projectMember.assignments
        .filter((assignment) => assignment.scope === 'Project')
        .map((assignment) => assignment.role),
    );
    this.editingMemberId.set(projectMember.member.id);
  }

  protected toggleRole(role: ProjectRole, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedRoles.update((roles) =>
      checked
        ? roles.includes(role)
          ? roles
          : [...roles, role]
        : roles.filter((selected) => selected !== role),
    );
  }

  protected cancelEdit(): void {
    this.editingMemberId.set(null);
    this.selectedRoles.set([]);
    this.saveError.set(false);
  }

  protected async saveRoles(projectMember: ProjectMember): Promise<void> {
    if (!this.canManage() || this.savingMemberId()) {
      return;
    }

    const memberId = projectMember.member.id;
    const currentRoles = projectMember.assignments
      .filter((assignment) => assignment.scope === 'Project')
      .map((assignment) => assignment.role);
    const selectedRoles = this.selectedRoles();
    let version = this.projectVersion();
    this.savingMemberId.set(memberId);
    this.saveError.set(false);
    try {
      for (const role of currentRoles.filter((candidate) => !selectedRoles.includes(candidate))) {
        await firstValueFrom(
          this.projects.removeRole(this.projectId(), memberId, role, 'Project', version),
        );
        version += 1;
      }
      for (const role of selectedRoles.filter((candidate) => !currentRoles.includes(candidate))) {
        const assignment = await firstValueFrom(
          this.projects.assignRole(this.projectId(), memberId, {
            role,
            scope: 'Project',
            expectedVersion: version,
          }),
        );
        version = Number(assignment.version);
      }
      await this.load();
      this.cancelEdit();
    } catch {
      await this.load();
      this.saveError.set(true);
    } finally {
      this.savingMemberId.set(null);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set(false);
    try {
      const organizationId = this.organizationId();
      if (!organizationId) {
        throw new Error('The organization route parameter is missing.');
      }

      const [project, permissions, organizationMembers] = await Promise.all([
        firstValueFrom(this.projects.get(this.projectId())),
        firstValueFrom(this.projects.myPermissions(this.projectId())),
        firstValueFrom(this.organizations.listMembers(organizationId)),
      ]);
      this.projectVersion.set(Number(project.version));
      this.permissions.set(permissions.permissions);
      this.isOwner.set(permissions.assignments.some((assignment) => assignment.role === 'Owner'));
      this.members.set(
        await Promise.all(
          organizationMembers.map(async (member) => {
            const permissionsForMember = await firstValueFrom(
              this.projects.memberPermissions(this.projectId(), member.id),
            );
            return { member, assignments: permissionsForMember.assignments };
          }),
        ),
      );
    } catch {
      this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private organizationId(): string | null {
    let current = this.route.snapshot;
    while (current) {
      const organizationId = current.paramMap.get('orgId');
      if (organizationId) {
        return organizationId;
      }
      if (!current.parent) {
        break;
      }
      current = current.parent;
    }
    return null;
  }
}
