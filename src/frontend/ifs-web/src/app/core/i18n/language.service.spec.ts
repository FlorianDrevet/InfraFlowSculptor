import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UserPreferencesService } from '../preferences/user-preferences.service';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  const setActiveLang = vi.fn();

  beforeEach(() => {
    localStorage.removeItem('ifs.language');
    setActiveLang.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: TranslocoService, useValue: { setActiveLang } },
        {
          provide: UserPreferencesService,
          useValue: { preferredLanguage: signal(null), setLanguage: vi.fn() },
        },
      ],
    });
  });

  it('changes the active language, document language, and saved preference together', () => {
    const language = TestBed.inject(LanguageService);

    language.setLanguage('en');

    expect(language.activeLanguage()).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(localStorage.getItem('ifs.language')).toBe('en');
    expect(setActiveLang).toHaveBeenCalledWith('en');
  });
});
