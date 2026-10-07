import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

@Component({
  selector: 'app-ds-toggle',
  standalone: true,
  templateUrl: './toggle.html',
  styleUrl: './toggle.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsToggle {
  readonly checked = model(false);
  readonly label = input<string | undefined>();
  readonly ariaLabel = input<string | undefined>();

  protected toggle(): void {
    this.checked.update((checked) => !checked);
  }
}
