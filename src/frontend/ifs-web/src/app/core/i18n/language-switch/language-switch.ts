import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { DsSegmented } from '../../../ds/segmented/segmented';
import { LanguageService, SupportedLanguage } from '../language.service';

@Component({
  selector: 'app-language-switch',
  standalone: true,
  imports: [DsSegmented, TranslocoPipe],
  templateUrl: './language-switch.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitch {
  protected readonly language = inject(LanguageService);
  protected readonly options = [
    { value: 'fr', label: 'FR' },
    { value: 'en', label: 'EN' },
  ];

  protected setLanguage(value: string): void {
    if (value === 'fr' || value === 'en') {
      this.language.setLanguage(value satisfies SupportedLanguage);
    }
  }
}
