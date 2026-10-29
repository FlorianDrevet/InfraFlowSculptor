import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { DsButton } from '../button/button';
import { DsTextField } from '../text-field/text-field';

let nextFieldId = 0;

@Component({
  selector: 'app-ds-cidr-field',
  standalone: true,
  imports: [DsButton, DsTextField],
  templateUrl: './cidr-field.html',
  styleUrl: './cidr-field.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DsCidrField), multi: true },
  ],
})
export class DsCidrField implements ControlValueAccessor {
  readonly label = input.required<string>();
  readonly inputLabel = input.required<string>();
  readonly addLabel = input.required<string>();
  readonly removeLabel = input.required<string>();
  readonly invalidMessage = input.required<string>();
  readonly id = input<string | undefined>();
  readonly maxItems = input(100);
  readonly disabled = input(false);

  protected readonly value = signal<string[]>([]);
  protected readonly draft = signal('');
  protected readonly controlDisabled = signal(false);
  protected readonly fieldId = computed(() => this.id() || `st-cidr-field-${this.generatedId}`);
  protected readonly draftError = computed(() => {
    const draft = this.draft();
    return draft.length > 0 && !isCanonicalIpv4Cidr(draft) ? this.invalidMessage() : undefined;
  });
  protected readonly canAdd = computed(() =>
    this.draft().length > 0
    && !this.draftError()
    && this.value().length < this.maxItems()
    && !this.isDisabled(),
  );

  private readonly generatedId = ++nextFieldId;
  private onChange: (value: string[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: unknown): void {
    this.value.set(Array.isArray(value) && value.every((item: unknown) => typeof item === 'string')
      ? [...value]
      : []);
  }

  registerOnChange(fn: (value: string[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.controlDisabled.set(disabled);
  }

  protected itemId(index: number): string {
    return `${this.fieldId()}-range-${index + 1}`;
  }

  protected draftId(): string {
    return `${this.fieldId()}-draft`;
  }

  protected itemError(value: string): string | undefined {
    return isCanonicalIpv4Cidr(value) ? undefined : this.invalidMessage();
  }

  protected onDraftChange(value: string | undefined): void {
    this.draft.set(value ?? '');
  }

  protected onItemChange(index: number, value: string | undefined): void {
    const nextValue = [...this.value()];
    nextValue[index] = value ?? '';
    this.setValue(nextValue);
  }

  protected addRange(): void {
    if (!this.canAdd()) {
      return;
    }

    const nextValue = [...this.value(), this.draft()];
    this.draft.set('');
    this.setValue(nextValue);
  }

  protected removeRange(index: number): void {
    this.setValue(this.value().filter((_value, currentIndex) => currentIndex !== index));
  }

  private isDisabled(): boolean {
    return this.disabled() || this.controlDisabled();
  }

  private setValue(value: string[]): void {
    this.value.set(value);
    this.onChange([...value]);
    this.onTouched();
  }
}

function isCanonicalIpv4Cidr(value: string): boolean {
  const parts = value.split('/');
  const prefixMatch = parts.length === 2 ? /^(0|[1-9]\d*)$/.exec(parts[1]) : null;
  if (parts.length !== 2 || !prefixMatch || prefixMatch[0] !== parts[1] || parts[1].length > 2) {
    return false;
  }

  const prefix = Number(parts[1]);
  if (prefix > 32) {
    return false;
  }

  const octetTexts = parts[0].split('.');
  if (octetTexts.length !== 4 || !octetTexts.every((octet) => {
    const octetMatch = /^(0|[1-9]\d{0,2})$/.exec(octet);
    return octetMatch !== null && octetMatch[0] === octet;
  })) {
    return false;
  }

  const octets = octetTexts.map(Number);
  if (octets.some((octet) => octet > 255)) {
    return false;
  }

  const address = octets.reduce((current, octet) => current * 256 + octet, 0);
  const hostBits = 32 - prefix;
  return hostBits === 32 ? address === 0 : address % 2 ** hostBits === 0;
}
