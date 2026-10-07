import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DsIcon } from '../icon/icon';

export type GoldenPathState = 'done' | 'attention' | 'error' | 'current' | 'pending';

export interface GoldenPathStep {
  label: string;
  state: GoldenPathState;
  meta?: string;
  hint?: string;
}

const DEFAULT_STEPS: readonly GoldenPathStep[] = [
  { label: 'Modéliser', state: 'pending' },
  { label: 'Générer', state: 'pending' },
  { label: 'Publier', state: 'pending' },
  { label: 'Déployer', state: 'pending' },
];

@Component({
  selector: 'app-ds-golden-path',
  imports: [DsIcon],
  standalone: true,
  templateUrl: './golden-path.html',
  styleUrl: './golden-path.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsGoldenPath {
  readonly steps = input<readonly GoldenPathStep[]>(DEFAULT_STEPS);
  readonly ariaLabel = input('Avancement vers la livraison');
}
