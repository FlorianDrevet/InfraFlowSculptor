import { signal } from '@angular/core';
import { fireEvent, render, screen } from '@testing-library/angular/zoneless';
import { afterEach, describe, expect, it } from 'vitest';
import { LanguageService } from '../../i18n/language.service';
import { AVAILABLE_THEME_NAMES } from '../theme.service';
import { ThemeSwitch } from './theme-switch';

describe('app-theme-switch', () => {
  afterEach(() => {
    localStorage.removeItem('ifs.theme');
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.style.removeProperty('color-scheme');
  });

  it('does not render while only one theme is available', async () => {
    await render(ThemeSwitch, {
      providers: [
        { provide: AVAILABLE_THEME_NAMES, useValue: ['dark'] },
        { provide: LanguageService, useValue: { activeLanguage: signal('fr') } },
      ],
    });

    expect(screen.queryByRole('group')).toBeNull();
  });

  it('renders when a second theme is available and applies a selected theme', async () => {
    await render(ThemeSwitch, {
      providers: [
        { provide: AVAILABLE_THEME_NAMES, useValue: ['dark', 'light'] },
        { provide: LanguageService, useValue: { activeLanguage: signal('fr') } },
      ],
    });

    fireEvent.click(screen.getByRole('button', { name: 'Clair' }));

    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    expect(localStorage.getItem('ifs.theme')).toBe('light');
  });
});
