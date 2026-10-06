import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DsIcon } from '../icon/icon';

@Component({
  selector: 'app-ds-generated-name',
  imports: [DsIcon],
  standalone: true,
  templateUrl: './generated-name.html',
  styleUrl: './generated-name.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsGeneratedName {
  readonly value = input.required<string>();
  readonly env = input<string>();
  readonly available = input<boolean | null>();
}
