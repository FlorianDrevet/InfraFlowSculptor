import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DsIcon, IconName } from '../icon/icon';

export type BadgeTone = 'neutral' | 'signal' | 'ember' | 'success' | 'danger';

@Component({
  selector: 'app-ds-badge',
  standalone: true,
  imports: [DsIcon],
  templateUrl: './badge.html',
  styleUrl: './badge.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsBadge {
  readonly tone = input<BadgeTone>('neutral');
  readonly mono = input(false);
  readonly dot = input(false);
  readonly icon = input<IconName | undefined>();
}
