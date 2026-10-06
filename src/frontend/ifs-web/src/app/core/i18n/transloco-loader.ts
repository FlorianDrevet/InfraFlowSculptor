import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';

/**
 * Fetches `/i18n/<lang>.json` (served from `public/i18n/`, see the
 * generated JSON files) at runtime. Kept separate from `provide-i18n.ts` so
 * it stays trivially unit-testable.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  // Built straight from HttpBackend, bypassing the app's HttpClient
  // interceptor chain: translation files are this app's own static assets,
  // not API calls, so `apiBaseUrlInterceptor` must not prefix them.
  private readonly http = new HttpClient(inject(HttpBackend));

  getTranslation(lang: string) {
    return this.http.get<Translation>(`/i18n/${lang}.json`);
  }
}
