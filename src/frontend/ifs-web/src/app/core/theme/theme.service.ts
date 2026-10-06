import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import {
  inject,
  Injectable,
  InjectionToken,
  OnDestroy,
  PLATFORM_ID,
  signal,
  computed,
} from '@angular/core';
import { AVAILABLE_THEMES, DEFAULT_THEME } from './themes.generated';

export type ThemeName = 'dark' | 'light';
export type ThemePreference = 'system' | ThemeName;

export const AVAILABLE_THEME_NAMES = new InjectionToken<readonly ThemeName[]>(
  'IFS available themes',
  {
    factory: () => AVAILABLE_THEMES,
  },
);

const THEME_STORAGE_KEY = 'ifs.theme';

function isThemePreference(value: string | null): value is ThemePreference {
  return value === 'system' || value === 'dark' || value === 'light';
}

@Injectable({ providedIn: 'root' })
export class ThemeService implements OnDestroy {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly themeNames = inject(AVAILABLE_THEME_NAMES);
  private readonly preferenceState = signal<ThemePreference>('system');
  private readonly prefersDarkState = signal(false);
  private mediaQuery?: MediaQueryList;
  private initialized = false;

  readonly preference = this.preferenceState.asReadonly();
  readonly availableThemes = this.themeNames;
  readonly effectiveTheme = computed(() => {
    const preferred = this.preferenceState();
    const desired =
      preferred === 'system' ? (this.prefersDarkState() ? 'dark' : 'light') : preferred;
    return this.themeNames.includes(desired) ? desired : (this.themeNames[0] ?? DEFAULT_THEME);
  });

  initialize(): void {
    if (this.initialized) {
      return;
    }
    this.initialized = true;

    if (this.isBrowser) {
      try {
        const stored = globalThis.localStorage.getItem(THEME_STORAGE_KEY);
        if (isThemePreference(stored)) {
          this.preferenceState.set(stored);
        }
      } catch {
        // Storage can be disabled; the system preference is still applied.
      }

      const browserWindow = this.document.defaultView;
      this.mediaQuery = browserWindow?.matchMedia?.('(prefers-color-scheme: dark)');
      this.prefersDarkState.set(this.mediaQuery?.matches ?? false);
      this.mediaQuery?.addEventListener('change', this.onSystemPreferenceChanged);
    }

    this.applyTheme();
  }

  setPreference(preference: ThemePreference): void {
    this.preferenceState.set(preference);
    this.applyTheme();

    if (!this.isBrowser) {
      return;
    }

    try {
      globalThis.localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // The current tab still changes theme when storage is unavailable.
    }
  }

  ngOnDestroy(): void {
    this.mediaQuery?.removeEventListener('change', this.onSystemPreferenceChanged);
  }

  private readonly onSystemPreferenceChanged = (event: MediaQueryListEvent): void => {
    this.prefersDarkState.set(event.matches);
    this.applyTheme();
  };

  private applyTheme(): void {
    const theme = this.effectiveTheme();
    const root = this.document.documentElement;
    root.setAttribute('data-theme', theme);
    root.style.colorScheme = theme;
  }
}
