import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DsIcon, IconName } from '../icon/icon';

export type BannerTone = 'ember' | 'danger' | 'success' | 'signal';

const BANNER_ICONS: Record<BannerTone, IconName> = {
  ember: 'alert',
  danger: 'x',
  success: 'check',
  signal: 'zap',
};

@Component({
  selector: 'app-ds-banner',
  standalone: true,
  imports: [DsIcon],
  templateUrl: './banner.html',
  styleUrl: './banner.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsBanner {
  readonly tone = input<BannerTone>('ember');
  readonly title = input<string | undefined>();

  protected iconName(): IconName {
    return BANNER_ICONS[this.tone()];
  }
}
