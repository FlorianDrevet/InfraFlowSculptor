import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { GeneratedNameRow } from './generated-name-row';

@Component({
  selector: 'app-generated-names',
  imports: [TranslocoPipe],
  templateUrl: './generated-names.html',
  styleUrl: './generated-names.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GeneratedNames {
  readonly items = input<GeneratedNameRow[]>([]);
  readonly loading = input(false);
  readonly error = input(false);
}
