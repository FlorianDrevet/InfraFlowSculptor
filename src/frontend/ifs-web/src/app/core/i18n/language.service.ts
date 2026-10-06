import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

export type SupportedLanguage = 'fr' | 'en';

const LANGUAGE_STORAGE_KEY = 'ifs.language';

function isSupportedLanguage(value: string | null | undefined): value is SupportedLanguage {
  return value === 'fr' || value === 'en';
}

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly document = inject(DOCUMENT);
  private readonly transloco = inject(TranslocoService);

  initialize(): void {
    let language: SupportedLanguage = 'fr';

    if (this.browser) {
      try {
        const stored = globalThis.localStorage.getItem(LANGUAGE_STORAGE_KEY);
        const browserLanguage = globalThis.navigator.language.split('-')[0]?.toLowerCase();
        if (isSupportedLanguage(stored)) {
          language = stored;
        } else if (isSupportedLanguage(browserLanguage)) {
          language = browserLanguage;
        }
      } catch {
        // Storage may be unavailable in private browsing; use the browser locale fallback.
        const browserLanguage = globalThis.navigator.language.split('-')[0]?.toLowerCase();
        if (isSupportedLanguage(browserLanguage)) {
          language = browserLanguage;
        }
      }
    }

    this.activate(language);
  }

  setLanguage(language: SupportedLanguage): void {
    this.activate(language);
    if (!this.browser) {
      return;
    }

    try {
      globalThis.localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
    } catch {
      // The active language still changes when browser storage is unavailable.
    }
  }

  private activate(language: SupportedLanguage): void {
    this.transloco.setActiveLang(language);
    this.document.documentElement.lang = language;
  }
}
