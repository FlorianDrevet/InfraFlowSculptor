import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Router } from '@angular/router';
import { ApiError } from '../../core/http/api-error';
import { NavigationRegistry } from '../../core/layout/navigation.registry';

interface CurrentUser {
  displayName: string;
}

@Component({
  selector: 'app-home',
  imports: [TranslocoPipe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home implements OnInit, OnDestroy {
  protected readonly currentUser = httpResource<CurrentUser>(() => ({
    url: '/v1/me',
    timeout: 10_000,
  }));
  private readonly router = inject(Router);
  private readonly navigation = inject(NavigationRegistry);
  private unregisterNavigation?: () => void;

  private readonly navigateToError = effect(() => {
    const failure = this.currentUser.error();
    if (!failure) {
      return;
    }

    const serverReference = failure instanceof ApiError ? failure.traceId : null;
    const reference = serverReference ?? `local-${globalThis.crypto?.randomUUID?.() ?? Date.now()}`;
    void this.router.navigate(['/error'], { queryParams: { reference }, replaceUrl: true });
  });

  ngOnInit(): void {
    this.unregisterNavigation = this.navigation.register({
      id: 'home',
      labelKey: 'shell.home',
      path: '/',
      scope: 'workspace',
    });
  }

  ngOnDestroy(): void {
    this.unregisterNavigation?.();
  }
}
