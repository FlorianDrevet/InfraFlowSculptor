# 06 — Parcours utilisateur

> Les parcours principaux, de bout en bout, avec les écrans et les personnes impliqués. Ils servent à
> concevoir l'interface ([26](26-interface.md)) et à écrire les tests de bout en bout. Chaque étape renvoie
> au document qui la spécifie.

## P-01 — De zéro au premier déploiement

**Persona** : architecte plateforme d'une entreprise cliente. **Durée visée** : moins d'une journée
([00 § 4](00-vision.md)).

| # | Action | Écran | Référence |
|---|---|---|---|
| 1 | Se connecter avec son compte Microsoft, créer l'organisation. | Connexion, Organisation | [10 § 3](10-organisations-et-acces.md) |
| 2 | Ajouter la connexion git : installer l'application GitHub ou ajouter le principal de service à Azure DevOps. | Organisation › Connexions | [24 § 2](24-depots-et-publication.md) |
| 3 | Créer le projet par l'assistant : identité, outils (langage, plateforme), environnements, nommage (préréglage), publication (préréglage, dépôts). | Assistant | [11 § 3](11-projets-et-environnements.md) |
| 4 | Ajouter le composant socle, ses ressources, puis un composant applicatif, ou partir d'un modèle de composant *(lot 2)*. | Projet, Composant | [12](12-composants-et-groupes-de-ressources.md), [14](14-modele-des-ressources.md) |
| 5 | Relier : l'application lit le Key Vault, tire du registre, écrit dans la file. IFS ajoute les rôles. | Ressource › Liaisons, Paramètres | [16](16-liaisons-identites-et-acces.md), [17](17-parametres-applicatifs-et-secrets.md) |
| 6 | Corriger les constats jusqu'à zéro erreur. | Compteur de constats, corrections en un clic | [20](20-validation.md) |
| 7 | Générer la révision 1, relire le code produit. | Révisions | [21](21-generation-et-revisions.md) |
| 8 | Publier : une pull request par dépôt. | Publication | [24 § 6](24-depots-et-publication.md) |
| 9 | Fusionner les pull requests chez le fournisseur git. | Fournisseur git | — |
| 10 | Suivre la liste de contrôle : exécuter le script Azure, puis la partie plateforme, saisir les secrets. | Liste de contrôle | [23](23-kit-installation.md) |
| 11 | Les pipelines CI puis release se déclenchent. Pour la production, l'approbateur lit l'aperçu produit par le stage Aperçu, puis approuve le stage Déploiement. | Plateforme CI ; IFS › Déploiements | [22 § 3.1](22-pipelines.md), [28](28-suivi-des-deploiements.md) |
| 12 | IFS affiche « révision 1 déployée » sur toutes les cibles ; la liste de contrôle est complète. | Déploiements | [28](28-suivi-des-deploiements.md) |

**Points de friction à surveiller** : étapes 2 et 10 (droits chez le client). L'écran explique
précisément quels droits demander et à qui.

## P-02 — Une équipe produit ajoute un service

**Persona** : développeur, rôle **Développeur** limité à son nouveau composant, une fois celui-ci créé par
l'architecte ou par un modèle.

