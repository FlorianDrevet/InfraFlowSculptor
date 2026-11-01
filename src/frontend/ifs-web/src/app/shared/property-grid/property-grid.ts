import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  OnChanges,
  output,
  signal,
  SimpleChanges,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import type { JsonElement } from '../../core/api/generated/models/json-element';
import { DsButton } from '../../ds/button/button';
import {
  canClearOverride,
  isPropertyEditable,
  type PropertyGridCell,
  type PropertyGridRow,
} from './property-grid.model';
import { Provenance } from '../provenance/provenance';
import type { PropertyGridUpdate } from './property-grid-update';
import { DsCidrField } from '../../ds/cidr-field/cidr-field';

@Component({
  selector: 'app-property-grid',
  imports: [DsButton, DsCidrField, FormsModule, Provenance, TranslocoPipe],
  templateUrl: './property-grid.html',
  styleUrl: './property-grid.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PropertyGrid implements OnChanges {
  private readonly transloco = inject(TranslocoService);
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly enumValues = signal(new Map<string, JsonElement | null | undefined>());
  private readonly controlValues = signal(new Map<string, JsonElement | null | undefined>());

  readonly rows = input<PropertyGridRow[]>([]);
  readonly syncRevision = input(0);
  readonly disabled = input(false);
  readonly updateValue = output<PropertyGridUpdate>();
  readonly clearOverride = output<{ property: string; targetId: string }>();

  protected readonly canClearOverride = canClearOverride;
  protected readonly isPropertyEditable = isPropertyEditable;

  ngOnChanges(changes: SimpleChanges<PropertyGrid>): void {
    if (!changes.rows && !changes.syncRevision) return;

    const values = new Map<string, JsonElement | null | undefined>();
    const controls = new Map<string, JsonElement | null | undefined>();
    for (const row of this.rows()) {
      const resourceKey = this.enumValueKey(row, null);
      controls.set(resourceKey, row.resource.value);
      values.set(
        resourceKey,
        row.resource.provenance === 'absent' ? undefined : row.resource.value,
      );
      for (const cell of row.targets) {
        const targetKey = this.enumValueKey(row, cell.targetId);
        controls.set(targetKey, cell.value);
        values.set(
          targetKey,
          !cell.applicable || cell.provenance === 'absent' ? undefined : cell.value,
        );
      }
    }
    this.enumValues.set(values);
    this.controlValues.set(controls);
    if (changes.syncRevision) this.restoreNativeControls();
  }

  protected propertyName(row: PropertyGridRow): string {
    const language = this.transloco.getActiveLang() === 'en' ? 'en' : 'fr';
    return row.definition.labels?.[language] ?? row.definition.labels?.fr ?? row.definition.name;
  }

  protected resourceCellLabel(row: PropertyGridRow): string {
    return `${this.propertyName(row)} — ${this.transloco.translate('resource.resourceValue')}`;
  }

  protected targetCellLabel(row: PropertyGridRow, code: string): string {
    return `${this.propertyName(row)} — ${code}`;
  }

  protected gridColumns(): string {
    const targetCount = this.rows()[0]?.targets.length ?? 0;
    return `minmax(12rem, 1.6fr) repeat(${targetCount + 1}, minmax(9rem, 1fr))`;
  }

  protected formatValue(value: JsonElement | null | undefined, provenance?: string | null): string {
    if (value === undefined || (value === null && (!provenance || provenance === 'absent')))
      return this.transloco.translate('resource.valueAbsent');
    if (value === null) return 'null';
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }

  protected formatArrayValue(value: JsonElement | null | undefined): string {
    return JSON.stringify(Array.isArray(value) ? value : []);
  }

  protected formatOption(value: JsonElement): string {
    if (value === null) return 'null';
    return this.formatValue(value);
  }

  protected formatEnumMissingValue(value: JsonElement | null | undefined): string {
    if (value === undefined) return this.transloco.translate('resource.valueAbsent');
    return value === null ? 'null' : this.formatValue(value);
  }

  protected enumOptionKey(value: JsonElement | null | undefined): string {
    return value === undefined ? 'missing' : `value:${JSON.stringify(value) ?? 'null'}`;
  }

  protected enumValue(
    row: PropertyGridRow,
    targetId: string | null,
  ): JsonElement | null | undefined {
    const values = this.enumValues();
    const key = this.enumValueKey(row, targetId);
    if (values.has(key)) return values.get(key);
    if (targetId === null) {
      return row.resource.provenance === 'absent' ? undefined : row.resource.value;
    }
    const cell = row.targets.find((item) => item.targetId === targetId);
    return !cell || !cell.applicable || cell.provenance === 'absent' ? undefined : cell.value;
  }

  protected controlValue(
    row: PropertyGridRow,
    targetId: string | null,
  ): JsonElement | null | undefined {
    const values = this.controlValues();
    const key = this.enumValueKey(row, targetId);
    if (values.has(key)) return values.get(key);
    if (targetId === null) return row.resource.value;
    return row.targets.find((cell) => cell.targetId === targetId)?.value;
  }

  protected controlKey(row: PropertyGridRow, targetId: string | null): string {
    return this.enumValueKey(row, targetId);
  }

  protected controlInputValue(row: PropertyGridRow, targetId: string | null): string {
    const value = this.controlValue(row, targetId);
    return value === null || value === undefined ? '' : this.formatValue(value);
  }

  protected isEnumValueMissing(
    values: JsonElement[] | undefined,
    value: JsonElement | null | undefined,
  ): boolean {
    const selectedValue = this.enumOptionKey(value);
    return !values?.some((option) => this.enumOptionKey(option) === selectedValue);
  }

  protected isBoolean(value: JsonElement | null): boolean {
    return typeof value === 'boolean';
  }

  protected isBooleanDefinition(kind: string): boolean {
    return kind === 'boolean';
  }

  protected isNumericDefinition(kind: string): boolean {
    return kind === 'integer' || kind === 'number';
  }

  protected isCidrList(row: PropertyGridRow): boolean {
    return row.definition.kind === 'array' && row.definition.itemsFormat === 'ipv4Cidr';
  }

  protected stringArray(value: JsonElement | null | undefined): string[] {
    return Array.isArray(value)
      ? value.filter((item): item is string => typeof item === 'string')
      : [];
  }

  protected cidrFieldId(row: PropertyGridRow, targetId: string | null): string {
    return `property-${row.definition.name}-${targetId ?? 'resource'}`;
  }

  protected updateCidrValue(row: PropertyGridRow, targetId: string | null, value: string[]): void {
    this.rememberControlValue(row, targetId, value);
    this.updateValue.emit({ property: row.definition.name, targetId, value });
  }

  protected onValueChange(event: Event, row: PropertyGridRow, targetId: string | null): void {
    if (this.disabled()) return;
    const control = event.target;
    if (control instanceof HTMLInputElement) {
      if (row.definition.kind === 'boolean') {
        this.rememberControlValue(row, targetId, control.checked);
        this.updateValue.emit({ property: row.definition.name, targetId, value: control.checked });
        return;
      }
      if (this.isNumericDefinition(row.definition.kind)) {
        if (control.value !== '' && Number.isFinite(control.valueAsNumber)) {
          this.rememberControlValue(row, targetId, control.valueAsNumber);
          this.updateValue.emit({
            property: row.definition.name,
            targetId,
            value: control.valueAsNumber,
          });
        }
        return;
      }
      this.rememberControlValue(row, targetId, control.value);
      this.updateValue.emit({ property: row.definition.name, targetId, value: control.value });
      return;
    }
    if (control instanceof HTMLSelectElement) {
      const option = row.definition.values?.find(
        (value) => this.enumOptionKey(value) === control.value,
      );
      if (option !== undefined) {
        this.enumValues.update((values) =>
          new Map(values).set(this.enumValueKey(row, targetId), option),
        );
        this.rememberControlValue(row, targetId, option);
        this.updateValue.emit({ property: row.definition.name, targetId, value: option });
      }
      return;
    }
    if (control instanceof HTMLTextAreaElement && row.definition.kind === 'array') {
      try {
        const value: unknown = JSON.parse(control.value);
        if (!Array.isArray(value)) throw new TypeError('Expected an array.');
        control.setCustomValidity('');
        this.rememberControlValue(row, targetId, value as JsonElement[]);
        this.updateValue.emit({
          property: row.definition.name,
          targetId,
          value: value as JsonElement[],
        });
      } catch {
        control.setCustomValidity(this.transloco.translate('resource.arrayInvalid'));
        control.reportValidity();
      }
    }
  }

  protected clear(row: PropertyGridRow, cell: PropertyGridCell): void {
    if (this.disabled()) return;
    this.clearOverride.emit({ property: row.definition.name, targetId: cell.targetId });
  }

  private enumValueKey(row: PropertyGridRow, targetId: string | null): string {
    return JSON.stringify([row.definition.name, targetId]);
  }

  private rememberControlValue(
    row: PropertyGridRow,
    targetId: string | null,
    value: JsonElement,
  ): void {
    this.controlValues.update((values) =>
      new Map(values).set(this.enumValueKey(row, targetId), value),
    );
  }

  private restoreNativeControls(): void {
    const controls = this.elementRef.nativeElement.querySelectorAll<HTMLElement>(
      '[data-property-grid-control]',
    );
    for (const control of controls) {
      const key = control.getAttribute('data-property-grid-control');
      if (key === null) continue;
      if (control instanceof HTMLInputElement) {
        if (control.type === 'checkbox') {
          control.checked = this.controlValues().get(key) === true;
        } else {
          const value = this.controlValues().get(key);
          control.value = value === null || value === undefined ? '' : this.formatValue(value);
        }
      } else if (control instanceof HTMLSelectElement) {
        control.value = this.enumOptionKey(this.enumValues().get(key));
      } else if (control instanceof HTMLTextAreaElement) {
        control.value = this.formatArrayValue(this.controlValues().get(key));
      }
    }
  }
}
