import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageService } from '../i18n/language.service';
import { isApiRequest } from './is-api-request';

export const acceptLanguageInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request)) {
    return next(request);
  }

  return next(
    request.clone({ setHeaders: { 'Accept-Language': inject(LanguageService).activeLanguage() } }),
  );
};
