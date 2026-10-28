import { ChangeDetectionStrategy, Component, effect, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { DsDialog } from '../../ds/dialog/dialog';
import { FeatureAvailabilityService } from '../features/feature-availability.service';
import { ActiveOrganizationStore } from '../http/active-organization.store';
import { OrganizationsService } from '../organizations/organizations.service';
import { UserPreferencesService } from '../preferences/user-preferences.service';
import { FEATURES } from '../features/feature-keys';
import { NavigationRegistry } from './navigation.registry';
import { Sidebar } from './sidebar';
import { Topbar } from './topbar';

@Component({
  selector: 'app-shell',
  imports: [DsDialog, RouterOutlet, Sidebar, Topbar, TranslocoPipe],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell implements OnInit {
  private readonly organizations = inject(OrganizationsService);
  private readonly activeOrganization = inject(ActiveOrganizationStore);
  private readonly features = inject(FeatureAvailabilityService);
  private readonly preferences = inject(UserPreferencesService);
  private readonly navigation = inject(NavigationRegistry);

  private readonly loadFeaturesForOrganization = effect(() => {
    if (!this.activeOrganization.organizationId()) {
      return;
    }

    void this.features.ensureLoaded().catch(() => undefined);
  });

  private readonly registerOrganizationMembers = effect(onCleanup => {
    const organizationId = this.activeOrganization.organizationId();
    if (!organizationId) {
      return;
    }

    onCleanup(
      this.navigation.register({
        id: 'organization-members',
        labelKey: 'organization.membersLink',
        path: `/organizations/${organizationId}/members`,
        scope: 'organization',
        featureKey: FEATURES.organizations,
        iconName: 'users',
      }),
    );
  });

  private readonly registerOrganizationAudit = effect(onCleanup => {
    const organizationId = this.activeOrganization.organizationId();
    const organization = this.organizations.organizations().find(item => item.id === organizationId);
    const canReadAudit = organization?.roles.some(role =>
      role === 'Administrator' || role === 'Auditor',
    );
    if (!organizationId || !canReadAudit) {
      return;
    }

    onCleanup(
      this.navigation.register({
        id: 'organization-audit',
        labelKey: 'organization.auditLink',
        path: `/organizations/${organizationId}/audit`,
        scope: 'organization',
        featureKey: FEATURES.organizations,
        iconName: 'clock',
      }),
    );
  });

  private readonly registerOrganizationSettings = effect(onCleanup => {
    const organizationId = this.activeOrganization.organizationId();
    if (!organizationId) {
      return;
    }

    onCleanup(
      this.navigation.register({
        id: 'organization-settings',
        labelKey: 'organization.settingsLink',
        path: `/organizations/${organizationId}/settings`,
        scope: 'organization',
        featureKey: FEATURES.organizations,
        iconName: 'sliders',
      }),
    );
  });

  ngOnInit(): void {
    this.preferences.load();
    void this.organizations.ensureLoaded().catch(() => undefined);
  }
}
