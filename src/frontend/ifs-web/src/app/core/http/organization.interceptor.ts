import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ActiveOrganizationStore } from './active-organization.store';
import { isApiRequest } from './is-api-request';

export const organizationInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request)) {
    return next(request);
  }

  const organizationId = inject(ActiveOrganizationStore).organizationId();

  if (!organizationId || request.headers.has('X-Ifs-Organization')) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { 'X-Ifs-Organization': organizationId } }));
};
