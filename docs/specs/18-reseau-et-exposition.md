# 18 — Réseau et exposition

## 1. Objectif

Décider comment chaque ressource est joignable, et garantir qu'une ressource restreinte ou privée reste
joignable par ceux qui en ont besoin : applications, pipelines, résolution DNS. Deux principes :
- **le réseau privé est complet ou absent** ([DEC-28](03-decisions.md)) ;
- **IFS déduit tout ce qui peut l'être** : zones DNS, enregistrements, liens, appairages inverses, rôles
  ([DEC-68](03-decisions.md)).

Lot 1 : exposition publique et restreinte. Lot 2, vague A : tout le reste de ce document, sauf mention.

## 2. Exposition d'une ressource

Chaque type qui le supporte a une propriété **Exposition**, surchargeable par environnement (exemple :
publique en dev, privée en prod).

| Mode | Lot | Effet |
|---|---|---|
| **Publique** | 1 | Accès public autorisé. Pour SQL et PostgreSQL : règle « services Azure ». |
| **Publique restreinte** | 1 | Accès public limité à une liste de plages IP (CIDR), surchargeable par environnement ; et, au lot 2, à des subnets autorisés par point de terminaison de service. Applications : restrictions d'accès ; données : règles de pare-feu. |
| **Privée** | 2 | Accès public désactivé ; accès par point de terminaison privé uniquement. |

**RG-NET-01 — Défaut.** Publique, sauf si le descripteur ou une politique d'organisation
([DEC-71](03-decisions.md)) impose autre chose.

**RG-NET-02 — Plages IP.** CIDR IPv4 valides, 1 à 100 entrées. Une liste vide en mode restreint est une
erreur `VAL-NET-IP-VIDE`.

**RG-NET-03 — Joignabilité du déploiement.** Si une ressource restreinte doit être jointe par la release
(accès aux données, écriture de secrets), la release ouvre puis referme une règle temporaire pour
l'exécuteur, même en cas d'échec. Pour une ressource privée, voir section 9.

## 3. Réseaux virtuels et subnets

| Donnée du VNet | Règles |
|---|---|
| Espaces d'adressage | Liste de CIDR, surchargeable par environnement. |
| Serveurs DNS | Liste d'IPv4, vide = DNS Azure. Surchargeable (exemple : résolveur du hub). |
| Protection DDoS | Liaison facultative vers un plan DDoS existant. |

| Donnée d'un subnet (enfant) | Règles |
|---|---|
| Nom | 1 à 80 caractères, unique dans le VNet. |
| Préfixe | CIDR, surchargeable par environnement. |
| Délégation | `Aucune`, `Microsoft.Web/serverFarms`, `Microsoft.App/environments`, `Microsoft.DevOpsInfrastructure/pools`, `GitHub.Network/networkSettings`, `Microsoft.DBforPostgreSQL/flexibleServers`, `Microsoft.ContainerInstance/containerGroups`. |
| Usage | `Points de terminaison privés`, `Intégration d'applications`, `Environnement Container Apps`, `Exécuteurs`, `Générique`. L'usage fixe la délégation attendue et la taille minimale. |
| Liaisons | NSG, table de routage, passerelle NAT (facultatives). |
| Points de terminaison de service | Liste de fournisseurs (`Microsoft.Storage`, `Microsoft.Sql`…). |

**RG-NET-04 — Contrôles d'adressage.**
- Un subnet est inclus dans un espace d'adressage de son VNet (`VAL-NET-CIDR-HORS`).
- Deux subnets d'un VNet ne se chevauchent pas (`VAL-NET-CIDR-CHEVAUCHE`).
- Deux VNets **appairés** (directement, ou par le même hub) ne se chevauchent pas : `Erreur`
  (`VAL-NET-VNET-CHEVAUCHE`). Deux VNets non appairés d'un même environnement qui se chevauchent :
  `Avertissement`.
