// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

@export()
@description('Cible Azure du déploiement du composant data.')
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
@description('Groupes de ressources créés par le composant data.')
type resourceGroupsConfig = {
  @description('Groupe de ressources principal du composant data.')
  main: resourceGroupConfig
}

@export()
@description('Référence explicite à une ressource d’un autre composant.')
type resourceReferenceConfig = {
  @description('Identifiant de l’abonnement contenant la ressource existante.')
  subscriptionId: string

  @description('Nom du groupe de ressources de la ressource existante.')
  resourceGroupName: string

  @description('Nom Azure de la ressource existante.')
  name: string
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
@description('Paramètres du serveur SQL orders et de sa base.')
type sqlOrdersConfig = {
  @description('Indique si le serveur et sa base sont déployés.')
  deploy: bool

  @description('Nom Azure explicite du serveur SQL.')
  name: string

  @description('Région Azure du serveur SQL.')
  location: string

  @description('Nom du groupe Entra administrateur du serveur SQL.')
  administratorGroupName: string

  @description('Identifiant objet du groupe Entra administrateur SQL pour cette cible.')
  administratorGroupObjectId: string

  @description('Accès réseau public explicite du serveur SQL.')
  publicNetworkAccess: 'Enabled' | 'Disabled'

  @description('Version TLS minimale du serveur SQL.')
  minimalTlsVersion: '1.0' | '1.1' | '1.2' | '1.3'

  @description('Nom Azure explicite de la base SQL orders.')
  databaseName: string

  @description('Nom du SKU de la base SQL.')
  databaseSkuName: string

  @description('Zone de disponibilité SQL, ou -1 si aucune zone n’est imposée.')
  databaseAvailabilityZone: -1 | 1 | 2 | 3

  @description('Tags effectifs du serveur et de la base SQL.')
  tags: object

  @description('Paramètres de diagnostic du serveur SQL.')
  diagnosticSettings: diagnosticSettingConfig[]

  @description('Paramètres de diagnostic de la base SQL.')
  databaseDiagnosticSettings: diagnosticSettingConfig[]
}
