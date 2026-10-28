import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { TranslocoPipe } from '@jsverse/transloco';
import { DsIcon } from '../../ds/icon/icon';
import { DsSelect } from '../../ds/select/select';
import { ActiveOrganizationStore } from '../http/active-organization.store';
import { LanguageSwitch } from '../i18n/language-switch/language-switch';
import { OrganizationsService } from '../organizations/organizations.service';
import { ThemeSwitch } from '../theme/theme-switch/theme-switch';
import { Brand } from './brand';
import { NavigationRegistry } from './navigation.registry';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [
    Brand,
    DsIcon,
    DsSelect,
    LanguageSwitch,
    RouterLink,
    RouterLinkActive,
    ThemeSwitch,
    TranslocoPipe,
  ],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Sidebar {
  readonly drawer = input(false);
  protected readonly navigation = inject(NavigationRegistry);
  protected readonly organizations = inject(OrganizationsService);
  protected readonly activeOrganization = inject(ActiveOrganizationStore);
  private readonly oidc = inject(OidcSecurityService);
  private readonly router = inject(Router);
  protected readonly accountName = computed(() => {
    const oidcState = this.oidc.userData() as unknown as {
      userData?: Record<string, unknown>;
    };
    const data = oidcState.userData;
    const name = data?.['name'];
    const username = data?.['preferred_username'];
    return typeof name === 'string' ? name : typeof username === 'string' ? username : 'IFS';
  });

  protected selectOrganization(organizationId: string): void {
    if (!organizationId || organizationId === this.activeOrganization.organizationId()) {
      return;
    }

    this.activeOrganization.setOrganizationId(organizationId);
    void this.router.navigateByUrl('/');
  }
}
