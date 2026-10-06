import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { Router } from '@angular/router';
import { timeout } from 'rxjs';
import { Api, getMe, GetMe$Params, MeResponse } from '../../core/api/generated';
import { ApiError } from '../../core/http/api-error';
import { NavigationRegistry } from '../../core/layout/navigation.registry';

@Component({
  selector: 'app-home',
  imports: [TranslocoPipe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home implements OnInit, OnDestroy {
  protected readonly currentUser = rxResource<MeResponse, GetMe$Params>({
    params: () => ({}),
    stream: ({ params }) => this.api.invoke(getMe, params).pipe(timeout({ first: 10_000 })),
  });
  private readonly api = inject(Api);
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
