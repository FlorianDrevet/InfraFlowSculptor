import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  model,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

let nextFieldId = 0;

@Component({
  selector: 'app-ds-text-field',
  standalone: true,
  templateUrl: './text-field.html',
  styleUrl: './text-field.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DsTextField), multi: true },
  ],
})
export class DsTextField implements ControlValueAccessor {
  readonly label = input<string | undefined>();
  readonly hint = input<string | undefined>();
  readonly error = input<string | undefined>();
  readonly mono = input(false);
  readonly changed = input(false);
  readonly id = input<string | undefined>();
  readonly value = model<string | undefined>(undefined);
  readonly defaultValue = input<string | number | undefined>();
  readonly type = input('text');
  readonly placeholder = input<string | undefined>();
  readonly name = input<string | undefined>();
  readonly required = input(false);
  readonly readonly = input(false);
  readonly disabled = input(false);

  protected readonly controlDisabled = signal(false);
  protected readonly fieldId = computed(() => this.id() || `st-field-${this.generatedId}`);
  protected readonly displayValue = computed(
    () => this.value() ?? (this.defaultValue() === undefined ? '' : String(this.defaultValue())),
  );
  protected readonly messageId = computed(() =>
    this.error() || this.hint() ? `${this.fieldId()}-message` : null,
  );
  protected readonly describedBy = computed(() => this.messageId());

  private readonly generatedId = ++nextFieldId;
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: unknown): void {
    if (value === null || value === undefined) {
      this.value.set('');
    } else if (typeof value === 'string' || typeof value === 'number') {
      this.value.set(String(value));
    } else {
      this.value.set('');
    }
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.controlDisabled.set(disabled);
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.value.set(value);
    this.onChange(value);
  }
}
