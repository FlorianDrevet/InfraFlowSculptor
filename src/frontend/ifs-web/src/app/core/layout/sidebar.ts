import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { TranslocoPipe } from '@jsverse/transloco';
import { DsIcon } from '../../ds/icon/icon';
import { LanguageSwitch } from '../i18n/language-switch/language-switch';
import { ThemeSwitch } from '../theme/theme-switch/theme-switch';
import { Brand } from './brand';
import { NavigationRegistry } from './navigation.registry';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [
    Brand,
    DsIcon,
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
  private readonly oidc = inject(OidcSecurityService);
  protected readonly accountName = computed(() => {
    const oidcState = this.oidc.userData() as unknown as {
      userData?: Record<string, unknown>;
    };
    const data = oidcState.userData;
    const name = data?.['name'];
    const username = data?.['preferred_username'];
    return typeof name === 'string' ? name : typeof username === 'string' ? username : 'IFS';
  });
}
