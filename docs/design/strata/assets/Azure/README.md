# Icônes Azure

Les 25 icônes officielles Microsoft de types de ressources Azure, tirées du pack **Azure Public Service Icons V24** (Azure Architecture Center), fichiers SVG non modifiés, seulement renommés.

| Fichier | Type IFS | Service |
| --- | --- | --- |
| key-vault.svg | KeyVault | Key Vault |
| redis-cache.svg | RedisCache | Azure Cache for Redis (pas Azure Managed Redis, le type du catalogue v1) |
| storage-account.svg | StorageAccount | Storage Account |
| app-service-plan.svg | AppServicePlan | App Service Plan |
| web-app.svg | WebApp | Web App (App Services) |
| function-app.svg | FunctionApp | Function App |
| managed-identity.svg | UserAssignedIdentity | Managed Identity |
| app-configuration.svg | AppConfiguration | App Configuration |
| container-apps-environment.svg | ContainerAppEnvironment | Container Apps Environment |
| container-app.svg | ContainerApp | Container App |
| log-analytics-workspace.svg | LogAnalyticsWorkspace | Log Analytics Workspace |
| application-insights.svg | ApplicationInsights | Application Insights |
| cosmos-db.svg | CosmosDb | Azure Cosmos DB |
| sql-server.svg | SqlServer | SQL Server |
| sql-database.svg | SqlDatabase | SQL Database |
| service-bus.svg | ServiceBusNamespace | Service Bus |
| event-hubs.svg | EventHubNamespace | Event Hubs |
| container-registry.svg | ContainerRegistry | Container Registry |
| virtual-network.svg | VirtualNetwork | Virtual Network |
| document-intelligence.svg | DocumentIntelligence | Document Intelligence (ex-Form Recognizer) |
| private-endpoint.svg | PrivateEndpoint | Private Endpoint |
| resource-group.svg | ResourceGroup | Resource Group |
| subnet.svg | Subnet | Subnet |
| network-security-group.svg | NetworkSecurityGroup | Network Security Group (roadmap) |
| front-door.svg | FrontDoor | Front Door and CDN Profiles (roadmap) |

Les icônes sont en couleur, conçues par Microsoft : elles ne prennent pas la couleur du texte et ne doivent pas être recolorées.

**Conditions Microsoft** : usage permis dans des diagrammes d'architecture, supports de formation et documentation. Pas de recadrage, rotation, retournement ni déformation ; nom du service écrit près de l'icône ; jamais pour représenter un autre produit que le service Microsoft concerné. L'usage dans l'interface d'un produit commercial n'est pas explicitement couvert : à faire valider avant la mise en vente.

Dans le code, préférer le composant `ResourceIcon`, qui embarque ces mêmes fichiers.

**Catalogue v1.** Plusieurs types du catalogue v1 n'ont pas encore leur icône ici : Azure Managed Redis, PostgreSQL serveur flexible, Static Web App, Microsoft Foundry (compte, projet), AI Search, passerelle NAT, IP publique, zones DNS, table de routage, pool d'exécuteurs. Ils utilisent la tuile d'abréviation de `ResourceIcon` (`variant="tile"`) jusqu'à l'ajout de leur icône officielle, tirée du même pack sans modification.
