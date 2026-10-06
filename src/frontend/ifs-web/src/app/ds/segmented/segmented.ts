import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { DsIcon, IconName } from '../icon/icon';

export interface SegmentedOption {
  value: string;
  label: string;
  icon?: IconName;
}

@Component({
  selector: 'app-ds-segmented',
  standalone: true,
  imports: [DsIcon],
  templateUrl: './segmented.html',
  styleUrl: './segmented.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsSegmented {
  readonly options = input<SegmentedOption[]>([]);
  readonly value = model<string | undefined>(undefined);
  readonly mono = input(false);
  readonly ariaLabel = input<string | undefined>();

  protected selectedValue(): string | undefined {
    return this.value() ?? this.options()[0]?.value;
  }

  protected select(value: string): void {
    this.value.set(value);
  }
}
