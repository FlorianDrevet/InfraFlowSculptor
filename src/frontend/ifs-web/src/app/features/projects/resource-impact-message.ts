import type { TranslocoService } from '@jsverse/transloco';

const impactTranslationKeys: Readonly<Record<string, string>> = {
  'IMPACT-LIA-LINK-IMPLICITE-RETIRE': 'resource.linkImpactImplicitRemoved',
  'IMPACT-LIA-ROLE-REVOKED': 'resource.linkImpactRoleRevoked',
  'IMPACT-LIA-OBLIGATOIRE-ROMPU': 'resource.linkImpactRequiredLinkBroken',
  'IMPACT-LIA-DEPENDANCE-RETIREE': 'resource.linkImpactDependencyRemoved',
  'IMPACT-LIA-DATA-ACCESS-REVOKED': 'resource.linkImpactDataAccessRevoked',
  'IMPACT-LIA-IDENTITE-UTILISEE': 'resource.linkImpactIdentityInUse',
};

export function resourceImpactMessage(
  transloco: TranslocoService,
  code: string,
  fallback: string,
): string {
  const translationKey = impactTranslationKeys[code];
  return translationKey ? transloco.translate(translationKey) : fallback;
}
