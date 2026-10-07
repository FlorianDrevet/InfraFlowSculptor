import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DsBadge } from '../../../ds/badge/badge';
import { DsBanner } from '../../../ds/banner/banner';
import { DsButton } from '../../../ds/button/button';
import { DsIcon, ICON_PATHS } from '../../../ds/icon/icon';
import { DsDialog } from '../../../ds/dialog/dialog';
import { DsGeneratedName } from '../../../ds/generated-name/generated-name';
import { DsGoldenPath } from '../../../ds/golden-path/golden-path';
import { DsPanel } from '../../../ds/panel/panel';
import { DsResourceIcon } from '../../../ds/resource-icon/resource-icon';
import { RESOURCE_TYPES, type AzureResourceType } from '../../../ds/resource-icon/resource-types';
import { DsResourceRow } from '../../../ds/resource-row/resource-row';
import { DsSelect } from '../../../ds/select/select';
import { DsSegmented } from '../../../ds/segmented/segmented';
import { DsTable } from '../../../ds/table/table';
import { DsTabs } from '../../../ds/tabs/tabs';
import { DsTextField } from '../../../ds/text-field/text-field';
import { DsToggle } from '../../../ds/toggle/toggle';
import {
  COLOR_TOKENS,
  RADIUS_TOKENS,
  SPACING_TOKENS,
  TYPE_GROUPS,
} from '../../../core/theme/foundations.generated';

@Component({
  selector: 'app-design-system',
  imports: [
    DsBadge,
    DsBanner,
    DsButton,
    DsIcon,
    DsDialog,
    DsGeneratedName,
    DsGoldenPath,
    DsPanel,
    DsResourceIcon,
    DsResourceRow,
    DsSelect,
    DsSegmented,
    DsTable,
    DsTabs,
    DsTextField,
    DsToggle,
  ],
  templateUrl: './design-system.html',
  styleUrls: ['./design-system.css', './design-system-components.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignSystem {
  protected readonly colors = COLOR_TOKENS;
  protected readonly typography = TYPE_GROUPS;
  protected readonly spacing = SPACING_TOKENS;
  protected readonly radii = RADIUS_TOKENS;
  protected readonly iconNames = Object.keys(ICON_PATHS) as (keyof typeof ICON_PATHS)[];
  protected readonly resourceTypes = Object.keys(RESOURCE_TYPES) as AzureResourceType[];
  protected readonly deliverySteps = [
    {
      label: 'Modéliser',
      state: 'done' as const,
      meta: '5 composants · 31 ressources',
      hint: '2 avertissements',
    },
    {
      label: 'Générer',
      state: 'attention' as const,
      meta: 'Révision 15 · il y a 2 h',
      hint: 'Périmée : le modèle a changé',
    },
    { label: 'Publier', state: 'done' as const, meta: '2 pull requests fusionnées sur 2' },
    {
      label: 'Déployer',
      state: 'error' as const,
      meta: 'dev à jour',
      hint: 'prd : partiellement appliquée',
    },
  ];
  protected readonly changeSteps = [
    { label: 'Valider', state: 'done' as const, meta: '0 erreur · 2 avertissements' },
    { label: 'Générer', state: 'done' as const, meta: 'Révision 16 · 23 fichiers' },
    { label: 'Relire', state: 'current' as const, meta: '4 fichiers modifiés' },
    { label: 'Publier', state: 'pending' as const, meta: 'Une pull request par dépôt' },
  ];

  protected readonly familyVariable = (family: string): string => `var(--ifs-font-${family})`;
  protected readonly tokenVariable = (category: string, name: string): string =>
    `var(--ifs-${category}-${name})`;
  protected readonly resourceLabel = (type: AzureResourceType): string =>
    RESOURCE_TYPES[type].label;
}
