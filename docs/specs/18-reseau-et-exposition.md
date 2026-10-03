# 18 — Réseau et exposition

## 1. Objectif

Décider comment chaque ressource est joignable, et garantir qu'une ressource restreinte ou privée reste
joignable par ceux qui en ont besoin : applications, pipelines de déploiement, résolution DNS. Le
principe : **le réseau privé est complet ou absent** ([DEC-28](03-decisions.md)).

## 2. Exposition réseau

Chaque type qui le supporte ([15](15-catalogue.md)) a une propriété **Exposition**, surchargeable par
environnement (exemple : publique en dev, privée en prod).

| Mode | Lot | Effet |
|---|---|---|
| **Publique** | 1 | Accès réseau public autorisé. Pour SQL et PostgreSQL : règle « services Azure ». |
| **Publique restreinte** | 1 | Accès public limité à une liste de plages IP (CIDR), surchargeable par environnement. Pour les applications : restrictions d'accès ; pour les données : règles de pare-feu. |
| **Privée** | 2 | Accès public désactivé ; accès par point de terminaison privé uniquement. |

**RG-NET-01 — Défaut.** Publique, sauf si le descripteur impose autre chose.

**RG-NET-02 — Plages IP.** CIDR IPv4 valides, 1 à 100 entrées. Une liste vide en mode restreint est une
erreur `VAL-NET-IP-VIDE`.

**RG-NET-03 — Joignabilité du déploiement (lot 1).** Si une ressource restreinte doit être jointe par la
release (accès aux données, [RG-LIA-22](16-liaisons-identites-et-acces.md)), la release ouvre et
referme une règle temporaire pour l'agent. Aucun autre type n'exige d'accès au plan de données pendant
le déploiement.

## 3. Réseau virtuel *(lot 2)*

| Donnée | Règles |
|---|---|
| Espaces d'adressage | Liste de CIDR, surchargeable par environnement. |
| Serveurs DNS | Liste d'IPv4, vide = DNS Azure. Surchargeable. |
| Subnets (enfants) | Nom (1–80, unique), préfixe CIDR (surchargeable), délégation, NSG (liaison), politiques réseau des points de terminaison privés. |

**RG-NET-04 — Contrôles CIDR.**
- Un subnet est inclus dans un espace d'adressage du VNet (`VAL-NET-CIDR-HORS`).
- Deux subnets d'un VNet ne se chevauchent pas (`VAL-NET-CIDR-CHEVAUCHE`).
- Deux VNets d'un même projet dans un même environnement ne se chevauchent pas (`Avertissement`, pour
  permettre le peering).
- La taille minimale d'un subnet dépend de son usage, fixée par le descripteur : intégration sortante,
  environnement Container Apps, points de terminaison privés.

**RG-NET-05 — Délégation exclusive.** Un subnet délégué (`Microsoft.Web/serverFarms`,
`Microsoft.App/environments`) ne reçoit que des ressources de cette délégation. Pas de point de
terminaison privé dans un subnet délégué.

## 4. Exposition privée *(lot 2)*

**RG-NET-06 — Points de terminaison privés.** Une ressource privée a au moins une liaison « point de
terminaison privé » vers un subnet du même environnement et de la même région. Il y a **un point de
terminaison par `groupId`** : l'utilisateur choisit les sous-ressources exposées (exemple : `blob` seul
pour un stockage). Une ressource privée sans point de terminaison est impossible à enregistrer.

**RG-NET-07 — Stratégie DNS.** Chaque environnement (et chaque cible propre `Single`) choisit une
stratégie, obligatoire dès qu'une ressource privée y est présente (`VAL-NET-DNS`) :

| Stratégie | Effet |
|---|---|
| **Zones gérées par IFS** | IFS déploie les zones `privatelink.*` nécessaires dans un composant désigné (souvent `Single`), les relie aux VNets du projet et crée les groupes de zones DNS des points de terminaison. |
| **Zones existantes** | L'utilisateur fournit, par zone, l'identifiant Azure de la zone (souvent dans un abonnement hub). IFS crée les groupes de zones DNS ; l'identité de déploiement doit avoir les droits sur ces zones (liste de contrôle). |
| **Gérée par une stratégie Azure** | L'organisation crée les enregistrements par Azure Policy. IFS ne crée rien ; la liste de contrôle le rappelle. |

**RG-NET-08 — Consommateurs intégrés.** Toute application qui consomme une ressource privée (liaison
d'accès, tirage d'image, stockage hôte, lecture de configuration, paramètre applicatif qui la cite)
doit avoir une **intégration sortante** vers un subnet qui peut joindre le point de terminaison :
- même VNet, ou VNet relié au même ensemble de zones DNS ;
- sinon erreur `VAL-NET-CONSOMMATEUR`.

**RG-NET-09 — Agents de déploiement.** Si la release doit joindre une ressource privée (accès aux
données, écriture de secrets dans un Key Vault privé), l'environnement doit utiliser des exécuteurs
auto-hébergés déclarés « dans le réseau » (`VAL-NET-AGENT`).

**RG-NET-10 — Accès public coupé.** Une ressource privée est générée avec l'accès réseau public
désactivé, et seulement si les règles précédentes sont satisfaites.

## 5. Intégration sortante *(lot 2)*

**UC-NET-01 — Intégrer une application à un réseau.** Liaison « intégration sortante » d'une WebApp ou
FunctionApp vers un subnet délégué `Microsoft.Web/serverFarms`. Tout le trafic sortant passe par le VNet.

**UC-NET-02 — Environnement Container Apps intégré.** Le subnet de l'environnement se choisit à la
création (propriété verrouillée) ; il est délégué `Microsoft.App/environments`.

## 6. Groupes de sécurité réseau *(lot 2)*

**UC-NET-03 — Gérer un NSG** : règles (nom, priorité 100–4096 unique par sens, sens, accès, protocole,
ports, source et destination en CIDR ou tag de service). Un NSG se lie à un ou plusieurs subnets.

## 7. Domaines personnalisés *(lot 2)*

Applicables aux WebApp, FunctionApp et ContainerApp.

| Champ | Règles |
|---|---|
| Environnement | Un domaine vaut pour un environnement. |
| Nom d'hôte | FQDN valide, 253 caractères max, en minuscules, unique dans le projet par environnement. |
| Certificat | `Géré` (certificat managé gratuit) ou `Key Vault` (Key Vault + nom du certificat + identité). |

**RG-NET-11 — Vérification au déploiement** ([DEC-29](03-decisions.md)). IFS n'accède pas à Azure ; la
vérification se fait dans la release :
1. la release déploie l'application, puis lit ses sorties (nom d'hôte par défaut, identifiant de
   vérification du domaine) ;
2. elle vérifie dans le DNS public que le `CNAME` (ou l'`A`) pointe vers l'application et que le
   `TXT asuid.<domaine>` porte l'identifiant attendu ;
3. si oui, elle lie le domaine et son certificat ;
4. sinon, elle ne lie pas le domaine, termine en **avertissement** (pas en échec) et affiche les
   enregistrements exacts à créer.

**RG-NET-12 — Pas de statut fictif.** IFS n'affiche pas de statut « validé ». L'écran montre les
enregistrements attendus (calculables avant déploiement) et renvoie vers le dernier résultat de la
release.

**RG-NET-13 — Certificat Key Vault.** L'identité choisie reçoit implicitement le rôle Key Vault
Certificate User (et Secrets User, exigé par App Service) sur le Key Vault.
