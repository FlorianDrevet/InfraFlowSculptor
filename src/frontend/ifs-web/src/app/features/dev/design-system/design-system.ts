import { ChangeDetectionStrategy, Component } from '@angular/core';
import {
  COLOR_TOKENS,
  RADIUS_TOKENS,
  SPACING_TOKENS,
  TYPE_GROUPS,
} from '../../../core/theme/foundations.generated';

@Component({
  selector: 'app-design-system',
  templateUrl: './design-system.html',
  styleUrl: './design-system.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignSystem {
  protected readonly colors = COLOR_TOKENS;
  protected readonly typography = TYPE_GROUPS;
  protected readonly spacing = SPACING_TOKENS;
  protected readonly radii = RADIUS_TOKENS;

  protected readonly familyVariable = (family: string): string => `var(--ifs-font-${family})`;
  protected readonly tokenVariable = (category: string, name: string): string => `var(--ifs-${category}-${name})`;
}
