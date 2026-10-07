import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DsBadge } from '../badge/badge';
import { RESOURCE_TYPES } from '../resource-icon/resource-types';
import { DsResourceIcon } from '../resource-icon/resource-icon';

export type ResourceRowStatusTone = 'neutral' | 'signal' | 'ember' | 'success' | 'danger';

export interface ResourceRowStatus {
  label: string;
  tone?: ResourceRowStatusTone;
}

@Component({
  selector: 'app-ds-resource-row',
  imports: [NgTemplateOutlet, DsBadge, DsResourceIcon],
  standalone: true,
  templateUrl: './resource-row.html',
  styleUrl: './resource-row.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsResourceRow {
  readonly type = input.required<string>();
  readonly name = input.required<string>();
  readonly generatedName = input.required<string>();
  readonly generatedNote = input<string>();
  readonly detail = input<string>();
  readonly typeLabel = input<string>();
  readonly status = input<ResourceRowStatus>();
  readonly href = input<string>();
  readonly indent = input(false);
  protected readonly displayType = computed(
    () =>
      this.typeLabel() ??
      RESOURCE_TYPES[this.type() as keyof typeof RESOURCE_TYPES]?.label ??
      this.type(),
  );
}
