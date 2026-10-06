import { EnvironmentProviders, inject, isDevMode, provideAppInitializer } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { LanguageService } from './language.service';
import { TranslocoHttpLoader } from './transloco-loader';

export function provideI18n(): EnvironmentProviders[] {
  return [
    ...provideTransloco({
        config: {
          availableLangs: ['fr', 'en'],
          defaultLang: 'fr',
          fallbackLang: 'en',
          reRenderOnLangChange: true,
          prodMode: !isDevMode(),
        },
        loader: TranslocoHttpLoader,
      }),
    provideAppInitializer(() => inject(LanguageService).initialize()),
  ];
}