| # | Action | Référence |
|---|---|---|
| 1 | L'architecte crée le composant `payments` (ou le duplique depuis `orders`) et donne le rôle Développeur à l'équipe, portée `payments`. | [UC-CMP-01](12-composants-et-groupes-de-ressources.md), [10 § 4](10-organisations-et-acces.md) |
| 2 | Le développeur ajoute sa Container App, sa base, ses paramètres. Il relie son application au Key Vault du socle : cet accès à un autre composant devient une **demande d'accès**, que l'architecte accepte. | [RG-ORG-09](10-organisations-et-acces.md), [RG-LIA-24](16-liaisons-identites-et-acces.md) |
| 3 | Il règle le build et les étapes de qualité (tests, couverture, Sonar). | [19](19-applications-build-et-deploiement.md) |
| 4 | Il génère une révision (`generer` vaut pour tout le projet, [RG-ORG-10](10-organisations-et-acces.md)). Une personne qui a `publier` publie (si la publication à deux personnes est exigée, ce n'est pas lui). | [21](21-generation-et-revisions.md), [RG-ORG-16](10-organisations-et-acces.md) |
| 5 | La liste de contrôle montre le différentiel : nouveau secret à saisir, nouveau dépôt si « un dépôt par composant ». | [RG-INS-06](23-kit-installation.md) |

## P-03 — Changer un paramètre en production

| # | Action | Référence |
|---|---|---|
| 1 | Modifier la surcharge `prd` du paramètre. | [17](17-parametres-applicatifs-et-secrets.md) |
| 2 | Générer ; le résumé ne cite que ce paramètre. | [21 § 2](21-generation-et-revisions.md) |
| 3 | Publier ; la pull request montre le diff du fichier de valeurs `prd`. | [24](24-depots-et-publication.md) |
| 4 | La release d'infrastructure se déclenche ; l'approbateur voit l'aperçu (une seule modification d'application). | [22 § 3](22-pipelines.md) |
| 5 | L'application redémarre avec la nouvelle valeur. Aucune livraison de code. | [DEC-53](03-decisions.md) |

Si la valeur doit changer souvent sans déploiement, l'écran propose de la déplacer dans App Configuration.

## P-04 — Livrer une nouvelle version du code

Entièrement hors d'IFS : commit → CI (build, étapes de qualité, image) → release (promotion, approbation
en production, contrôle de santé). IFS l'affiche dans le suivi des déploiements de l'application
([28 § 3.2](28-suivi-des-deploiements.md)).

## P-05 — Retirer une ressource

| # | Action | Référence |
|---|---|---|
| 1 | Supprimer la ressource ; IFS montre l'impact (liaisons, rôles, paramètres). | [UC-RES-05](14-modele-des-ressources.md) |
| 2 | Générer, publier, déployer. La ressource est détachée dans Azure. | [DEC-46](03-decisions.md) |
| 3 | Elle apparaît dans l'inventaire des ressources détachées ; supprimer avec la commande fournie, puis la marquer « supprimée ». | [28 § 5](28-suivi-des-deploiements.md) |

## P-06 — Un agent IA propose une évolution

| # | Action | Référence |
|---|---|---|
| 1 | Dans son outil (Claude, Copilot), l'utilisateur demande « ajoute une file `refunds` et donne à `api` le droit d'y écrire ». | [25](25-agent-ia-mcp.md) |
| 2 | L'agent lit le modèle, prépare les commandes, vérifie par `preview_change`, crée une proposition. | [25 § 3](25-agent-ia-mcp.md) |
| 3 | Un membre qui a `propositions.appliquer` relit la proposition dans IFS et l'applique. | [25 § 4](25-agent-ia-mcp.md) |
| 4 | Suite du parcours P-03 à partir de l'étape 2. | — |

## P-07 — Reprendre une infrastructure existante *(lot 3)*

Import depuis ARM, Bicep ou un groupe de ressources ([29](29-import.md)) → revue → proposition → première
révision qui **adopte** les ressources sans les recréer ([RG-IMP-09](29-import.md)).

## P-08 — Changer d'outil

