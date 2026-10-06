// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

@export()
@description('Cible Azure du déploiement du composant orders.')
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
@description('Groupes de ressources créés par le composant orders.')
type resourceGroupsConfig = {
  @description('Groupe de ressources principal du composant orders.')
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
@description('Référence explicite à une base SQL et à son serveur parent.')
type sqlDatabaseReferenceConfig = {
  @description('Référence du serveur SQL qui contient la base.')
  server: resourceReferenceConfig

  @description('Nom Azure de la base SQL existante.')
  name: string
}

@export()
@description('Références aux ressources existantes nécessaires à orders.')
type externalResourcesConfig = {
  @description('Workspace Log Analytics du composant core.')
  coreWorkspace: resourceReferenceConfig

  @description('Composant Application Insights du composant core.')
  coreAppInsights: resourceReferenceConfig

  @description('Key Vault du composant core.')
  coreKeyVault: resourceReferenceConfig

  @description('Base SQL du composant data.')
  dataSqlDatabase: sqlDatabaseReferenceConfig

  @description('Registre de conteneurs du composant platform.')
  platformRegistry: resourceReferenceConfig
}

@export()
@description('Paramètres explicites d’une destination de diagnostic complète.')
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
@description('Paramètres explicites d’une destination de diagnostic métrique.')
type metricDiagnosticSettingConfig = {
  @description('Nom explicite du paramètre de diagnostic.')
  name: string

  @description('Catégorie de métriques envoyée à Log Analytics.')
  metricCategory: string

  @description('Format de destination Log Analytics.')
  logAnalyticsDestinationType: 'Dedicated' | 'AzureDiagnostics'
}

@export()
@description('Paramètres de l’identité affectée par l’utilisateur de l’API.')
type apiIdentityConfig = {
  @description('Indique si l’identité affectée par l’utilisateur est déployée.')
  deploy: bool

  @description('Nom Azure explicite de l’identité affectée par l’utilisateur.')
  name: string

  @description('Région Azure de l’identité.')
  location: string

  @description('Tags effectifs de l’identité.')
  tags: object
}

@export()
@description('Paramètres de l’environnement Azure Container Apps.')
type caeMainConfig = {
  @description('Indique si l’environnement Azure Container Apps est déployé.')
  deploy: bool

  @description('Nom Azure explicite de l’environnement.')
  name: string

  @description('Région Azure de l’environnement.')
  location: string

  @description('Indique si l’environnement est interne au réseau virtuel.')
  internal: bool

  @description('Indique si les ressources de l’environnement sont redondantes entre zones.')
  zoneRedundant: bool

  @description('Accès réseau public explicite de l’environnement.')
  publicNetworkAccess: 'Enabled' | 'Disabled'

  @description('Profils de charge de travail explicites de l’environnement.')
  workloadProfiles: object[]

  @description('Tags effectifs de l’environnement.')
  tags: object

  @description('Paramètres de diagnostic de l’environnement.')
  diagnosticSettings: diagnosticSettingConfig[]
}

@export()
@description('Paramètres de l’application Container App api.')
type caApiConfig = {
  @description('Indique si l’application Container App est déployée.')
  deploy: bool

  @description('Nom Azure explicite de la Container App.')
  name: string

  @description('Région Azure de la Container App.')
  location: string

  @description('Image complète à déployer, vide pour utiliser l’image de démarrage épinglée.')
  image: string

  @description('Nombre de CPU de chaque réplique, représenté en décimal.')
  cpu: string

  @description('Mémoire de chaque réplique au format Container Apps.')
  memory: string

  @description('Nombre minimal de répliques.')
  minReplicas: int

  @description('Nombre maximal de répliques.')
  maxReplicas: int

  @description('Indique si l’ingress est externe.')
  ingressExternal: bool

  @description('Port cible de l’ingress et du conteneur API.')
  ingressTargetPort: int

  @description('Indique si l’ingress accepte les connexions HTTP non chiffrées.')
  ingressAllowInsecure: bool

  @description('Nom du secret Key Vault référencé par la variable Payments__ApiKey.')
  paymentsSecretName: string

  @description('Tags effectifs de la Container App.')
  tags: object

  @description('Paramètres de diagnostic métrique de la Container App.')
  diagnosticSettings: metricDiagnosticSettingConfig[]
}
