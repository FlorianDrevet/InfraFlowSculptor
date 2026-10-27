import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

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
  readonly valueChange = output<string>();

  protected emitValueChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.valueChange.emit(target.value);
    }
  }
}
