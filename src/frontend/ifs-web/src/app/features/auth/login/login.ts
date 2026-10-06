import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { DsBadge } from '../../../ds/badge/badge';
import { DsButton } from '../../../ds/button/button';
import { Brand } from '../../../core/layout/brand';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [Brand, DsBadge, DsButton, TranslocoPipe],
  templateUrl: './login.html',
  styleUrl: './login.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private readonly oidc = inject(OidcSecurityService);

  protected continueWithMicrosoft(): void {
    this.oidc.authorize();
  }
}
