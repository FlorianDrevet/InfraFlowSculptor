import { ChangeDetectionStrategy, Component, ViewEncapsulation, input } from '@angular/core';

@Component({
  selector: 'app-ds-table',
  standalone: true,
  templateUrl: './table.html',
  styleUrl: './table.css',
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsTable {
  readonly ariaLabel = input<string>();
}
