import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RESOURCE_TYPES } from './resource-types';

type ResourceMetadata = {
  label: string;
  abbr: string;
  category: string;
  file: string | null;
};

@Component({
  selector: 'app-ds-resource-icon',
  standalone: true,
  templateUrl: './resource-icon.html',
  styleUrl: './resource-icon.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsResourceIcon {
  readonly type = input.required<string>();
  readonly size = input(28);
  readonly variant = input<'icon' | 'tile'>('icon');
  readonly alt = input<string | undefined>();

  protected readonly resource = computed<ResourceMetadata>(() => {
    const type = this.type();
    return (
      RESOURCE_TYPES[type as keyof typeof RESOURCE_TYPES] ?? {
        label: type || 'Ressource',
        abbr: '?',
        category: 'platform',
        file: null,
      }
    );
  });
  protected readonly accessibleName = computed(() => this.alt() ?? this.resource().label);
  protected readonly imageUrl = computed(() => {
    const file = this.resource().file;
    return file ? `/azure-icons/${file}` : null;
  });
  protected readonly tileFontSize = computed(() => Math.max(9, Math.round(this.size() * 0.36)));
}
