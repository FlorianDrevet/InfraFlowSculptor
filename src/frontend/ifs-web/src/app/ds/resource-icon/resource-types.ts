// Généré depuis docs/design/strata/components/bundle.js — ne pas modifier.
export interface AzureResourceTypeInfo {
  readonly label: string;
  readonly abbr: string;
  readonly category: string;
  readonly file: string | null;
  readonly roadmap?: true;
}

export const RESOURCE_TYPES = {
  KeyVault: { label: 'Key Vault', abbr: 'kv', category: 'security', file: 'key-vault.svg' },
  RedisCache: {
    label: 'Azure Cache for Redis',
    abbr: 'redis',
    category: 'data',
    file: 'redis-cache.svg',
  },
  StorageAccount: {
    label: 'Storage Account',
    abbr: 'st',
    category: 'data',
    file: 'storage-account.svg',
  },
  AppServicePlan: {
    label: 'App Service Plan',
    abbr: 'asp',
    category: 'compute',
    file: 'app-service-plan.svg',
  },
  WebApp: { label: 'Web App', abbr: 'app', category: 'compute', file: 'web-app.svg' },
  FunctionApp: {
    label: 'Function App',
    abbr: 'func',
    category: 'compute',
    file: 'function-app.svg',
  },
  UserAssignedIdentity: {
    label: 'Managed Identity',
    abbr: 'id',
    category: 'security',
    file: 'managed-identity.svg',
  },
  AppConfiguration: {
    label: 'App Configuration',
    abbr: 'appcs',
    category: 'platform',
    file: 'app-configuration.svg',
  },
  ContainerAppEnvironment: {
    label: 'Container Apps Environment',
    abbr: 'cae',
    category: 'compute',
    file: 'container-apps-environment.svg',
  },
  ContainerApp: {
    label: 'Container App',
    abbr: 'ca',
    category: 'compute',
    file: 'container-app.svg',
  },
  LogAnalyticsWorkspace: {
    label: 'Log Analytics Workspace',
    abbr: 'law',
    category: 'observability',
    file: 'log-analytics-workspace.svg',
  },
  ApplicationInsights: {
    label: 'Application Insights',
    abbr: 'appi',
    category: 'observability',
    file: 'application-insights.svg',
  },
  CosmosDb: { label: 'Azure Cosmos DB', abbr: 'cosmos', category: 'data', file: 'cosmos-db.svg' },
  SqlServer: { label: 'SQL Server', abbr: 'sql', category: 'data', file: 'sql-server.svg' },
  SqlDatabase: { label: 'SQL Database', abbr: 'sqldb', category: 'data', file: 'sql-database.svg' },
  ServiceBusNamespace: {
    label: 'Service Bus',
    abbr: 'sbns',
    category: 'messaging',
    file: 'service-bus.svg',
  },
  EventHubNamespace: {
    label: 'Event Hubs',
    abbr: 'evhns',
    category: 'messaging',
    file: 'event-hubs.svg',
  },
  ContainerRegistry: {
    label: 'Container Registry',
    abbr: 'acr',
    category: 'platform',
    file: 'container-registry.svg',
  },
  VirtualNetwork: {
    label: 'Virtual Network',
    abbr: 'vnet',
    category: 'network',
    file: 'virtual-network.svg',
  },
  DocumentIntelligence: {
    label: 'Document Intelligence',
    abbr: 'di',
    category: 'ai',
    file: 'document-intelligence.svg',
  },
  PrivateEndpoint: {
    label: 'Private Endpoint',
    abbr: 'pe',
    category: 'network',
    file: 'private-endpoint.svg',
  },
  ResourceGroup: {
    label: 'Resource Group',
    abbr: 'rg',
    category: 'platform',
    file: 'resource-group.svg',
  },
  Subnet: { label: 'Subnet', abbr: 'snet', category: 'network', file: 'subnet.svg' },
  NetworkSecurityGroup: {
    label: 'Network Security Group',
    abbr: 'nsg',
    category: 'network',
    file: 'network-security-group.svg',
    roadmap: true,
  },
  FrontDoor: {
    label: 'Front Door',
    abbr: 'afd',
    category: 'network',
    file: 'front-door.svg',
    roadmap: true,
  },
} as const satisfies Record<string, AzureResourceTypeInfo>;

export type AzureResourceType = keyof typeof RESOURCE_TYPES;
export type AzureResourceCategory = (typeof RESOURCE_TYPES)[AzureResourceType]['category'];
