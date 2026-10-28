// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

@export()
@description('Cible Azure du déploiement du composant core.')
type targetConfig = {
  @description('Code de la cible, par exemple dev ou prd.')
  code: string

  @description('Identifiant de l’abonnement Azure cible.')
  subscriptionId: string

  @description('Région Azure des ressources de cette cible.')
  location: string
}

@export()
@description('Configuration du groupe de ressources du composant.')
type resourceGroupConfig = {
  @description('Nom Azure explicite du groupe de ressources.')
  name: string

  @description('Région Azure du groupe de ressources.')
  location: string

  @description('Tags effectifs du groupe de ressources.')
  tags: object
}

@export()
@description('Groupes de ressources créés par le composant core.')
type resourceGroupsConfig = {
  @description('Groupe de ressources principal du composant core.')
  main: resourceGroupConfig
}

@export()
@description('Paramètres explicites d’une destination de diagnostic vers Log Analytics.')
type diagnosticSettingConfig = {
  @description('Nom explicite du paramètre de diagnostic.')
  name: string

  @description('Groupe de catégories de journal envoyé à Log Analytics.')
  logCategoryGroup: string

  @description('Catégorie de métriques envoyée à Log Analytics.')
  metricCategory: string

  @description('Format de destination Log Analytics.')
  logAnalyticsDestinationType: 'Dedicated' | 'AzureDiagnostics'
}

@export()
@description('Paramètres du workspace Log Analytics principal.')
type logMainConfig = {
  @description('Indique si le workspace est déployé.')
  deploy: bool

  @description('Nom Azure explicite du workspace.')
  name: string

  @description('Région Azure du workspace.')
  location: string

  @description('Rétention des journaux en jours.')
  dataRetentionDays: int

  @description('Quota quotidien en gigaoctets, ou -1 sans quota.')
  dailyQuotaGb: string

  @description('Nom du SKU du workspace.')
  skuName: 'PerGB2018'

  @description('Accès réseau explicite pour l’ingestion.')
  publicNetworkAccessForIngestion: 'Enabled' | 'Disabled'

  @description('Accès réseau explicite pour les requêtes.')
  publicNetworkAccessForQuery: 'Enabled' | 'Disabled'

  @description('Tags effectifs du workspace.')
  tags: object

  @description('Paramètres de diagnostic du workspace, vide si aucun n’est requis.')
  diagnosticSettings: diagnosticSettingConfig[]
}

@export()
@description('Paramètres du composant Application Insights principal.')
type appiMainConfig = {
  @description('Indique si Application Insights est déployé.')
  deploy: bool

  @description('Nom Azure explicite du composant Application Insights.')
  name: string

  @description('Région Azure du composant Application Insights.')
  location: string

  @description('Type d’application Application Insights.')
  applicationType: 'web' | 'other'

  @description('Pourcentage d’échantillonnage des données de télémétrie.')
  samplingPercentage: int

  @description('Durée de rétention des données en jours.')
  retentionInDays: 30 | 60 | 90 | 120 | 180 | 270 | 365 | 550 | 730

  @description('Indique si le masquage des adresses IP est désactivé.')
  disableIpMasking: bool

  @description('Indique si l’authentification locale est désactivée.')
  disableLocalAuth: bool

  @description('Accès réseau explicite pour l’ingestion.')
  publicNetworkAccessForIngestion: 'Enabled' | 'Disabled'

  @description('Accès réseau explicite pour les requêtes.')
  publicNetworkAccessForQuery: 'Enabled' | 'Disabled'

  @description('Tags effectifs du composant Application Insights.')
  tags: object

  @description('Paramètres de diagnostic du composant Application Insights.')
  diagnosticSettings: diagnosticSettingConfig[]
}

@export()
@description('Paramètres du Key Vault principal.')
type kvMainConfig = {
  @description('Indique si le Key Vault est déployé.')
  deploy: bool

  @description('Nom Azure explicite du Key Vault.')
  name: string

  @description('Région Azure du Key Vault.')
  location: string

  @description('Indique si la protection contre la purge est activée.')
  enablePurgeProtection: bool

  @description('Indique si l’autorisation basée sur RBAC est activée.')
  enableRbacAuthorization: bool

  @description('Indique si la suppression réversible est activée.')
  enableSoftDelete: bool

  @description('Durée de rétention de la suppression réversible en jours.')
  softDeleteRetentionInDays: int

  @description('SKU explicite du Key Vault.')
  sku: 'standard' | 'premium'

  @description('Accès réseau public explicite du Key Vault.')
  publicNetworkAccess: 'Enabled' | 'Disabled'

  @description('Tags effectifs du Key Vault.')
  tags: object

  @description('Paramètres de diagnostic du Key Vault.')
  diagnosticSettings: diagnosticSettingConfig[]
}
