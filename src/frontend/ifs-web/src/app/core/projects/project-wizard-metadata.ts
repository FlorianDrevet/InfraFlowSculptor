import type { CatalogRegion } from '../api/generated';

export interface ProjectWizardMetadata {
  tenantId: string;
  catalogVersion: string;
  regions: CatalogRegion[];
}
