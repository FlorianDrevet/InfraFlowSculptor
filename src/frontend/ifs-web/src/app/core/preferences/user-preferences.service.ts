import { Injectable, inject, signal } from '@angular/core';
import { Api, getMe, updateMePreferences } from '../api/generated';
import type { SupportedLanguage } from '../i18n/language.service';
import type { ThemePreference } from '../theme/theme.service';

@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly api = inject(Api);
  private readonly preferredLanguageState = signal<SupportedLanguage | null>(null);
  private readonly preferredThemeState = signal<ThemePreference | null>(null);
  private loadStarted = false;
  private loaded = false;
  private languageChanged = false;
  private themeChanged = false;

  readonly preferredLanguage = this.preferredLanguageState.asReadonly();
  readonly preferredTheme = this.preferredThemeState.asReadonly();

  load(): void {
    if (this.loadStarted) {
      return;
    }

    this.loadStarted = true;
    this.api.invoke(getMe, {}).subscribe({
      next: (profile) => {
        if (!this.languageChanged && (profile.preferredLanguage === 'fr' || profile.preferredLanguage === 'en')) {
          this.preferredLanguageState.set(profile.preferredLanguage);
        }
        if (
          !this.themeChanged &&
          (profile.preferredTheme === 'system' || profile.preferredTheme === 'dark' || profile.preferredTheme === 'light')
        ) {
          this.preferredThemeState.set(profile.preferredTheme);
        }

        this.loaded = true;
        if (this.languageChanged || this.themeChanged) {
          this.persist();
        }
      },
      error: () => {
        this.loaded = true;
        if (this.languageChanged || this.themeChanged) {
          this.persist();
        }
      },
    });
  }

  setLanguage(language: SupportedLanguage): void {
    this.languageChanged = true;
    this.preferredLanguageState.set(language);
    this.persistWhenLoaded();
  }

  setTheme(theme: ThemePreference): void {
    this.themeChanged = true;
    this.preferredThemeState.set(theme);
    this.persistWhenLoaded();
  }

  private persistWhenLoaded(): void {
    if (this.loaded) {
      this.persist();
    }
  }

  private persist(): void {
    this.api.invoke(updateMePreferences, {
      body: {
        preferredLanguage: this.preferredLanguageState() ?? 'fr',
        preferredTheme: this.preferredThemeState() ?? 'system',
      },
    }).subscribe({ error: () => undefined });
  }
}
