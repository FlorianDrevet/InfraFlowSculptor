import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
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
export class PropertyGrid {
  private readonly transloco = inject(TranslocoService);

  readonly rows = input<PropertyGridRow[]>([]);
  readonly disabled = input(false);
  readonly updateValue = output<PropertyGridUpdate>();
  readonly clearOverride = output<{ property: string; targetId: string }>();

  protected readonly canClearOverride = canClearOverride;
  protected readonly isPropertyEditable = isPropertyEditable;

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

  protected formatValue(value: JsonElement | null): string {
    if (value === null) return this.transloco.translate('resource.valueAbsent');
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }

  protected formatArrayValue(value: JsonElement | null): string {
    return JSON.stringify(Array.isArray(value) ? value : []);
  }

  protected formatOption(value: JsonElement): string {
    return this.formatValue(value);
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

  protected stringArray(value: JsonElement | null): string[] {
    return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
  }

  protected cidrFieldId(row: PropertyGridRow, targetId: string | null): string {
    return `property-${row.definition.name}-${targetId ?? 'resource'}`;
  }

  protected updateCidrValue(row: PropertyGridRow, targetId: string | null, value: string[]): void {
    this.updateValue.emit({ property: row.definition.name, targetId, value });
  }

  protected onValueChange(event: Event, row: PropertyGridRow, targetId: string | null): void {
    if (this.disabled()) return;
    const control = event.target;
    if (control instanceof HTMLInputElement) {
      if (row.definition.kind === 'boolean') {
        this.updateValue.emit({ property: row.definition.name, targetId, value: control.checked });
        return;
      }
      if (this.isNumericDefinition(row.definition.kind)) {
        if (control.value !== '' && Number.isFinite(control.valueAsNumber)) {
          this.updateValue.emit({
            property: row.definition.name,
            targetId,
            value: control.valueAsNumber,
          });
        }
        return;
      }
      this.updateValue.emit({ property: row.definition.name, targetId, value: control.value });
      return;
    }
    if (control instanceof HTMLSelectElement) {
      const option = row.definition.values?.find(
        (value) => this.formatOption(value) === control.value,
      );
      if (option !== undefined) {
        this.updateValue.emit({ property: row.definition.name, targetId, value: option });
      }
      return;
    }
    if (control instanceof HTMLTextAreaElement && row.definition.kind === 'array') {
      try {
        const value: unknown = JSON.parse(control.value);
        if (!Array.isArray(value)) throw new TypeError('Expected an array.');
        control.setCustomValidity('');
        this.updateValue.emit({ property: row.definition.name, targetId, value: value as JsonElement[] });
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
}
