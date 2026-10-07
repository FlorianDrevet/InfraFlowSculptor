import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-ds-select',
  standalone: true,
  templateUrl: './select.html',
  styleUrl: './select.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsSelect {
  readonly ariaLabel = input<string>();
  readonly disabled = input(false);
  readonly required = input(false);
}
