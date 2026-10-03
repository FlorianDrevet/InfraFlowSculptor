# 17 — Paramètres applicatifs et secrets

## 1. Objectif

Fournir aux applications leur configuration sans la coder en dur et sans faire transiter de secret en
clair. Un **paramètre applicatif** a une destination et une source ; IFS génère la valeur, le chemin
d'accès et les droits correspondants ([DEC-27](03-decisions.md)).

## 2. Destinations

| Destination | Destinataire | Identification | Unicité |
|---|---|---|---|
| **Variable d'environnement** | WebApp, FunctionApp, ContainerApp | Nom | Par application |
| **Clé de configuration** | AppConfiguration | Clé + label (facultatif) | Par magasin, sur le couple (clé, label) |

**RG-PAR-01 — Nom de variable.** `^[A-Za-z_][A-Za-z0-9_.-]{0,255}$`. Le séparateur hiérarchique s'écrit
`__` (`ConnectionStrings__Main`). Un `:` est refusé avec cette explication : il n'est pas supporté sous
Linux.

**RG-PAR-02 — Clé de configuration.** 1 à 1024 caractères, sans `%`, différente de `.` et `..`. Label :
0 à 128 caractères, sans `*`, `,`, `\`.

**RG-PAR-03 — Doublon.** Un doublon est refusé à la saisie avec un message qui nomme le paramètre
existant.

## 3. Sources de valeur

| Source | Saisie | Valeur à l'exécution |
|---|---|---|
| **Littérale** | Une valeur, avec surcharge facultative par environnement. | La valeur effective de l'environnement, écrite en clair. |
| **Sortie de ressource** | Ressource du projet + sortie **non sensible** de son descripteur. | Calculée au déploiement (exemple : `vaultUri`). |
| **Secret Key Vault** | Key Vault (du projet ou existant) + nom du secret + alimentation (section 4). | Référence Key Vault résolue par Azure ou par l'application ; la valeur n'apparaît jamais dans les fichiers. |

**RG-PAR-04 — Sortie sensible.** Une sortie sensible (chaîne de connexion, clé) ne peut être utilisée
qu'en alimentation d'un secret Key Vault. Le serveur refuse toute autre utilisation.

**RG-PAR-05 — Valeur suspecte.**
- **Avant tout enregistrement**, IFS analyse la valeur littérale : motifs de jetons connus, chaînes de
  connexion, forte entropie. Une valeur détectée est refusée, avec la conversion en secret de pipeline
  proposée ; l'utilisateur peut la forcer en déclarant qu'elle n'est pas secrète, ce qui est journalisé
  ([DEC-95](03-decisions.md)).
- Une valeur dont seul le **nom** ressemble à un secret (`password`, `secret`, `token`, `apikey`,
  `connectionstring`, insensible à la casse) produit l'avertissement `VAL-PAR-SECRET-SUSPECT`, avec une
  action « convertir en secret de pipeline ».
- Une valeur secrète enregistrée par erreur peut être **expurgée** ([RG-HIS-12](31-historique-et-versions.md)).

**RG-PAR-06 — Sortie entre composants.** La ressource source peut appartenir à un autre composant. La
liaison implicite « dépendance de valeur » ([16](16-liaisons-identites-et-acces.md)) ajoute la
dépendance entre composants.

**RG-PAR-07 — Tout est modifiable.** Destination, nom, source et valeurs se modifient sans supprimer le
paramètre.

## 4. Alimentation d'un secret Key Vault

| Alimentation | Sens | Génération |
|---|---|---|
| **Géré hors IFS** | Le secret existe déjà, ou un tiers l'écrit. | Rien n'est écrit ; la liste de contrôle rappelle que le secret doit exister. |
| **Sortie sensible** | La valeur est la sortie sensible d'une ressource du projet (exemple : `connectionString` d'un stockage à clé partagée). | Le déploiement écrit la sortie dans le secret. |
| **Secret de pipeline** | La valeur est saisie par le client dans le magasin de secrets de sa plateforme CI. | Une étape de la release lit le secret et l'écrit dans le Key Vault ([section 6](#6-secrets-de-pipeline)). |

**RG-PAR-08 — Nom de secret.** 1 à 127 caractères, lettres, chiffres, tirets. Deux paramètres qui
écrivent le même secret du même Key Vault avec des alimentations différentes sont en erreur
`VAL-PAR-SECRET-CONFLIT`. Un secret n'a qu'un écrivain : le composant qui porte son Key Vault, ou le
composant consommateur si le coffre est une ressource existante ([DEC-98](03-decisions.md)).

**RG-PAR-22 — Coffre avant consommateur.** Un Key Vault qui reçoit un secret de pipeline ou un mot de passe
généré appartient à un composant déployé **avant** tout composant qui consomme ce secret, ou est une
ressource existante. Sinon : erreur `VAL-SEC-ORDRE` ([DEC-86](03-decisions.md)). Un consommateur est une
application qui référence le secret, ou un serveur qui reçoit le mot de passe. L'assistant crée par défaut
un composant socle qui porte les coffres.

**RG-PAR-09 — Droit de lecture implicite.**
- Variable d'environnement : l'application reçoit le rôle Key Vault Secrets User sur le Key Vault, pour
  l'identité choisie dans le paramètre (par défaut l'identité par défaut de l'application,
  [RG-LIA-10](16-liaisons-identites-et-acces.md), sinon son identité système). Une Container App exige une
  identité affectée : son identité système n'existe pas encore quand ses secrets sont résolus à la
  création. C'est une liaison implicite « lecture de secret ».
- Clé de configuration : chaque application liée au magasin par « lecture de configuration » reçoit ce
  rôle.

## 5. Génération

Les formes ci-dessous sont des formes **Azure** : chaque émetteur les exprime dans son langage
([DEC-44](03-decisions.md)).

| Destination | Source | Forme générée |
|---|---|---|
| Variable d'environnement (WebApp, FunctionApp) | Littérale, sortie | Entrée du paramètre `appSettings` de l'application. |
| | Secret Key Vault | `@Microsoft.KeyVault(SecretUri=…)` ; si l'identité est affectée, `keyVaultReferenceIdentity` est positionné. |
| Variable d'environnement (ContainerApp) | Littérale, sortie | `env` du conteneur. |
| | Secret Key Vault | Secret de Container App avec `keyVaultUrl` et identité, référencé par `secretRef`. |
| Clé de configuration | Littérale, sortie | Entrée du fichier de clés du magasin pour la cible (`appconfig/<magasin>.<cible>.json`), synchronisée par la release (RG-PAR-23). |
| | Secret Key Vault | Même fichier, entrée de type référence Key Vault (`application/vnd.microsoft.appconfig.keyvaultref+json`). |

**RG-PAR-23 — Synchronisation des clés.** Les clés ne sont pas des ressources du code d'infrastructure : la
release du composant qui porte le magasin les écrit par le plan de données, après le déploiement, dans
tous les langages ([22 § 3.1](22-pipelines.md)). Elle ajoute et met à jour les clés du fichier, et supprime
les clés qu'IFS avait écrites et qui n'y sont plus (elles portent le tag `managed-by: infraflowsculptor`).
Une clé créée hors IFS n'est jamais touchée. L'identité de déploiement reçoit App Configuration Data Owner
sur le magasin par le déploiement lui-même ([RG-LIA-18](16-liaisons-identites-et-acces.md)).

**RG-PAR-10 — Paramètres implicites.** Les paramètres déduits des liaisons (`APPLICATIONINSIGHTS_CONNECTION_STRING`,
`AzureWebJobsStorage__*`, `AZURE_CLIENT_ID`, adresse App Configuration) s'affichent avec les autres,
marqués implicites. Un paramètre explicite de même nom est refusé.

**RG-PAR-11 — Propriétaire unique.** Les paramètres d'une application sont écrits par le déploiement
d'infrastructure. Le pipeline applicatif ne les modifie jamais ([19](19-applications-build-et-deploiement.md)).
Un paramètre ajouté à la main dans Azure est donc écrasé au déploiement suivant ; l'écran de
l'application le rappelle.

## 6. Secrets de pipeline

**RG-PAR-12 — Variable.** Chaque secret alimenté par pipeline a une variable secrète dans le magasin de
secrets CI de chaque cible où le paramètre s'applique ([23](23-kit-installation.md)) : groupe de
variables `ifs-<projet>-<cible>` (Azure DevOps), secret de l'environnement `<projet>-<cible>` dans
chaque dépôt qui déploie l'infrastructure (GitHub), variable CI/CD limitée à l'environnement (GitLab). Nom par défaut :
`<nom logique du Key Vault>_<nom du secret>`, en majuscules, tirets remplacés par `_` (exemple :
`MAIN_PAYMENTS_API_KEY`) ; modifiable, `^[A-Za-z_][A-Za-z0-9_]{0,127}$`, unique dans le magasin.

**RG-PAR-13 — IFS ne connaît pas la valeur.** Le kit d'installation crée la variable vide quand la
plateforme le permet (Azure DevOps), sinon il liste les secrets attendus absents (GitHub). Il ne lit ni ne
modifie jamais une valeur existante.

**RG-PAR-14 — Contrôle avant déploiement.** La release vérifie, avant de déployer une cible, que chaque
variable secrète attendue a une valeur. Si ce n'est pas le cas, elle échoue avec la liste des variables
vides et l'emplacement où les saisir.

**RG-PAR-15 — Écriture hors du code d'infrastructure.** Une étape de la release écrit chaque secret de
pipeline dans son Key Vault (Azure CLI, avec l'identité de déploiement) :
- la release du composant qui porte le Key Vault l'écrit **après** son déploiement ; les consommateurs,
  déployés plus tard (RG-PAR-22), le trouvent donc dès leur création ;
- si le Key Vault est une ressource existante, la release du composant consommateur l'écrit **avant** son
  déploiement ([22 § 3.1](22-pipelines.md)).

La valeur ne passe jamais par le code d'infrastructure : elle n'apparaît dans aucun fichier, aucun
historique de déploiement ARM ni aucun état Terraform ou Pulumi ([DEC-46](03-decisions.md)). L'étape
n'écrit une nouvelle version du secret que si la valeur a changé ; les applications qui le référencent
sont alors redémarrées pour relire la référence.

## 7. Mots de passe et clés d'administration

L'authentification locale est permise mais déconseillée ([DEC-51](03-decisions.md)). Quand elle est
activée, IFS organise la vie du secret sans jamais en connaître la valeur.

| Secret | Ressources | Origine |
|---|---|---|
| Mot de passe administrateur | SqlServer (`EntraEtSql`), PostgreSqlFlexibleServer (`EntraEtMotDePasse`) | Choisie dans la ressource : `Généré` (défaut) ou `Secret de pipeline` |
| Identifiants admin du registre | ContainerRegistry (utilisateur admin activé) | Générés par Azure |
| Clés d'accès | StorageAccount (clé partagée), ServiceBusNamespace (authentification locale) | Générées par Azure, exposées comme sorties sensibles |

**RG-PAR-16 — Key Vault de stockage.** Toute ressource qui active un mot de passe ou un compte admin
désigne un Key Vault du projet (ou existant) qui le reçoit. Nom de secret par défaut :
`<nom logique>-admin-password` (ou `-admin-username` / `-admin-password` pour le registre).

**RG-PAR-17 — Mot de passe généré.** Le mot de passe d'un serveur vit dans un Key Vault déployé avant le
serveur (RG-PAR-22).
1. La release du composant qui porte le Key Vault, après son déploiement, génère le mot de passe s'il
   n'existe pas encore (longueur 32, quatre classes de caractères, conforme à la politique Azure du
   service) et l'écrit dans le coffre. Si le coffre est une ressource existante, c'est la release du
   composant du serveur qui le fait, avant son déploiement.
2. La release du composant du serveur lit le secret dans son stage Aperçu (il doit exister, sinon échec
   « déployez d'abord <composant du coffre> ») et le transmet au code d'infrastructure comme entrée
   sensible.

Les déploiements suivants relisent le même secret : le mot de passe ne change pas.

**RG-PAR-18 — Mot de passe fourni.** En alimentation `Secret de pipeline`, la valeur vient du magasin de
secrets CI ([section 6](#6-secrets-de-pipeline)). La release l'écrit dans le Key Vault, puis la transmet au
code d'infrastructure.

**RG-PAR-19 — Transmission au code d'infrastructure.**
- Bicep : paramètre `@secure()`.
- Terraform : attribut en écriture seule quand le fournisseur le propose (exemple :
  `administrator_login_password_wo` avec sa version) ; sinon attribut classique, la valeur est alors dans
  l'état protégé ([EXG-20](27-exigences-non-fonctionnelles.md)) et un constat `Info` le signale.
- Pulumi : valeur secrète, chiffrée dans l'état.

**RG-PAR-20 — Identifiants du registre.** Si l'utilisateur admin d'un registre est activé, la release copie
le nom d'utilisateur et le mot de passe dans le Key Vault désigné après chaque déploiement.

**UC-PAR-04 — Renouveler un mot de passe** *(lot 2)* (`modele.modifier`). Le prochain déploiement de la
cible génère un nouveau mot de passe, l'écrit dans le Key Vault, puis l'applique. Les applications qui le
lisent par référence Key Vault sont redémarrées.

**RG-PAR-21 — Usage par les applications.** Une application peut lire un mot de passe ou une clé par un
paramètre de source « Secret Key Vault » ([section 3](#3-sources-de-valeur)), en alimentation « géré hors
IFS » vers le secret ci-dessus. L'écran rappelle que l'accès par identité ([16 § 6](16-liaisons-identites-et-acces.md))
est préférable. *(Lot 2 : accès aux données par utilisateur SQL dédié à mot de passe, créé par le script
post-déploiement, plutôt que par le compte administrateur.)*

## 8. Import depuis un fichier

**UC-PAR-01 — Importer des paramètres.** L'utilisateur dépose un fichier ; IFS propose les paramètres
extraits, qu'il valide un par un ou en bloc.

| Format | Lecture |
|---|---|
| `appsettings*.json` | Objets aplatis avec `__`. |
| `local.settings.json` | Section `Values`. |
| `.env` | Lignes `CLÉ=valeur`, commentaires ignorés. |
| `application.properties` | `clé=valeur` ou `clé: valeur`. |
| `application.yml` | Aplati avec `.`, converti en `__`. |

Chaque entrée devient un paramètre littéral. Les entrées suspectes ([RG-PAR-05](#3-sources-de-valeur))
sont proposées par défaut comme secrets de pipeline, **sans leur valeur** : le fichier n'est pas
conservé et la valeur n'est jamais enregistrée.

## 9. Cas d'utilisation

- **UC-PAR-02** — Ajouter, modifier, supprimer un paramètre (`modele.modifier`).
- **UC-PAR-03** — Lister les paramètres d'une application ou d'un magasin, explicites et implicites,
  avec leur valeur effective par environnement (les secrets montrent leur référence, jamais une valeur).
- **UC-PAR-05** — Voir les sorties disponibles d'une ressource, avec leur description et leur caractère
  sensible.
