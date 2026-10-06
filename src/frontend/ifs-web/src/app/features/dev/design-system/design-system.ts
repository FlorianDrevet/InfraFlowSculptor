import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DsBadge } from '../../../ds/badge/badge';
import { DsBanner } from '../../../ds/banner/banner';
import { DsButton } from '../../../ds/button/button';
import { DsIcon, ICON_PATHS } from '../../../ds/icon/icon';
import { DsPanel } from '../../../ds/panel/panel';
import { DsSegmented } from '../../../ds/segmented/segmented';
import { DsTabs } from '../../../ds/tabs/tabs';
import { DsTextField } from '../../../ds/text-field/text-field';
import { DsToggle } from '../../../ds/toggle/toggle';
import {
  COLOR_TOKENS,
  RADIUS_TOKENS,
  SPACING_TOKENS,
  TYPE_GROUPS,
} from '../../../core/theme/foundations.generated';

@Component({
  selector: 'app-design-system',
  imports: [
    DsBadge,
    DsBanner,
    DsButton,
    DsIcon,
    DsPanel,
    DsSegmented,
    DsTabs,
    DsTextField,
    DsToggle,
  ],
  templateUrl: './design-system.html',
  styleUrls: ['./design-system.css', './design-system-components.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignSystem {
  protected readonly colors = COLOR_TOKENS;
  protected readonly typography = TYPE_GROUPS;
  protected readonly spacing = SPACING_TOKENS;
  protected readonly radii = RADIUS_TOKENS;
  protected readonly iconNames = Object.keys(ICON_PATHS) as (keyof typeof ICON_PATHS)[];

  protected readonly familyVariable = (family: string): string => `var(--ifs-font-${family})`;
  protected readonly tokenVariable = (category: string, name: string): string =>
    `var(--ifs-${category}-${name})`;
}
