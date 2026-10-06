import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-ds-panel',
  standalone: true,
  templateUrl: './panel.html',
  styleUrl: './panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsPanel {
  readonly title = input<string | undefined>();
  readonly subtitle = input<string | undefined>();
  readonly padded = input(true);
  readonly level = input<2 | 3>(2);
}