- Taille minimale par usage, fixée par le descripteur (`VAL-NET-SUBNET-TAILLE`) : par exemple /27 pour un
  environnement Container Apps à profils de charge, /28 pour l'intégration d'applications.

**RG-NET-05 — Délégation exclusive.** Un subnet délégué ne reçoit que des ressources de sa délégation. Pas
de point de terminaison privé dans un subnet délégué.

**UC-NET-01 — Plan d'adressage assisté.** À partir d'un espace par environnement (exemple :
`10.10.0.0/16` en dev), IFS propose un découpage en subnets selon les usages déclarés, sans chevauchement,
avec une marge de croissance. L'utilisateur accepte ou ajuste.

## 4. Topologies

| Topologie | Modélisation | Pour qui |
|---|---|---|
| **Un VNet par environnement** | Un VNet dans le composant socle, présent dans chaque environnement. | Projets autonomes. |
| **Hub and spoke modélisé** | Un composant `connectivité` porte le hub : VNet, zones DNS privées, éventuellement un pare-feu existant référencé. Il est `Single` (un hub pour tous les environnements) ou `PerEnvironment` (un hub par environnement, souvent dans l'abonnement de connectivité, [DEC-69](03-decisions.md)). Chaque composant applicatif a son VNet *spoke* appairé au hub. | Équipes plateforme qui gèrent leur réseau dans IFS. |
| **Hub existant** | Le hub (VNet, zones DNS) est une **ressource existante** par environnement, dans un abonnement géré par une autre équipe. Les spokes s'y appairent. | Entreprises avec une Landing Zone Azure existante. |

## 5. Appairage

**UC-NET-02 — Appairer deux VNets.** Liaison **appairage** d'un VNet vers un autre VNet du projet ou un
VNet existant.

| Paramètre | Valeurs | Défaut |
|---|---|---|
| Accès au réseau distant | Booléen | Oui |
| Trafic transféré accepté | Booléen | Oui |
| Transit par passerelle | `Aucun`, `Le distant utilise ma passerelle`, `J'utilise la passerelle distante` | `Aucun` |

**RG-NET-06 — Les deux côtés.** IFS génère l'appairage dans les deux sens.
- Si le VNet distant est une ressource existante d'un autre abonnement, le côté distant est déployé à sa
  portée. L'identité de déploiement doit pouvoir y créer l'appairage (Network Contributor sur le VNet du
  hub, ou un rôle personnalisé plus étroit du client) : le kit l'indique dans la liste de contrôle.
- Si le client interdit cet accès, l'option **« côté distant géré par l'équipe du hub »** génère seulement
  le côté local et fournit, dans la liste de contrôle, la commande exacte pour l'équipe du hub.

**RG-NET-07 — Environnements.** Un appairage relie des VNets du **même** environnement, sauf vers un hub
`Single` partagé par tous les environnements. Un appairage dev ↔ prod entre spokes est refusé
(`VAL-NET-APPAIRAGE-ENV`).

## 6. Sortie et routage

**UC-NET-03 — Passerelle NAT.** Ressource **passerelle NAT** avec ses IP publiques (ressource **IP
publique**, SKU Standard, statique), liée à des subnets. Elle donne une IP de sortie stable aux
applications intégrées, par exemple pour les listes blanches des partenaires. L'IP est une sortie de la
ressource.

**UC-NET-04 — Table de routage.** Ressource **table de routage** avec ses routes : préfixe, type de saut
(`VirtualAppliance` avec IP, `Internet`, `VnetLocal`, `None`). Elle se lie à des subnets. Cas type : tout le
trafic sortant vers le pare-feu du hub (`0.0.0.0/0` → IP du pare-feu existant).

**RG-NET-08 — Intégration et routage.** Une application intégrée à un subnet routé vers un pare-feu reçoit
`vnetRouteAllEnabled`. La liste de contrôle rappelle d'ouvrir sur le pare-feu les flux sortants
nécessaires : Azure Monitor, Key Vault, registre, points de terminaison de la plateforme.

## 7. Groupes de sécurité réseau

**UC-NET-05 — Gérer un NSG.** Règles : nom, priorité de 100 à 4096 unique par sens, sens, accès,
protocole, ports, source et destination en CIDR, tag de service ou groupe de sécurité d'application. Un NSG
se lie à des subnets.

**RG-NET-09 — Règles requises.** Certains usages exigent des règles (environnement Container Apps, pool
d'exécuteurs…). Le descripteur les fournit ; IFS les ajoute comme règles **implicites**, visibles et non
supprimables, à chaque NSG lié à un subnet de cet usage.

## 8. Points de terminaison privés et DNS privé

### 8.1 Points de terminaison privés

**RG-NET-10 — Un point de terminaison par sous-ressource.** Une ressource privée a, pour chaque
sous-ressource exposée (`groupId`, exemple : `blob` seul pour un stockage), une liaison **point de
terminaison privé** vers un subnet d'usage « points de terminaison privés » du même environnement et de la
même région. Une ressource privée sans point de terminaison ne peut pas être enregistrée.

**RG-NET-11 — Consommateurs.** Toute ressource qui consomme une ressource privée doit pouvoir la joindre :
liaison d'accès, tirage d'image, stockage hôte, lecture de configuration, paramètre qui la cite,
connexion Foundry. Une application doit avoir une intégration sortante dans un VNet relié (même VNet,
appairé, ou par le hub). Sinon `VAL-NET-CONSOMMATEUR`.

### 8.2 Zones DNS privées

**RG-NET-12 — Zones nécessaires.** Le descripteur de chaque type privatisable déclare les zones
`privatelink.*` nécessaires à chaque `groupId`. Exemples :
- `privatelink.vaultcore.azure.net` pour un Key Vault ;
- `privatelink.azurecr.io` et la zone des points de terminaison de données pour un registre ;
- trois zones pour Foundry ([32](32-ia-et-foundry.md)).

Les zones qui dépendent de la région ou du service sont calculées par IFS.

**RG-NET-13 — Stratégie DNS** par environnement (et par cible propre `Single`). Elle est obligatoire dès
qu'une ressource privée y est présente (`VAL-NET-DNS`) :

| Stratégie | Effet |
|---|---|
| **Zones gérées par IFS** | Un composant désigné (souvent le hub) reçoit, comme ressources **implicites**, toutes les zones nécessaires au projet, avec un **lien** vers chaque VNet qui doit résoudre. Les groupes de zones DNS des points de terminaison y pointent. Une zone apparaît ou disparaît automatiquement avec les ressources privées. |
| **Zones existantes** | L'utilisateur fournit l'identifiant de chaque zone, souvent dans l'abonnement du hub. IFS crée les groupes de zones DNS ; l'identité de déploiement doit pouvoir écrire dans ces zones (Private DNS Zone Contributor, liste de contrôle). |
| **Gérée par une stratégie Azure** | Les enregistrements sont créés par des stratégies « deploy if not exists » de la Landing Zone. IFS ne crée rien ; la validation vérifie seulement que chaque VNet consommateur utilise les serveurs DNS déclarés (résolveur du hub). |

**RG-NET-14 — Environnement Container Apps interne.** Un environnement Container Apps à ingress interne
reçoit une zone DNS privée implicite pour son domaine par défaut, avec l'enregistrement générique vers son
IP statique, liée aux VNets consommateurs.

## 9. Pipelines et réseau privé

**RG-NET-15 — Exécuteurs dans le réseau.** Si la release doit joindre une ressource privée (accès aux
données, écriture de secrets dans un Key Vault privé, clés App Configuration avec accès public coupé), la
cible doit utiliser des exécuteurs qui atteignent le réseau. Sinon `VAL-NET-AGENT`. Trois moyens :

| Moyen | Plateforme | Modélisation |
|---|---|---|
| **Managed DevOps Pool** | Azure DevOps | Ressource **pool d'exécuteurs** (`Microsoft.DevOpsInfrastructure/pools`) : subnet délégué, taille de VM, nombre maximal d'agents, image, organisation et projets Azure DevOps autorisés. IFS l'utilise comme exécuteurs de la cible. |
| **Runners GitHub en réseau privé** | GitHub Actions (lot 2, vague D) | Ressource **réglages réseau GitHub** (`GitHub.Network/networkSettings`) dans un subnet délégué ; le groupe de runners se configure côté GitHub (liste de contrôle). |
| **Exécuteurs auto-hébergés** | Toutes | Déclarés par leur nom de pool ou leurs libellés, marqués « dans le réseau ». |

**RG-NET-16 — L'œuf et la poule.** Le pool d'exécuteurs privé ne peut pas se déployer lui-même. Il vit dans
un composant déployé avec des exécuteurs hébergés (le hub, ou un composant `outillage`), avant les
composants qui en ont besoin. La validation le vérifie (`VAL-NET-AGENT-CYCLE`).

## 10. Intégration sortante des applications

**UC-NET-06 — Intégrer une application.** Liaison **intégration sortante** d'une Web App ou d'une Function
App vers un subnet d'usage « intégration d'applications ». Tout le trafic sortant passe par le VNet.

**UC-NET-07 — Environnement Container Apps intégré.** Le subnet de l'environnement se choisit à la
création (propriété verrouillée), d'usage « environnement Container Apps ». L'ingress de l'environnement est
`Externe` ou `Interne`.

## 11. DNS public et domaines personnalisés

**UC-NET-08 — Zone DNS publique.** Ressource **zone DNS** (Azure DNS) pour un domaine du client (exemple :
`contoso.com`), avec des enregistrements saisis (A, AAAA, CNAME, TXT, MX).

**UC-NET-09 — Domaine personnalisé** d'une Web App, Function App ou Container App :

| Champ | Règles |
|---|---|
| Environnement | Un domaine vaut pour un environnement. |
| Nom d'hôte | FQDN valide, 253 caractères max, en minuscules, unique dans le projet par environnement. |
| Zone | Zone DNS publique du projet (ou existante) qui porte le domaine, facultative. |
| Certificat | `Géré` (certificat managé gratuit) ou `Key Vault` (Key Vault + nom du certificat + identité). |

**RG-NET-17 — Domaine dans une zone modélisée.** Si le domaine appartient à une zone du projet, IFS génère
les enregistrements (`CNAME` vers l'application, `TXT asuid` avec l'identifiant de vérification lu pendant
le même déploiement) avant la liaison du domaine et du certificat. Le domaine fonctionne dès le premier
déploiement, sans action manuelle.

**RG-NET-18 — Domaine hors zone modélisée** ([DEC-29](03-decisions.md)). La release vérifie les
enregistrements DNS publics avant de lier le domaine. S'ils manquent, elle ne lie pas, termine en
avertissement et affiche les enregistrements exacts à créer.

**RG-NET-19 — Certificat Key Vault.** L'identité choisie reçoit implicitement Key Vault Certificate User
(et Secrets User, exigé par App Service) sur le Key Vault.

**RG-NET-20 — Front Door** *(lot 2, vague F)*. Une application exposée par Front Door a son domaine porté
par Front Door, et son accès direct restreint à Front Door (tag de service et contrôle de l'en-tête
`X-Azure-FDID`). IFS le génère dès que la liaison « origine Front Door » existe.

## 12. Ce qui reste hors IFS

Azure Firewall et DNS Private Resolver comme ressources gérées arrivent au lot 3. En attendant, ils sont
référencés comme ressources existantes : IP du pare-feu dans les routes, IP du résolveur dans les serveurs
DNS des VNets.
