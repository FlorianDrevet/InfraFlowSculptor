// Généré par InfraFlowSculptor — projet shop. Ne pas modifier : les modifications seront signalées puis remplacées à la prochaine publication. Personnalisation : voir README.ifs.md.

@export()
@description('Cible Azure du déploiement du composant platform.')
type targetConfig = {
  @description('Code de la cible partagée, ici shared.')
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
@description('Groupes de ressources créés par le composant platform.')
type resourceGroupsConfig = {
  @description('Groupe de ressources principal du composant platform.')
  main: resourceGroupConfig
}

@export()
@description('Paramètres du registre de conteneurs partagé.')
type acrMainConfig = {
  @description('Indique si le registre est déployé.')
  deploy: bool

  @description('Nom Azure explicite du registre.')
  name: string

  @description('Région Azure du registre.')
  location: string

  @description('SKU du registre de conteneurs.')
  acrSku: 'Basic' | 'Standard' | 'Premium'

  @description('Indique si le compte administrateur du registre est activé.')
  adminUserEnabled: bool

  @description('Accès réseau public explicite du registre.')
  publicNetworkAccess: 'Enabled' | 'Disabled'

  @description('Indique si les tirages anonymes d’image sont activés.')
  anonymousPullEnabled: bool

  @description('Tags effectifs du registre.')
  tags: object

  @description('Paramètres de diagnostic du registre, vide car la cible shared n’est liée à aucun workspace environnemental.')
  diagnosticSettings: object[]
}
