import { HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config.token';

/** Returns whether a request targets this application’s versioned API. */
export function isApiRequest(request: HttpRequest<unknown>): boolean {
  const { apiUrl } = inject(APP_CONFIG);
  const appOrigin = typeof window === 'undefined' ? 'http://localhost' : window.location.origin;

  try {
    const apiRoot = new URL(apiUrl || '/', appOrigin);
    const requestUrl = new URL(request.url, apiRoot);
    const apiPath = `${apiRoot.pathname.replace(/\/+$/, '')}/v1`;

    return (
      requestUrl.origin === apiRoot.origin &&
      (requestUrl.pathname === apiPath || requestUrl.pathname.startsWith(`${apiPath}/`))
    );
  } catch {
    return false;
  }
}
