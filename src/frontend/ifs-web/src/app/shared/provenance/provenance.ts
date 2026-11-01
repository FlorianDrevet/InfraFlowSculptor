import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-provenance',
  imports: [TranslocoPipe],
  templateUrl: './provenance.html',
  styleUrl: './provenance.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Provenance {
  readonly value = input.required<string>();
  protected readonly labelKey = computed(() => {
    switch (this.value()) {
      case 'override': return 'resource.provenance.override';
      case 'resource': return 'resource.provenance.resource';
      case 'catalogDefault': return 'resource.provenance.catalogDefault';
      default: return 'resource.provenance.absent';
    }
  });
}
