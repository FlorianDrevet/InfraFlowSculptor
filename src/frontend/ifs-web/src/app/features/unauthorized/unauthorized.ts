import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { RouterLink } from '@angular/router';

/**
 * Target of `angular-auth-oidc-client`'s `unauthorizedRoute`: the library
 * navigates here when the OIDC callback fails,
 * e.g. when the authorization code cannot be exchanged for tokens.
 */
@Component({
  selector: 'app-unauthorized',
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './unauthorized.html',
  styleUrl: './unauthorized.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Unauthorized {}
