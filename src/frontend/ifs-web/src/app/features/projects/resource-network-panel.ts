import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { PropertyGridRow } from '../../shared/property-grid/property-grid.model';
import { PropertyGrid } from '../../shared/property-grid/property-grid';
import type { PropertyGridUpdate } from '../../shared/property-grid/property-grid-update';

@Component({
  selector: 'app-resource-network-panel',
  imports: [PropertyGrid, TranslocoPipe],
  templateUrl: './resource-network-panel.html',
  styleUrl: './resource-network-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceNetworkPanel {
  readonly rows = input<PropertyGridRow[]>([]);
  readonly disabled = input(false);
  readonly updateValue = output<PropertyGridUpdate>();
  readonly clearOverride = output<{ property: string; targetId: string }>();
}
