import { EnvironmentProviders, PLATFORM_ID, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { APP_CONFIG, AppConfig, DEFAULT_APP_CONFIG } from './app-config.token';

/**
 * Loads the runtime `AppConfig` before the application renders.
 *
 * Only fetches in the browser: `/config.json` is served from `public/` (see
 * the Docker entrypoint for how it gets regenerated per-environment). On the
 * server (SSR), the first render uses the defaults below — negligible in
 * practice since the client re-runs this initializer during hydration and
 * corrects it before the user can interact with the page. This keeps
 * `app.config.ts`'s providers isomorphic without reaching for Node APIs
 * (`tsconfig.app.json` intentionally excludes Node's ambient types from the
 * browser build). If your app renders environment-sensitive content on the
 * very first SSR paint, read `API_URL` in `server.ts` instead, which does
 * have Node types available.
 *
 * If anything fails, the defaults are kept and a warning is logged: a
 * misconfigured `config.json` should never crash the whole app.
 */
export function provideAppConfig(): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: APP_CONFIG, useFactory: () => ({ ...DEFAULT_APP_CONFIG }) },
    provideAppInitializer(async () => {
      if (!isPlatformBrowser(inject(PLATFORM_ID))) {
        return;
      }

      const config = inject(APP_CONFIG);
      try {
        const response = await fetch('/config.json');
        if (response.ok) {
          Object.assign(config, (await response.json()) as Partial<AppConfig>);
        }
      } catch (error) {
        console.warn('[app-config] Could not load /config.json, using defaults.', error);
      }
    }),
  ]);
}
