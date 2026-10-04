import type * as React from 'react';

/** Noms des icônes d'interface (trait 1.75 px, grille 24). */
export type IconName =
  | 'home' | 'folder' | 'layers' | 'zap' | 'users' | 'sliders' | 'search' | 'plus' | 'minus' | 'check' | 'x'
  | 'alert' | 'chevron-right' | 'chevron-down' | 'lock' | 'globe' | 'key' | 'list' | 'graph' | 'file'
  | 'download' | 'upload' | 'clock' | 'branch' | 'refresh' | 'grid' | 'fit' | 'star' | 'copy' | 'trash' | 'external-link';

/** Types de ressources : mêmes valeurs que `ResourceTypeEnum` côté IFS, plus les types réseau annexes. */
export type ResourceType =
  | 'KeyVault' | 'RedisCache' | 'StorageAccount' | 'AppServicePlan' | 'WebApp' | 'FunctionApp' | 'UserAssignedIdentity'
  | 'AppConfiguration' | 'ContainerAppEnvironment' | 'ContainerApp' | 'LogAnalyticsWorkspace' | 'ApplicationInsights'
  | 'CosmosDb' | 'SqlServer' | 'SqlDatabase' | 'ServiceBusNamespace' | 'EventHubNamespace' | 'ContainerRegistry'
  | 'VirtualNetwork' | 'DocumentIntelligence' | 'PrivateEndpoint' | 'ResourceGroup' | 'Subnet'
  | 'NetworkSecurityGroup' | 'FrontDoor';

export type ResourceCategory = 'compute' | 'data' | 'messaging' | 'security' | 'observability' | 'network' | 'platform' | 'ai';
export type Tone = 'neutral' | 'signal' | 'ember' | 'success' | 'danger';

export interface ButtonProps extends Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, 'type'> {
  /** primary : l'action principale de l'écran (une seule). danger : action destructrice. */
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'md' | 'sm';
  icon?: IconName;
  iconPosition?: 'start' | 'end';
  /** Rend un lien `<a>` stylé en bouton. */
  href?: string;
  type?: 'button' | 'submit' | 'reset';
}
export declare function Button(props: ButtonProps): React.ReactElement;

export interface IconProps { name: IconName; size?: number; strokeWidth?: number; /** Rend l'icône annoncée par les lecteurs d'écran. */ label?: string; className?: string; style?: React.CSSProperties }
export declare function Icon(props: IconProps): React.ReactElement;

export interface BadgeProps { tone?: Tone; /** Police mono : environnements, identifiants. */ mono?: boolean; /** Pastille de couleur avant le texte. */ dot?: boolean; icon?: IconName; children?: React.ReactNode; className?: string }
export declare function Badge(props: BadgeProps): React.ReactElement;

export interface TextFieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string; hint?: string; error?: string;
  /** Police mono pour les noms, chemins et CIDR. */
  mono?: boolean;
  /** Valeur qui diffère de l'environnement de référence (grille par environnement). */
  changed?: boolean;
}
export declare function TextField(props: TextFieldProps): React.ReactElement;

export interface SegmentedOption { value: string; label: React.ReactNode; icon?: IconName }
export interface SegmentedProps { options: SegmentedOption[]; value?: string; defaultValue?: string; onChange?: (value: string) => void; mono?: boolean; ariaLabel?: string; className?: string }
export declare function Segmented(props: SegmentedProps): React.ReactElement;

export interface ToggleProps { checked?: boolean; defaultChecked?: boolean; onChange?: (checked: boolean) => void; label?: string; ariaLabel?: string; className?: string }
export declare function Toggle(props: ToggleProps): React.ReactElement;

export interface TabItem { id: string; label: string; count?: number | string; countTone?: 'neutral' | 'ember' }
export interface TabsProps { items: TabItem[]; active?: string; onChange?: (id: string) => void; ariaLabel?: string; className?: string }
export declare function Tabs(props: TabsProps): React.ReactElement;

export interface ResourceIconProps {
  type: ResourceType;
  /** Taille en px (défaut 28). */
  size?: number;
  /** azure : icône officielle Microsoft (défaut). tile : abréviation sur fond de catégorie. */
  variant?: 'azure' | 'tile';
  /** Texte alternatif ; défaut : nom complet du service. `''` si le nom est écrit juste à côté. */
  alt?: string;
  title?: string; className?: string; style?: React.CSSProperties;
}
export declare function ResourceIcon(props: ResourceIconProps): React.ReactElement;

export interface ResourceRowProps {
  type: ResourceType; name: string; typeLabel?: string;
  /** Nom exact écrit dans le Bicep pour l'environnement affiché. */
  generatedName: string;
  /** Explication quand le nom est ajusté (« sans tirets, contrainte Azure »). */
  generatedNote?: string;
  detail?: React.ReactNode;
  status?: { tone: Tone; label: string };
  href?: string; className?: string; style?: React.CSSProperties;
}
export declare function ResourceRow(props: ResourceRowProps): React.ReactElement;

export interface GeneratedNameProps { value: string; env?: string; /** true : disponible, false : pris, absent : non vérifié. */ available?: boolean; className?: string }
export declare function GeneratedName(props: GeneratedNameProps): React.ReactElement;

export interface GoldenPathStep { label: string; state?: 'done' | 'attention' | 'error' | 'current' | 'pending'; meta?: string; hint?: string }
export interface GoldenPathRailProps { steps?: GoldenPathStep[]; ariaLabel?: string; className?: string; style?: React.CSSProperties }
export declare function GoldenPathRail(props: GoldenPathRailProps): React.ReactElement;

export interface BannerProps { tone?: 'ember' | 'danger' | 'success' | 'signal'; title?: string; children?: React.ReactNode; action?: React.ReactNode; className?: string }
export declare function Banner(props: BannerProps): React.ReactElement;

export interface PanelProps { title?: string; subtitle?: string; actions?: React.ReactNode; children?: React.ReactNode; /** false : corps sans padding (tableaux, listes). */ padded?: boolean; as?: 'section' | 'div' | 'aside'; className?: string; style?: React.CSSProperties }
export declare function Panel(props: PanelProps): React.ReactElement;

export interface ResourceTypeMeta { label: string; abbr: string; category: ResourceCategory; icon: string; roadmap?: boolean }

declare global {
  interface Window {
    Strata: {
      Button: typeof Button; Icon: typeof Icon; Badge: typeof Badge; TextField: typeof TextField; Segmented: typeof Segmented;
      Toggle: typeof Toggle; Tabs: typeof Tabs; ResourceIcon: typeof ResourceIcon; ResourceRow: typeof ResourceRow;
      GeneratedName: typeof GeneratedName; GoldenPathRail: typeof GoldenPathRail; Banner: typeof Banner; Panel: typeof Panel;
      resourceTypes: Record<ResourceType, ResourceTypeMeta>;
      /** Icônes Azure officielles en data URI, indexées par `ResourceTypeMeta.icon`. */
      azureIcons: Record<string, string>;
    };
  }
}
