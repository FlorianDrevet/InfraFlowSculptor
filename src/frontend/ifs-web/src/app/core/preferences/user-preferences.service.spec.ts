import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Api } from '../api/generated';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UserPreferencesService } from './user-preferences.service';

describe('UserPreferencesService', () => {
  const invoke = vi.fn();

  beforeEach(() => {
    invoke.mockReset();
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: { invoke } }] });
  });

  it('reloads server preferences before persisting a local change after a failed load', () => {
    invoke
      .mockReturnValueOnce(throwError(() => new Error('offline')))
      .mockReturnValueOnce(of({ preferredLanguage: 'en', preferredTheme: 'light' }))
      .mockReturnValueOnce(of(undefined));

    const preferences = TestBed.inject(UserPreferencesService);
    preferences.load();
    preferences.setTheme('dark');

    expect(invoke).toHaveBeenCalledTimes(3);
    expect(invoke.mock.calls[2][1]).toEqual({
      body: { preferredLanguage: 'en', preferredTheme: 'dark' },
    });
  });
});
