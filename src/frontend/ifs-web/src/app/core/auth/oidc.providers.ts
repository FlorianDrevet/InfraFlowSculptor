import { HttpBackend, HttpClient } from '@angular/common/http';
import { EnvironmentProviders } from '@angular/core';
import {
  LogLevel,
  PassedInitialConfig,
  StsConfigHttpLoader,
  StsConfigLoader,
  provideAuth,
  withAppInitializerAuthCheck,
} from 'angular-auth-oidc-client';
import { map } from 'rxjs';
import { AppConfig } from '../config/app-config.token';

export function buildOidcConfiguration(config: AppConfig, origin: string) {
  const { oidc } = config;
  const isEntra = oidc.provider === 'entra';
  const apiBaseUrl = config.apiUrl.trim().replace(/\/+$/, '');

  return {
    authority: oidc.authority,
    authWellknownEndpointUrl: isEntra ? 'https://login.microsoftonline.com/common/v2.0' : undefined,
    strictIssuerValidationOnWellKnownRetrievalOff: isEntra,
    customParamsAuthRequest: isEntra ? { prompt: 'select_account' } : undefined,
    clientId: oidc.clientId,
    redirectUrl: origin,
    postLogoutRedirectUri: origin,
    unauthorizedRoute: '/unauthorized',
    responseType: 'code',
    scope: oidc.scope,
    disableRefreshTokenOfflineAccessScopeWarning: !isEntra,
    secureRoutes: [apiBaseUrl ? `${apiBaseUrl}/v1/` : '/v1/'],
    silentRenew: true,
    useRefreshToken: true,
    logLevel: LogLevel.Warn,
  };
}

/**
 * Reads the same runtime configuration as `provideAppConfig`. This client
 * bypasses the API base URL interceptor because `/config.json` is a web asset.
 */
function oidcConfigLoaderFactory(httpBackend: HttpBackend): StsConfigHttpLoader {
  const http = new HttpClient(httpBackend);
  const config$ = http.get<AppConfig>('/config.json').pipe(
    map((config) => {
      const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:4200';

      return buildOidcConfiguration(config, origin);
    }),
  );

  return new StsConfigHttpLoader(config$);
}

export function provideOidcAuth(): EnvironmentProviders {
  const authConfig: PassedInitialConfig = {
    loader: {
      provide: StsConfigLoader,
      useFactory: oidcConfigLoaderFactory,
      deps: [HttpBackend],
    },
  };

  return provideAuth(
    authConfig,
    withAppInitializerAuthCheck(),
  );
}
