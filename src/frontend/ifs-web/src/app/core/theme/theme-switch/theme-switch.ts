import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DsSegmented, SegmentedOption } from '../../../ds/segmented/segmented';
import { LanguageService } from '../../i18n/language.service';
import { ThemePreference, ThemeService } from '../theme.service';

@Component({
  selector: 'app-theme-switch',
  standalone: true,
  imports: [DsSegmented],
  templateUrl: './theme-switch.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeSwitch {
  protected readonly theme = inject(ThemeService);
  private readonly language = inject(LanguageService);
  protected readonly languageLabel = computed(() =>
    this.language.activeLanguage() === 'en' ? 'Theme' : 'Thème',
  );
  protected readonly options = computed<SegmentedOption[]>(() => {
    const english = this.language.activeLanguage() === 'en';
    return [
      { value: 'system', label: english ? 'System' : 'Système' },
      { value: 'dark', label: english ? 'Dark' : 'Sombre' },
      { value: 'light', label: english ? 'Light' : 'Clair' },
    ];
  });

  protected setPreference(value: string): void {
    if (value === 'system' || value === 'dark' || value === 'light') {
      this.theme.setPreference(value satisfies ThemePreference);
    }
  }
}
