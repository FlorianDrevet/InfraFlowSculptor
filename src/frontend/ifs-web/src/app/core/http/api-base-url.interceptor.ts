import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config.token';

/**
 * Prepends `AppConfig.apiUrl` to every relative HTTP request, so feature
 * code can call `http.get('/users')` instead of hardcoding the API origin.
 * Absolute URLs (`http://...`, `https://...`) are left untouched, so calls
 * to third-party APIs still work.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const { apiUrl } = inject(APP_CONFIG);

  if (/^https?:\/\//i.test(req.url)) {
    return next(req);
  }

  return next(req.clone({ url: `${apiUrl}${req.url.startsWith('/') ? '' : '/'}${req.url}` }));
};