| Changement | Parcours | Référence |
|---|---|---|
| Plateforme CI (Azure DevOps → GitHub Actions) | Changer dans le projet, ressaisir les approbateurs, générer, publier, exécuter la partie plateforme du kit, supprimer les objets orphelins de l'ancienne plateforme. | [DEC-48](03-decisions.md) |
| Langage (Bicep → Terraform) *(lot 3)* | Changer dans le projet, générer (blocs `import`), publier, exécuter le script Azure (stockage d'état), vérifier l'aperçu (aucune création ni suppression), déployer, retirer l'ancienne pile en mode détaché. | [DEC-48](03-decisions.md) |

## P-09 — Construire une application d'IA *(lot 2)*

| # | Action | Référence |
|---|---|---|
| 1 | Ajouter au socle un compte Foundry, un déploiement de modèle de conversation et un d'embeddings (capacité réduite en dev, plus forte en prod), un AI Search. | [32 § 2](32-ia-et-foundry.md) |
| 2 | Ajouter un projet Foundry pour l'équipe ; s'il faut des agents avec données chez le client, passer en configuration standard et relier stockage, AI Search et Cosmos DB. | [32 § 4](32-ia-et-foundry.md) |
| 3 | Relier l'application au projet par une liaison « utilisation d'IA » : IFS ajoute le rôle, l'adresse et les noms de déploiement. | [32 § 3](32-ia-et-foundry.md) |
| 4 | En prod, passer le compte et ses dépendances en privé ; IFS ajoute points de terminaison et zones DNS. | [18 § 8](18-reseau-et-exposition.md) |
| 5 | Vérifier les quotas (connexion Azure) et le coût estimé, puis générer et publier. | [RG-IA-02](32-ia-et-foundry.md), [33 § 3](33-gouvernance-couts-et-supervision.md) |

## P-10 — Brancher un projet sur une Landing Zone existante *(lot 2)*

| # | Action | Référence |
|---|---|---|
| 1 | Déclarer le hub de chaque environnement comme VNet existant, et les zones DNS privées comme zones existantes. | [18 § 4](18-reseau-et-exposition.md) |
| 2 | Créer un VNet spoke par environnement dans le socle (plan d'adressage assisté), l'appairer au hub ; choisir « côté distant géré par l'équipe du hub » si l'accès au hub est refusé. | [18 § 5](18-reseau-et-exposition.md) |
| 3 | Router le trafic sortant vers le pare-feu du hub (table de routage) ; serveurs DNS = résolveur du hub. | [18 § 6](18-reseau-et-exposition.md) |
| 4 | Ajouter un Managed DevOps Pool dans un composant `outillage` déployé en premier, et l'utiliser comme exécuteurs des cibles privées. | [18 § 9](18-reseau-et-exposition.md) |
| 5 | Passer les ressources en privé ; corriger les constats (consommateurs non intégrés, DNS, exécuteurs). | [20](20-validation.md) |
| 6 | La liste de contrôle donne à l'équipe du hub les actions à faire de son côté (appairage distant, flux du pare-feu). | [23 § 4](23-kit-installation.md) |

## P-11 — Livrer en bleu/vert et revenir en arrière *(lot 2)*

| # | Action | Référence |
|---|---|---|
| 1 | Choisir la stratégie bleu/vert pour `api` en prod ; générer, publier, déployer l'infrastructure (mode de révisions multiples). | [19 § 9](19-applications-build-et-deploiement.md) |
| 2 | Une nouvelle version passe la CI ; en prod, la release crée la révision `green` à 0 % et la teste sur son adresse d'étiquette. | [RG-APP-19](19-applications-build-et-deploiement.md) |
| 3 | L'approbateur valide la bascule ; 100 % du trafic passe sur `green`. | — |
| 4 | Un problème apparaît : depuis IFS, le suivi des déploiements indique l'action de retour (trafic vers `blue`). | [UC-APP-03](19-applications-build-et-deploiement.md) |

## P-12 — Annuler une erreur de modélisation

| # | Action | Référence |
|---|---|---|
| 1 | Dans l'historique du projet, retrouver le jeu de modifications fautif (auteur, date, détail). | [31 § 2](31-historique-et-versions.md) |
| 2 | L'annuler : sans conflit, l'annulation est immédiate ; avec conflit, une proposition est créée. | [UC-HIS-04](31-historique-et-versions.md) |
| 3 | Générer, publier, déployer si la modification avait déjà été déployée. | [RG-HIS-09](31-historique-et-versions.md) |
