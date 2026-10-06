import { describe, expect, it } from 'vitest';
import { DEFAULT_APP_CONFIG } from '../config/app-config.token';
import { buildOidcConfiguration } from './oidc.providers';

describe('OIDC configuration', () => {
  it('uses an online Keycloak session for the browser application by default', () => {
    expect(DEFAULT_APP_CONFIG.oidc.scope).toBe('openid profile email');
    expect(buildOidcConfiguration(DEFAULT_APP_CONFIG, 'http://localhost:4200'))
      .toHaveProperty('disableRefreshTokenOfflineAccessScopeWarning', true);
  });

  it('limits automatic access-token attachment to the runtime API URL', () => {
    const runtimeConfig = { ...DEFAULT_APP_CONFIG, apiUrl: 'https://api.example.test' };
    const configuration = buildOidcConfiguration(runtimeConfig, 'http://localhost:4200') as {
      secureRoutes?: string[];
      unauthorizedRoute?: string;
    };

    expect(configuration.secureRoutes).toEqual(['https://api.example.test/v1/']);
    expect(configuration.unauthorizedRoute).toBe('/unauthorized');
    expect('https://api.example.test.evil.com/v1/me'.startsWith(configuration.secureRoutes![0])).toBe(false);
  });

  it('keeps same-origin access tokens scoped to the API route when the API base URL is empty', () => {
    const runtimeConfig = { ...DEFAULT_APP_CONFIG, apiUrl: '' };
    const configuration = buildOidcConfiguration(runtimeConfig, 'http://localhost:4200') as {
      secureRoutes?: string[];
    };

    expect(configuration.secureRoutes).toEqual(['/v1/']);
    expect('/config.json'.startsWith(configuration.secureRoutes![0])).toBe(false);
  });
});
