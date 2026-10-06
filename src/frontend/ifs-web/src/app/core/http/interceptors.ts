import { authInterceptor } from 'angular-auth-oidc-client';
import { HttpInterceptorFn } from '@angular/common/http';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';
import { errorInterceptor } from './error.interceptor';

/**
 * The functional HTTP interceptor chain, in order, passed to
 * `provideHttpClient(withInterceptors(httpInterceptors))` in `app.config.ts`.
 *
 * Kept as its own array (rather than inlined in `app.config.ts`) so the
 * `auth` schematic can append an auth interceptor to it without having to
 * parse the `provideHttpClient(...)` call itself.
 */
export const httpInterceptors: HttpInterceptorFn[] = [apiBaseUrlInterceptor, authInterceptor(), errorInterceptor];
