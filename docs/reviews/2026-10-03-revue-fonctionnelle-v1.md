# Revue critique de la spécification InfraFlowSculptor v1

Date : 3 octobre 2026. Périmètre : corpus `docs/specs`, y compris décisions, projet de référence et annexe v0. Revue documentaire et vérifications ciblées dans les documentations officielles ; aucun déploiement ni test du logiciel n'a été réalisé.

## 1. Compréhension, hypothèses et informations manquantes

### Compréhension du produit

**Faits présents dans la spec.** IFS est un SaaS qui transforme un modèle Azure en code d'infrastructure, pipelines et kit d'installation conservés chez le client. Les utilisateurs visés sont les architectes plateforme, développeurs et intégrateurs ; les agents IA constituent un canal d'utilisation supplémentaire. Le lot 1 cible Bicep et Azure DevOps, avec Azure Repos et GitHub pour les dépôts. Les déploiements sont exécutés chez le client et suivis par lecture de la plateforme CI.

La valeur attendue est double : réussir le premier déploiement rapidement et maintenir ensuite des conventions et des accès cohérents. La deuxième partie constitue, à mon avis, la meilleure piste de différenciation ; elle reste à valider auprès d'utilisateurs.

La spec prévoit déjà l'isolation des organisations, une matrice de permissions, la concurrence optimiste, des propositions, l'audit, des extensions, un manifeste de fichiers, la restauration du modèle et des ressources détachées. Les constats ci-dessous portent principalement sur les interactions entre ces mécanismes et leurs effets réels.

### Hypothèses de cette revue — à confirmer

| Hypothèse | Pourquoi elle change l'analyse |
|---|---|
| Le premier marché visé est une équipe Azure qui démarre une nouvelle application ou un nouveau composant. | L'import d'une infrastructure complète arrive au lot 3 ; une cible majoritairement composée d'infrastructures existantes changerait les priorités. |
| Un projet utilise des abonnements d'un même tenant Entra. | Le modèle porte les abonnements mais ne définit pas complètement le tenant par cible et les liaisons entre tenants. |
| Les déploiements de production font partie de la promesse du lot 1. | `prd`, les approbations et le projet de référence sont présents dès le premier jalon. Les problèmes de révocation et d'approbation sont donc bloquants. |
| Le MVP doit être exploitable par des clients pilotes, avec une assistance limitée. | Une démonstration accompagnée peut accepter davantage d'étapes manuelles qu'un produit commercial autonome. |
| L'équipe de réalisation doit limiter ses investissements avant validation commerciale. | Aucun effectif ni budget ne permet de justifier la largeur des quatre lots. |

### Informations absentes qui empêchent une décision fiable

- Entretiens et engagements de clients pilotes : fréquence des créations de services, temps actuellement perdu, contraintes d'approbation, volonté de payer.
- Équipe, compétences disponibles, budget, date cible et temps réservé à la maintenance du catalogue.
- Frontière contractuelle du support : génération seulement, diagnostic des pipelines ou assistance Azure jusqu'au rétablissement.
- Conditions acceptées chez les clients : droits à l'abonnement, accès public, consentement Entra, services cloud autorisés, dépôts privés accessibles au SaaS.
- Définition du lancement : pilote sur abonnement de test, production limitée ou ouverture commerciale avec SLA.

### Lecture successive des dix rôles

| Regard | Challenge principal |
|---|---|
| Utilisateur final | « Retiré », « restauré » et « déployé » doivent décrire précisément ce qui s'est réellement passé ; ces mots engagent la confiance. |
| Product Manager | Le premier déploiement et la maintenance quotidienne sont deux hypothèses à mesurer séparément. |
| Expert métier plateforme | Un accès intercomposants, un secret partagé ou une modification du socle ont un propriétaire métier qui doit pouvoir décider. |
| UX/UI Designer | L'utilisateur doit comprendre la prochaine action, son responsable et les prérequis, sans parcourir plusieurs outils pour reconstruire l'état. |
| Architecte / Lead Tech | Le séquencement initial, les mutations partielles et les différences entre moteurs IaC conditionnent le modèle fonctionnel. |
| QA / Test Manager | Le projet de référence vérifie surtout le chemin heureux ; les garanties majeures exigent des scénarios de retrait, concurrence et reprise. |
| RSSI / conformité | Il faut limiter les effets indirects des commandes et garantir la révocation effective, pas seulement contrôler les routes de l'API. |
| Support / opérations | Un échec après déploiement peut laisser Azure modifié ; il faut identifier les étapes terminées et proposer une reprise sûre. |
| Finance / direction | La maintenance des variantes, les coûts des essais cloud et l'assistance peuvent dominer le coût du SaaS. |
| Business challenger | Il faut démontrer le gain face à des modules et pipelines internes déjà maîtrisés, particulièrement après la première génération. |

## 2. Analyse critique structurée

**Lecture des tableaux.** Les observations citent des faits ou omissions du corpus ; les recommandations sont des propositions de cette revue. Les probabilités sont des appréciations qualitatives, sans mesure d'usage : « certaine dans ce cas » signifie que les règles se contredisent lorsque le scénario se présente. Les priorités portent sur le lancement du périmètre concerné ; un problème du lot 3 ne bloque pas le lot 1.

### 2.1 Objectifs produit et valeur métier

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A01 | Valeur | [Vision §3–5](../specs/00-vision.md) vise plateforme, développeurs et ESN ; réseau privé, modules internes et import arrivent plus tard. | Les clients les plus sensibles à la gouvernance peuvent avoir besoin de ces prérequis dès l'achat. | Un produit complet pour un segment qui ne peut pas l'adopter. | Moyenne, à valider | Élevé | Choisir un segment pilote : nouvelles applications Azure, Bicep/ADO, dans un cadre réseau accepté. Qualifier les exclusions avant invitation. | Qui peut réellement utiliser et acheter le lot 1 ? |
| A02 | Valeur | La cible « moins d'une journée » inclut implicitement consentements, accès git, droits Azure et approbateurs, sans point de départ opérationnel défini. | Une journée de travail et une journée calendaire sont différentes. | Promesse invérifiable et attribution erronée des échecs. | Élevée | Élevé | Mesurer séparément le temps de préparation des accès, le temps actif de modélisation et le temps jusqu'à une application fonctionnelle. | La promesse commence-t-elle avant ou après obtention des droits ? |

### 2.2 Utilisateurs, rôles et permissions

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A03 | Permissions | [RG-ORG-09](../specs/10-organisations-et-acces.md) autorise une liaison vers un autre composant ; [UC-RES-05](../specs/14-modele-des-ressources.md) supprime aussi les références entrantes. Les permissions des objets indirectement modifiés ne sont pas explicitées. | Une commande locale peut accorder un accès au coffre du socle ou supprimer le paramètre d'une autre équipe. | Franchissement de la responsabilité d'une équipe. | Élevée en usage partagé | Bloquant | Calculer l'ensemble des effets et leurs permissions avant application ; demander l'accord du propriétaire pour un nouvel accès sensible. Refuser une cascade hors portée. | Qui autorise l'accès à une ressource cible et les mutations induites ? |
| A04 | Permissions | `membres.gerer` permet d'attribuer un rôle ; l'Administrateur du projet l'a mais n'a pas `projet.administrer`. [UC-HIS-05](../specs/31-historique-et-versions.md) permet aussi de restaurer un projet avec une liste de permissions incomplète. | Sans plafond d'attribution et contrôle commande par commande, un administrateur peut se donner Propriétaire ou une restauration modifier une cible protégée. | Élévation de privilèges par attribution, proposition ou restauration. | Moyenne | Bloquant | Réserver l'attribution du rôle Propriétaire au propriétaire ; exiger pour chaque effet restauré les mêmes permissions que pour une modification directe. | `propositions.appliquer` autorise-t-il à lui seul les opérations sous-jacentes ? |
| A05 | Permissions | [RG-ORG-10](../specs/10-organisations-et-acces.md) retire les permissions hors composant d'un rôle limité. Le développeur de [P-02](../specs/06-parcours-utilisateur.md), limité à `payments`, est pourtant censé générer une révision ; `generer` n'a pas de portée composant. | Le parcours principal ne correspond pas à la matrice. | Développeur bloqué ou permission globale ajoutée au cas par cas. | Certaine dans ce cas | Élevé | Accorder explicitement la génération au niveau projet, ou définir une génération d'essai limitée et ses règles de visibilité. | Quel pouvoir de génération donne-t-on au développeur limité ? |
| A06 | Permissions | [Kit §2](../specs/23-kit-installation.md) donne `Contributor` à l'abonnement et réutilise l'identité de cible ; [RG-APP-08](../specs/19-applications-build-et-deploiement.md) utilise l'identité du registre pour pousser l'image. | Du code de build ou une extension client s'exécute dans un contexte potentiellement très privilégié. | Compromission de l'abonnement depuis un pipeline de code. | Moyenne | Bloquant | Séparer identité de build, identité de livraison applicative et identité d'infrastructure ; restreindre portées et pipelines autorisés ; isoler les validations de PR non fiables. | Quel pipeline peut utiliser quelle identité, à quelle portée ? |
| A07 | Permissions | [RG-PUB-03 et UC-PUB-05](../specs/24-depots-et-publication.md) permettent de choisir et parcourir les dépôts accessibles à une connexion d'organisation, sans attribution explicite connexion/dépôt → projets autorisés. | L'accès technique du SaaS peut être plus large que celui d'un membre. | Exposition de code d'une autre équipe via un projet créé pour y accéder. | Moyenne | Élevé | Autoriser les dépôts par projet ou groupe de projets ; rendre la sélection elle-même contrôlée. Réévaluer l'accès à chaque lecture. | Un créateur de projet peut-il choisir tous les dépôts de l'organisation ? |

### 2.3 Parcours utilisateurs

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A08 | Premier déploiement | [P-01](../specs/06-parcours-utilisateur.md), [kit §2](../specs/23-kit-installation.md) et [RG-PAR-17](../specs/17-parametres-applicatifs-et-secrets.md) préparent des droits ou lisent un coffre avant que les ressources concernées soient créées. Les secrets de pipeline sont écrits après le déploiement de l'application qui les référence. | Le parcours initial comporte des dépendances sans phase de résolution définie. | Échec initial, relances manuelles et ouverture de droits plus larges pour débloquer. | Élevée | Bloquant | Définir les phases : fondations et identités, ressources support, droits, secrets, applications, accès aux données ; décrire la reprise entre phases et tester depuis un abonnement vide. | Que crée le kit, que crée la première release, et quand les consommateurs démarrent-ils ? |
| A09 | Parcours de découverte | [DEC-78](../specs/03-decisions.md) promet génération sans connexion git ; [VAL-APP-CODE-SOURCE](../specs/20-validation.md) bloque une application sans destination de code ; [RG-GEN-04](../specs/21-generation-et-revisions.md) exige la compilation avant enregistrement alors qu'une extension locale peut n'exister que chez le client. | L'essai autonome et la personnalisation ont besoin de niveaux de validation différents. | Découverte bloquée, ou révision annoncée déployable sans validation complète. | Élevée | Élevé | Distinguer modèle valide, archive générable, publication possible et déploiement prévalidé. Définir un contrat d'extension vérifiable sans accès à son contenu. | Peut-on générer un exemple téléchargeable avec des chemins encore non résolus ? |
| A10 | Collaboration | [UC-GEN-01](../specs/21-generation-et-revisions.md) génère toujours tout le projet ; les brouillons sont au lot 2. Une modification incomplète d'une équipe peut bloquer ou rejoindre la livraison d'une autre. | Les composants sont présentés comme autonomes, mais leur espace de travail ne l'est pas. | Blocage d'un correctif urgent ou publication involontaire de changements. | Élevée dès plusieurs contributeurs | Élevé | Pour le MVP, réserver l'application au modèle principal à des propositions atomiques validées, ou assumer un seul éditeur coordinateur. Prévoir ensuite un espace de préparation isolé. | Comment livrer un correctif pendant qu'une autre équipe prépare une modification ? |

### 2.4 Règles de gestion et processus métier

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A11 | Retrait et révocation | [DEC-46](../specs/03-decisions.md) détache les ressources retirées, toujours en cible protégée ; [RG-LIA-06/14](../specs/16-liaisons-identites-et-acces.md) dit que les droits implicites disparaissent. Aucune exception ne garantit la suppression effective des attributions RBAC retirées. | Détacher conserve la ressource Azure. Un droit ne doit pas rester actif alors que le modèle le présente comme retiré. | Accès persistants aux secrets et données ; anciennes configurations encore actives. | Élevée au premier retrait | Bloquant | Séparer cycle de vie des données et cycle de vie des autorisations/configurations. Révoquer explicitement les droits gérés et devenus inutiles, avec propriétaire et preuve d'exécution. | Quels objets sont conservés, supprimés ou révoqués lors d'un retrait ? |
| A12 | Propriété des objets | [RG-LIA-14](../specs/16-liaisons-identites-et-acces.md) déduplique un rôle, mais chaque composant a sa pile ; plusieurs composants peuvent partager identité et cible. Les secrets partagés n'ont pas non plus de propriétaire d'écriture explicite. | La déduplication logique ne définit pas quel déploiement crée, modifie ou retire l'objet partagé. | Piles concurrentes, rôle retiré alors qu'il est encore utilisé, écrasement d'un secret. | Moyenne | Élevé | Attribuer un unique propriétaire de déploiement aux rôles, secrets, zones et autres objets dérivés ; conserver les origines consommatrices. | Qui possède chaque objet dérivé partagé ? |
| A13 | Dépendances | [RG-CMP-05/06](../specs/12-composants-et-groupes-de-ressources.md) transforme toute liaison en dépendance ordonnée et interdit les cycles. Deux applications ayant besoin de leurs adresses respectives deviennent impossibles à décrire. | Une dépendance d'exécution n'est pas toujours une dépendance de création. | Modèle artificiellement regroupé, perte d'autonomie des composants. | Moyenne | Élevé | Distinguer dépendance de création, configuration et exécution ; sinon documenter explicitement cette limite du MVP et fournir un parcours alternatif. | Quelles liaisons imposent réellement un ordre de création ? |
| A14 | Livraison compatible | [RG-APP-03](../specs/19-applications-build-et-deploiement.md) affirme qu'ajouter un paramètre avant le code est toujours sûr ; [22 §3.1](../specs/22-pipelines.md) ne vérifie que l'existence des dépendances. | Un paramètre peut être interprété par l'ancien code ; une ressource existante peut avoir une version incompatible. | Rupture malgré un ordre apparemment correct. | Moyenne | Élevé | Remplacer « toujours sûr » par une obligation de compatibilité déclarée ; lier chaque livraison à des prérequis d'infrastructure/configuration et prévoir une évolution en deux temps. | Comment exprime-t-on la version minimale d'une dépendance ? |
| A15 | Retour arrière | [RG-HIS-09](../specs/31-historique-et-versions.md) propose restaurer → générer → déployer, alors que certaines propriétés sont irréversibles et que les données peuvent avoir changé. | Restaurer un modèle n'annule ni une croissance de stockage, ni une protection contre la purge, ni une migration de données. | Fausse promesse de reprise et tentative de modification refusée. | Élevée lors d'un incident | Élevé | Calculer un plan de retour : applicable, partiel, impossible ou avec intervention. Expliquer séparément restauration du modèle, du code et des données. | Que garantit le bouton restaurer après un déploiement irréversible ? |

### 2.5 Données et qualité des données

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A16 | Référence de comparaison | [RG-GEN-19](../specs/21-generation-et-revisions.md) calcule les retraits depuis la dernière révision publiée ; les PR peuvent rester ouvertes, les cibles sauter des versions et les dépôts être publiés partiellement. | Publier, fusionner et appliquer sont des événements différents. | Inventaire faux ; au lot Terraform, retrait à conserver omis du plan d'une cible en retard. | Élevée | Bloquant | Conserver les intentions de retrait jusqu'à leur traitement par cible ; distinguer état attendu, publié, fusionné et résultat de chaque application. Vérifier l'état géré depuis le pipeline du client. | Quelle référence fait foi pour chaque cible et pour chaque opération de retrait ? |
| A17 | Identité et unicité | [RG-NOM-08](../specs/13-nommage.md) compare les noms dans toute l'organisation ; [import JSON](../specs/11-projets-et-environnements.md) conserve les abonnements et ressources existantes. Les codes `Single` ne sont pas explicitement uniques entre eux. | Un clone ou deux cibles `shared` peuvent viser les mêmes ressources, identités techniques ou environnements CI. | Double gestion ou collision ; information sur un projet caché révélée par un message. | Moyenne | Élevé | Définir l'unicité des cibles, la réservation concurrente et une détection des identifiants Azure déjà gérés. Importer un clone en mode non publiable jusqu'à revue du ciblage. | Un export peut-il servir à reprendre le même déploiement ou seulement à créer une copie ? |
| A18 | Évolution du catalogue | [RG-RES-01](../specs/14-modele-des-ressources.md) dépend des défauts du catalogue ; [RG-EXP-05](../specs/40-exploitation-ifs.md) suit automatiquement la dernière version, avec épinglage limité à 90 jours. | Sans modification métier, une nouvelle génération peut changer des valeurs, devenir invalide ou inclure une mise à niveau pendant un correctif urgent. | Changement involontaire et impossibilité de reproduire un ancien état. | Élevée | Élevé | Figer les versions par projet et rendre la migration explicite avec diff. Définir fin de support, correctifs de sécurité et reproduction historique avec ancien catalogue. | À l'expiration des 90 jours, migre-t-on, bloque-t-on ou prolonge-t-on ? |

### 2.6 Fonctionnalités et priorisation

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A19 | MVP | [Lot 1](../specs/04-perimetre-et-lots.md) comprend 18 types, deux fournisseurs git, plusieurs builds, historique avancé, MCP et commercialisation ; deux émetteurs supplémentaires doivent aussi produire un projet déployable. | Même le jalon 1 inclut de nombreux mécanismes difficiles à rendre fiables ensemble. | Délai long avant retour client ; coût de maintenance sous-estimé. | Élevée | Élevé | Définir un pilote séparé du lot 1 : un fournisseur git, un parcours conteneur, un petit catalogue et une validation complète des changements usuels. Borner les prototypes de neutralité. | Quel apprentissage impose réellement chaque fonctionnalité du MVP ? |
| A20 | Couverture du catalogue | [StaticWebApp §3.17](../specs/15-catalogue.md) est une application du lot 1, mais [19 §1 et RG-APP-17](../specs/19-applications-build-et-deploiement.md) ne couvrent que WebApp, FunctionApp et ContainerApp. Managed Redis introduit une politique d'accès hors du contrat RBAC général. | Ajouter un type impose un parcours, des permissions et un cycle de retrait, pas seulement un descripteur. | Type visible mais impossible à livrer correctement. | Certaine pour la couverture documentaire | Élevé | Exiger pour chaque type une fiche complète : création, build éventuel, droits, mise à jour, retrait, restauration, critères d'acceptation. Reporter les types incomplets. | Ces deux types sont-ils nécessaires aux premiers clients ? |

### 2.7 Intégrations et dépendances externes

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A21 | Aperçu Azure | [22 §3.2](../specs/22-pipelines.md) promet un `what-if` de pile. Microsoft indique que le `what-if` des Deployment Stacks n'est pas pris en charge. | C'est un garde-fou central avant approbation. | Fonction impossible telle qu'écrite et retraits mal présentés. | Confirmée par la documentation | Bloquant | Produire un aperçu ARM du template et une analyse distincte du cycle de vie de la pile ; présenter leurs limites. Valider le comportement des retraits sur un prototype. | Quelle preuve exacte l'approbateur examine-t-il ? |
| A22 | Approbations CI | [22 §3.1](../specs/22-pipelines.md) place aperçu puis approbation dans un même stage protégé ; Azure DevOps évalue les checks avant le stage. | L'aperçu prévu n'existe pas encore lorsque l'approbation est demandée. | Approbation à l'aveugle ou contournement improvisé du contrôle. | Confirmée pour ce découpage | Bloquant | Préparer l'aperçu dans une phase préalable, puis approuver un déploiement lié à cet artefact. Définir expiration et recalcul après changements concurrents. | Comment relier l'approbation au contenu et au contexte effectivement appliqués ? |
| A23 | Validation et SQL | Le [kit ADO §3.1](../specs/23-kit-installation.md) crée les pipelines, sans politiques de validation des branches Azure Repos. [RG-LIA-21](../specs/16-liaisons-identites-et-acces.md) crée des utilisateurs SQL via identité de service sans décrire les permissions Graph de l'identité du serveur. | Deux prérequis externes du parcours heureux ne sont pas couverts. | PR non validées automatiquement ; échec de création des accès SQL. | Élevée | Élevé | Ajouter les politiques de build et leurs permissions au kit ; modéliser le prérequis SQL/Graph ou choisir une méthode explicitement validée qui l'évite. | Qui dispose des droits git et Entra nécessaires ? |
| A24 | Publication git | [RG-PUB-11/12/14](../specs/24-depots-et-publication.md) prépare puis écrit ; une PR existante est remplacée. Aucun contrat précis ne couvre branche avancée, timeout après commit, nouvelle revue humaine ou publication concurrente. | La concurrence existe aussi chez le fournisseur. | Écrasement de revue, commits répétés et publication partielle ambiguë. | Élevée | Élevé | Comparer le SHA attendu avant écriture, rendre les tentatives idempotentes, garder un statut par dépôt et vérifier les validations après changement du contenu d'une PR. | Une PR déjà approuvée peut-elle être remplacée automatiquement ? |
| A25 | Réseau et tenants | [18 §2/8/9](../specs/18-reseau-et-exposition.md) déduit la joignabilité à partir du modèle ; une ressource existante n'a ni réseau ni enfants selon [RG-RES-10](../specs/14-modele-des-ressources.md). Le tenant de chaque abonnement reste peu défini. | Un ID Azure ne donne ni les flux autorisés, ni le DNS, ni les contraintes d'identité ; le mode restreint du lot 1 doit aussi laisser passer les applications. | Configuration valide mais dépendances injoignables. | Élevée en infrastructure existante | Élevé | Introduire des prérequis déclarés pour les dépendances externes et un contrôle de connectivité exécuté chez le client. Limiter explicitement le MVP à un tenant et à des topologies documentées. | Quelles propriétés externes sont vérifiées, déclaratives ou inconnues ? |

### 2.8 Sécurité, confidentialité, conformité et auditabilité

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A26 | Secrets accidentels | [RG-PAR-05](../specs/17-parametres-applicatifs-et-secrets.md) ne fait qu'avertir sur un nom suspect ; [RG-ORG-20](../specs/10-organisations-et-acces.md) et [RG-HIS-02/03](../specs/31-historique-et-versions.md) conservent les valeurs et leur historique. | Un token collé dans `VALUE` peut être stocké, exporté et historisé indéfiniment. | Fuite incompatible avec la promesse « aucun secret applicatif stocké ». | Élevée sur la durée | Élevé | Reformuler la garantie, ajouter détection avant persistance et procédure de retrait d'un contenu sensible dans historiques, fichiers et sauvegardes, avec trace non sensible et rotation côté client. | Comment traite-t-on un secret déjà enregistré par erreur ? |
| A27 | Actions d'un agent | [RG-MCP-05](../specs/25-agent-ia-mcp.md) expose toutes les commandes ; `publish_revision` compte sur une confirmation du client MCP. | Un client automatisé peut ne pas afficher de confirmation ; le mode direct et les extensions augmentent l'effet d'une commande. | Publication ou mutation sensible sans revue effective. | Moyenne | Élevé | Autorisations serveur spécifiques, mode proposition par défaut et confirmation liée à un aperçu versionné pour les opérations sensibles. Exclure explicitement les commandes administratives non nécessaires. | Quelles actions doivent exiger une validation humaine garantie par le serveur ? |
| A28 | Conservation | [EXG-06](../specs/27-exigences-non-fonctionnelles.md) parle de durée légale ; audit 13 mois, journal interne 2 ans et historique à vie suivent des règles différentes. Les contenus libres peuvent contenir des données personnelles. | Une durée définie par le produit n'est pas automatiquement une obligation légale. | Effacement incohérent, données conservées dans exports et historiques. | Élevée | Élevé | Définir finalité, données, durée, accès et effacement par catégorie ; documenter sous-traitants et flux externes. Faire valider le traitement des sauvegardes et des copies exportées. | Quelle justification soutient chaque durée et chaque exception à l'effacement ? |

### 2.9 Performance, disponibilité et montée en charge

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A29 | Charge | [EXG-08/21/22](../specs/27-exigences-non-fonctionnelles.md) fixe de gros volumes et des limites par utilisateur ; [RG-SUI-01](../specs/28-suivi-des-deploiements.md) prévoit du polling. Pas de budget de travail par organisation, de file équitable ni de limite d'historique en volume. | Une organisation peut multiplier jetons, générations et exécutions suivies. | Saturation, frais imprévus et limitation par les fournisseurs. | Moyenne | Élevé | Fixer limites par organisation et connexion, file d'attente visible, pagination et budgets de compilation. Définir les jeux de données des p95, notamment ressources × environnements. | Quel volume et quelle concurrence sont garantis par plan ? |
| A30 | Diff et déclencheurs | [RG-GEN-08](../specs/21-generation-et-revisions.md) inscrit le numéro de révision dans chaque fichier généré ; les triggers de [22](../specs/22-pipelines.md) suivent ces fichiers. | Une petite modification peut changer les en-têtes de tous les composants. | PR bruyantes, builds et déploiements inutiles ; confiance réduite dans le diff. | Certaine si tous les en-têtes sont réécrits | Élevé | Mettre l'identifiant global dans un manifeste/artefact et conserver des fichiers inchangés pour les composants sans changement fonctionnel. | La traçabilité doit-elle rendre tous les fichiers différents à chaque génération ? |

### 2.10 Administration, support et exploitation

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A31 | Échecs partiels | [28 §3](../specs/28-suivi-des-deploiements.md) retient la dernière révision dont le stage a réussi. Pourtant secrets, accès SQL et domaines s'exécutent après le déploiement ; aucun état de connaissance périmée n'est défini. | Un stage rouge peut avoir déjà modifié Azure. Une API CI inaccessible ne prouve pas que rien n'a changé. | Tableau de bord trompeur et reprise sur une mauvaise hypothèse. | Élevée | Élevé | Afficher les étapes appliquées, le dernier succès connu, le dernier essai et la fraîcheur. Ajouter « partiellement appliqué », « inconnu » et une procédure de reprise. | Que signifie exactement « révision déployée » ? |
| A32 | Réconciliation du kit | [RG-INS-05](../specs/23-kit-installation.md) ne supprime rien ; le script réaligne les approbateurs. Aucun parcours complet ne couvre retrait d'une fédération, changement de protection ou exécution d'un vieux kit. | Des accès obsolètes peuvent rester actifs ; une ancienne version peut rétablir une ancienne protection. | Accès persistants et dérive des garde-fous CI. | Élevée | Élevé | Versionner le contrat d'installation, détecter les régressions et distinguer nettoyage d'infrastructure et révocation de sécurité. Produire des actions de désactivation explicites. | Le kit peut-il révoquer un accès devenu inutile et refuser un retour de version ? |
| A33 | Support et reprise | [40](../specs/40-exploitation-ifs.md) prévoit incidents, accès consenti et page d'état ; [EXG-10](../specs/27-exigences-non-fonctionnelles.md) donne RPO/RTO. Le cas du client sans administrateur accessible et la reconciliation après restauration d'IFS ne sont pas décrits. | Git et Azure peuvent avoir avancé pendant la période perdue. | Blocage client ou répétition d'actions externes après reprise. | Moyenne | Élevé | Définir récupération de compte/organisation, diagnostic partageable expurgé, périmètre du support et relecture des publications/exécutions après restauration. | Comment rétablit-on le service sans accès support déjà approuvé ni état local à jour ? |

### 2.11 Accessibilité, internationalisation et compatibilité

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A34 | Accessibilité | [RG-UI-04](../specs/26-interface.md) et [RG-HIS-05](../specs/31-historique-et-versions.md) placent des informations essentielles au survol ; matrices et diff sont centraux malgré la cible WCAG AA. | L'équivalent en liste du graphe ne résout pas l'accès au clavier, au tactile et au lecteur d'écran des autres écrans. | Revue d'impact inaccessible ou incomprise. | Élevée si conçue seulement pour souris | Élevé | Exposer origine et auteur au focus/clic, alternatives textuelles aux couleurs, navigation de matrice et résumé accessible des diff. | Quels parcours doivent être entièrement utilisables au clavier et sur petit écran ? |
| A35 | Connexion et langue | [26](../specs/26-interface.md) décrit préférences et conflits, mais pas la conservation d'un formulaire lors d'une coupure/session expirée. FR/EN ne précise pas le langage des artefacts, dates et nombres. | Les formulaires longs et le collage de configuration amplifient les erreurs. | Perte de saisie ou nouvelle commande envoyée deux fois. | Moyenne | Moyen | Préserver la saisie non sensible, afficher enregistré/en attente/échec, rendre la reprise idempotente ; fixer formats API et conventions de traduction des artefacts. | Le mobile sert-il à consulter/revoir ou à modéliser complètement ? |

### 2.12 Mesure du succès, analytics et indicateurs

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A36 | Succès réel | [00 §8](../specs/00-vision.md) cible >90 % de premiers déploiements sans retouche ; [28 §4](../specs/28-suivi-des-deploiements.md) mesure les runs réussis. Les abandons avant génération et la livraison d'une image de démarrage ne sont pas équivalents à une application utilisable. | Un taux de runs verts peut masquer les échecs d'onboarding. | Optimisation d'un indicateur sans valeur utilisateur. | Élevée | Élevé | Définir cohortes, exclusions et succès fonctionnel ; suivre aussi abandons, modifications manuelles, demandes au support et délai d'une deuxième modification. | Quel événement prouve que l'utilisateur a obtenu la valeur promise ? |
| A37 | Économie | [DEC-78](../specs/03-decisions.md) facture le membre actif ; génération automatisée, départ en cours de mois, dépassement, baisse de plan et quotas cumulés ne sont pas définis. Le coût d'assistance n'est pas chiffré. | Le minimum Équipe vaut 117 €/mois selon la spec, indépendamment du coût des essais et du support. | Factures contestées, gratuité coûteuse et marge inconnue. | Moyenne | Élevé | Définir l'événement facturable, compteur visible, cycle de facturation, gestion des impayés et mode de sortie. Mesurer le coût par organisation activée et par organisation active. | Qui paie l'activité d'un agent et quelle marge vise-t-on ? |

### 2.13 Risques projet, planning et gouvernance

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| A38 | Acceptation | [90](../specs/90-projet-de-reference.md) teste essentiellement une création réussie, un ajout et un retrait de file. Le lot 1 contient d'autres types et des opérations risquées. | Un exemple positif ne démontre pas les garanties générales. | Bugs concentrés sur incidents, droits, retraits et environnements décalés. | Élevée | Élevé | Ajouter les critères de la section 3 et une matrice minimale type × opération × cible protégée ; lier chaque invariant critique à une preuve de validation. | Quelles preuves autorisent réellement la livraison d'un type ? |
| A39 | Cohérence normative | [04 §7](../specs/04-perimetre-et-lots.md) affirme qu'il n'y a plus de point ouvert ; [P1–P11](../specs/01-principes.md) priment sur les règles, alors que certaines décisions plus récentes modifient leur portée. L'annexe A1 emploie « Résolu » pour une solution seulement spécifiée. | Les arbitrages semblent fermés alors que leurs interactions restent indécises. | Interprétations divergentes et impression de validation acquise. | Élevée | Élevé | Réouvrir un registre d'arbitrages ; définir l'ordre de préséance ; distinguer décidé, spécifié, réalisé et vérifié. Synchroniser les documents après chaque décision. | Quel document fait foi en cas de conflit ? |
| A40 | Feuille de route | [P11](../specs/01-principes.md) exige une notion exprimable partout ; [VAL-GEN-LANGAGE](../specs/20-validation.md) prévoit des capacités inégales. Import et migration promettent une adoption sûre malgré propriétés non reprises et modules opaques. | Les différences de sémantique et de cycle de vie ne se réduisent pas à la syntaxe d'un émetteur. | Retard, plafond fonctionnel et migrations risquées. | Élevée lors de l'ouverture | Élevé | Adopter une matrice de capacités explicite, séparer compatibilité de syntaxe et de comportement, borner les migrations aux combinaisons démontrées. | Vise-t-on le plus petit dénominateur commun ou une parité sur un sous-ensemble garanti ? |

### Vérifications externes des constats techniques et de conservation

**A21.** La page des limitations Microsoft indique explicitement l'absence de `what-if` pour les Deployment Stacks. L'aperçu ARM et l'analyse des retraits de pile sont donc deux contrôles à concevoir séparément ; cette proposition est une recommandation de la revue. [Microsoft — limitations des Deployment Stacks](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks-known-issues).

**A11.** Le mode de détachement conserve les ressources dans Azure. J'en déduis que la règle générale de la spec ne suffit pas à garantir la révocation d'une attribution RBAC gérée comme ressource : il faut un traitement explicite. [Microsoft — Deployment Stacks](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks).

**A22 et concurrence.** Azure DevOps évalue les checks des ressources avant de démarrer le stage. Son verrou exclusif utilise `runLatest` par défaut ; la promesse « le second attend » nécessite un choix explicite tel que `sequential`. Le verrou d'environnement proposé dans le kit a aussi une portée plus large que le verrou composant × cible annoncé. [Microsoft — approbations et checks](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/approvals?view=azure-devops).

**A23, PR.** Pour Azure Repos, la validation des PR se configure par politiques de branche ; un simple déclencheur YAML de PR ne suffit pas. [Microsoft — pipelines Azure Repos Git](https://learn.microsoft.com/en-us/azure/devops/pipelines/repos/azure-repos-git?view=azure-devops).

**A23, SQL.** Lorsque la création d'un utilisateur Entra est exécutée par une identité de service avec résolution dans l'annuaire, Azure SQL a besoin des permissions Graph appropriées via l'identité du serveur. L'adhésion du déployeur au groupe administrateur ne décrit donc pas tout le prérequis. [Microsoft — authentification Entra dans Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-overview?view=azuresql).

**A28.** La CNIL recommande généralement six mois à un an pour les journaux et prévoit des durées différentes lorsqu'elles sont justifiées. Cela ne permet ni de déclarer les 13 mois illégaux, ni de les présenter comme une obligation légale générale. [CNIL — recommandation sur la journalisation](https://www.cnil.fr/fr/la-cnil-publie-une-recommandation-relative-aux-mesures-de-journalisation).

Ces vérifications sont ciblées. Elles ne constituent pas une certification de toutes les SKU, versions, régions et capacités listées dans le catalogue.

### Contradictions complémentaires à corriger dans le corpus

Ces points complètent A39 ; ils ne constituent pas chacun un nouveau chantier produit.

| Documents / règles | Désaccord concret | Correction attendue |
|---|---|---|
| 11 §6 et RG-HIS-03 | L'export exclut l'historique dans un document et permet de l'inclure dans l'autre. | Spécifier les variantes d'export, leur autorisation et leur conservation. |
| RG-DON-04 et 28 | Les exécutions suivies sont immuables, tout en devant évoluer de « en cours » vers un résultat. | Distinguer événements immuables et état courant calculé. |
| RG-DON-06 et RG-IA-02 | Aucune lecture Azure conservée hors import/disponibilité des noms, mais lecture d'usage et de quotas pour l'IA. | Définir les catégories admises, leur fraîcheur et leur durée de cache. |
| UC-CMP-01 et RG-PUB-05 | Un composant serait livrable sans intervention sur le plan ; le préréglage « un dépôt par composant » exige cette intervention. | Décrire l'exception dans le parcours et affecter l'action à son responsable. |
| RG-APP-14, RG-PUB-15 et RG-GEN-17 | Une extension applicative manquante est un avertissement ; la publication exige sa présence, et l'extension d'infrastructure manquante bloque. | Fixer la gravité à chaque étape et interdire d'annoncer une publication déployable incomplète. |
| RG-NOM-10 et option « Ressources retirées » | Le message annonce toujours un détachement, alors qu'une cible non protégée peut supprimer. | Calculer le message par cible selon l'action effective. |
| DEC-66, 04 et A1 | Les vagues du lot 2 divergent ; A1 place encore Managed Redis au lot 2. Plusieurs renvois `RG-ORG-11` ne visent plus la bonne règle. | Réaligner les références et générer les matrices depuis une source structurée. |
| 90 §2.5 et RG-APP-08 | L'identité `shared` n'a que Contributor dans les résultats attendus ; la CI requiert aussi un droit de poussée d'image. | Corriger le jeu d'attendus et surtout séparer cette identité de build de celle d'infrastructure. |
| P7 et contrat des extensions | Le catalogue est présenté comme suffisant pour ajouter un type ; des types introduisent pourtant de nouveaux scripts, politiques d'accès et comportements de livraison. | Distinguer ajouts de valeurs dans une capacité existante et nouvelles capacités nécessitant du code. |

## 3. Cas limites, scénarios oubliés et critères d'acceptation

Les comportements ci-dessous sont **proposés**. Ils servent à transformer les arbitrages en critères testables. Les décisions de chaque ligne doivent être prises avant de prétendre garantir le scénario.

| ID | Scénario concret | Comportement produit attendu | Décision à prendre / preuve attendue |
|---|---|---|---|
| S01 | Premier déploiement : coffre créé dans le même composant qu'une base nécessitant un mot de passe, ou qu'une application lisant un secret de pipeline. | Préparer coffre, identité et droits avant toute lecture/écriture ; publier le secret avant le démarrage du consommateur. Réessayer seulement les erreurs transitoires avec une limite. | Définir le découpage en phases et réussir depuis zéro sans seconde exécution corrective manuelle. |
| S02 | Retrait du dernier besoin `Key Vault Secrets User` en production. | Le rôle géré est effectivement révoqué ; le coffre et ses données sont conservés. L'interface indique si une autre origine maintient le droit. | Vérifier que l'application perd l'accès après propagation, pas seulement que le rôle disparaît du code. |
| S03 | Deux composants utilisent la même identité et le même rôle sur une cible partagée. | Un seul propriétaire déploie l'attribution ; retirer une origine ne retire pas le droit tant qu'une autre subsiste. | Définir propriétaire, dépendances et transfert éventuel de responsabilité. |
| S04 | R1 déployée en prod ; R2 retire une ressource, mais seule dev est mise à jour ; prod passe directement à R3. | L'intention de retrait reste applicable à prod. L'inventaire inclut l'ancienne ressource une fois le retrait réellement traité. | Tester la séquence avec révisions sautées et publication dans plusieurs dépôts. |
| S05 | R2 est publiée dans une PR jamais fusionnée ; R3 la remplace ou publie dans un autre dépôt seulement. | Aucun déploiement fictif ; comparaison contre le contenu réellement visé et état de livraison par dépôt/cible. | Faut-il suivre les PR dès le MVP ? Recommandation : oui, au minimum leur commit et leur fusion/fermeture. |
| S06 | Azure applique les ressources, puis l'écriture du secret ou le script SQL échoue. | Afficher « partiellement appliqué » et les phases terminées ; permettre une reprise idempotente depuis la plateforme CI. | Préciser qui décide de reprendre ou de revenir et ce qui reste utilisable pendant l'incident. |
| S07 | L'agent CI s'arrête brutalement après ouverture temporaire du pare-feu. | La règle restante est identifiable, signalée et nettoyable par une procédure indépendante de l'agent interrompu. | Une étape de fin seule ne couvre pas la perte de l'exécuteur ; définir nettoyage de rattrapage et délai maximal. |
| S08 | Une application reçoit une nouvelle image entre la lecture de l'image par la release infra et son application. | Le déploiement infra ne remet pas l'ancienne image. L'aperçu est recalculé si nécessaire. | Définir un verrou commun aux mutations infra/app d'une même application, et sa portée temporelle pendant les approbations. |
| S09 | Deux éditeurs modifient des objets distincts qui, ensemble, créent une collision de nom ou un cycle. | Validation atomique des invariants globaux à la confirmation ; une seule transaction est acceptée si les deux sont incompatibles. | La version par objet suffit-elle ? Définir la version du projet et le comportement de réessai. |
| S10 | Suppression d'une ressource du socle référencée par un composant que l'auteur ne peut pas modifier. | Prévisualiser les effets ; bloquer la cascade ou créer une demande à l'équipe concernée. | Aucun objet hors portée ne doit être modifié indirectement sans autorisation applicable. |
| S11 | Un droit est retiré pendant la préparation d'une publication ou avant application d'une proposition. | Recontrôler les droits au moment de l'effet ; arrêter les écritures non encore réalisées et rapporter les écritures déjà effectuées. | Définir la frontière d'annulation d'une opération externe et le statut « partiel ». |
| S12 | Git accepte le commit mais la réponse réseau est perdue ; l'utilisateur clique de nouveau. | Reprendre la même tentative, retrouver commit/branche/PR ; ne pas dupliquer les effets. | Clé d'idempotence et journal d'opération requis. |
| S13 | Le manifeste est absent, modifié, appartient à un autre projet ou une branche avance après le diff. | Refuser l'écriture automatique ; montrer le conflit et demander une reprise de propriété explicite ou un recalcul. | Quelle preuve d'appartenance fait foi, notamment entre deux organisations IFS utilisant le même dépôt ? |
| S14 | Un membre importe le même projet deux fois ou clone un projet vers les mêmes abonnements. | Remapper les identifiants internes, détecter les ressources déjà gérées et bloquer une double prise de contrôle. | Définir copie, transfert et récupération comme opérations distinctes. |
| S15 | Restauration d'un objet dont la purge protection a été activée, d'une base agrandie ou d'une ressource supprimée avec ses données. | Montrer ce qui est restaurable et ce qui ne l'est pas ; ne pas promettre le retour des données. | Définir les règles d'irréversibilité sur l'état connu par cible et les sauvegardes requises hors IFS. |
| S16 | Renommage du projet ou d'un composant après publication, puis ancienne release relancée. | Inventorier anciens noms de piles, identités et pipelines ; empêcher deux chaînes actives de gérer des ressources incompatibles. | Autoriser ces changements en MVP ou imposer une migration dédiée ? |
| S17 | Secret collé dans une valeur littérale, puis remplacé par une référence Key Vault. | Empêcher les diff, exports et téléchargements ultérieurs de révéler l'ancienne valeur ; guider la rotation chez le client. | Définir redaction exceptionnelle et trace d'audit sans reproduire le secret. |
| S18 | Dépendance Azure absente, quota insuffisant, policy client refusant le déploiement, SKU indisponible. | Précontrôle dans le tenant client avec diagnostic, responsable et action ; aucun « valide » ne doit signifier vérification Azure exhaustive. | Définir « vérifié », « déclaré » et « non vérifié » dans les constats. |
| S19 | Nom historique valide mais absent du catalogue ; paramètre vide, `null`, supprimé ou surcharge retirée. | Distinguer valeur vide, absence et héritage ; refuser la perte silencieuse d'un réglage. | Spécifier ces sémantiques pour API, import de fichiers et interface. |
| S20 | Une mise à jour du catalogue ou une politique d'organisation invalide un projet ; une ancienne révision reste publiable. | Montrer l'impact ; appliquer une règle explicite aux révisions historiques : politique actuelle, exception approuvée ou reproduction encadrée. | L'approbation d'une révision ancienne peut-elle contourner une nouvelle interdiction de sécurité ? |
| S21 | Une restauration historique est préparée, puis le catalogue change avant son application. | Recalculer l'aperçu, conserver l'ancien état pour comparaison et demander une nouvelle validation si l'effet change. | Fixer les versions du modèle, des politiques et du catalogue utilisées pour chaque opération. |
| S22 | API CI indisponible, événements dupliqués ou reçus dans le désordre ; commit de merge différent de celui de la PR IFS. | Afficher la fraîcheur, dédupliquer et corréler via empreinte des fichiers/artefacts ; ne pas déduire la révision du seul nom du run. | Définir la preuve de correspondance entre révision, commit, artefact et cible. |
| S23 | Une release applicative est verte avec image de démarrage, mais sa base ou son coffre reste inaccessible. | Distinguer infrastructure créée, code livré et dépendances fonctionnelles vérifiées. | Le succès d'activation doit inclure une vérification utile de l'application témoin. |
| S24 | Connexion faible, session expirée, navigateur hors support, petit écran ou navigation clavier. | Préserver la saisie, indiquer clairement le support, permettre lecture des états et des impacts sans survol/couleur seuls. | Définir le minimum mobile, la durée de récupération de saisie et le parcours après reconnexion. |
| S25 | Script ou agent gratuit multiplie organisations, projets ou requêtes de compilation. | Quotas agrégés, file équitable, limites de durée et notification de limitation ; les autres clients restent servis. | Fixer une politique d'usage gratuit et les plafonds d'essai sans interdire les automatisations légitimes. |
| S26 | Suspension pour impayé ou passage d'Équipe à Découverte alors que les quotas sont dépassés. | Lecture et export maintenus selon la politique annoncée ; aucun retrait de données automatique ; créations bloquées de façon prévisible. | Définir délais de grâce, facturation, conservation et réactivation. |
| S27 | Dernier propriétaire absent, compte Entra désactivé, invitation acceptée avec un compte invité ou un alias différent. | Processus d'identification et de récupération contrôlé ; aucune substitution fondée uniquement sur un texte d'e-mail. | Définir identité stable, validation d'invitation et recours quand aucun administrateur n'est joignable. |
| S28 | Import d'une ressource existante avec une propriété ARM non représentée, ou migration depuis une pile encore active. | Afficher les pertes possibles, vérifier toute modification destructive et désigner une seule chaîne de gestion avant bascule. | Le contrôle des noms seuls ne garantit pas l'absence de perte ; définir les propriétés impérativement conservées et la preuve de transfert. |

Pour les essais de retour applicatif, conserver un artefact identifiable par empreinte, une durée de rétention annoncée et ses prérequis de configuration. Un tag d'image décrit comme « immuable » sans mécanisme de protection ni conservation ne suffit pas à démontrer qu'une ancienne version pourra être relivrée.

Les scénarios S01, S02, S04, S06, S08, S10, S12 et S22 devraient compléter en priorité le projet de référence. Les critères doivent vérifier les effets observables : accès effectivement révoqué, artefact effectivement livré, absence de double écriture, état correctement présenté après échec. Les tests de syntaxe des sorties ne couvrent pas ces garanties.

## 4. Suggestions de fonctionnalités et d'améliorations

Les efforts sont relatifs à cette spec : **faible** = règle/écran local, **moyen** = parcours traversant plusieurs domaines, **élevé** = nouvel état persistant, exécution distribuée ou intégration importante. Ce ne sont pas des estimations de planning.

### 4.1 Indispensables au MVP proposé

| Proposition | Besoin couvert | Valeur attendue | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|---|
| Diagnostic de préparation par cible | Savoir avant modélisation si tenant, permissions, dépôts et politique réseau permettent le parcours. | Réduire les abandons tardifs et rendre la promesse mesurable. | Matrice de compatibilité ; script chez le client ; résultats importables sans secrets. | Moyen | Élevé | Un diagnostic ancien ne doit pas garantir la disponibilité future. |
| Installation en phases avec reprise | Passer réellement de zéro à une application utilisable. | Corriger le principal risque de premier déploiement. | A08, permissions Azure/Entra, journal des phases. | Élevé | Bloquant | Un trop grand nombre de phases devient un orchestrateur général ; limiter au parcours pilote. |
| Cycle explicite de révocation | Retirer un accès en conservant les données. | Confiance dans le câblage automatique sur la durée. | Propriétaire unique des objets dérivés, registre des origines et opérations de retrait. | Élevé | Bloquant | Retirer un droit encore nécessaire ; vérifier toutes ses origines. |
| Chaîne de preuve de livraison | Comprendre ce qui est publié, fusionné, appliqué et fonctionnel. | Reprise et support fiables ; indicateurs crédibles. | Empreintes d'artefacts, événements CI, état par cible et phase. | Élevé | Bloquant | Donner l'impression de connaître Azure en temps réel ; toujours afficher la source et la fraîcheur. |
| Un modèle de départ guidé | Éviter la saisie de tout le projet de référence sans aide. | Activation plus rapide et moins d'erreurs de câblage. | Petit catalogue stable, valeurs obligatoires demandées, coût indicatif expliqué. | Faible à moyen | Élevé | Un gabarit trop riche recrée le coût du catalogue complet. |
| Préparation atomique des changements | Empêcher les travaux incomplets de bloquer ou polluer une livraison. | Collaboration minimale cohérente. | Propositions déjà prévues ; contrôles de concurrence et permissions. | Moyen | Élevé | Ne pas développer un système complet de branches du modèle dès le pilote. |

### 4.2 Forte valeur pour une version ultérieure

| Proposition | Besoin couvert | Valeur attendue | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|---|
| Catalogue d'offres internes publiées par l'équipe plateforme | Un développeur demande « API + base » dans un cadre approuvé, sans attendre la création manuelle du composant de P-02. | Autonomie concrète ; réduction de la charge plateforme. | Modèles versionnés, paramètres autorisés, politique d'accès aux services partagés. | Moyen à élevé | Élevé après pilote | Beaucoup de gouvernance pour peu d'usage si la cible reste individuelle. |
| Contrat de dépendance externe | Décrire hub, coffre, registre ou service déjà géré par une autre équipe. | Accès progressif au marché des infrastructures existantes sans import complet. | Métadonnées déclarées, propriétaire, diagnostics chez le client. | Moyen | Élevé | Données déclaratives périmées ; date et preuve de vérification nécessaires. |
| Migration assistée du catalogue avec diff | Mettre à jour les modules sans mélanger mise à niveau technique et changement métier. | Maintenance prévisible et correctifs urgents plus sûrs. | Versionnement et reproduction des anciennes sorties. | Moyen à élevé | Élevé | Prolifération de versions supportées ; annoncer un cycle de support borné. |
| Coût de changement et ressources conservées | Voir coût estimé ajouté, coût résiduel des ressources détachées et postes non estimés. | Relier décisions de modèle et facture potentielle. | Estimation déjà prévue, inventaire fiable, hypothèses d'usage. | Moyen | Moyen | Fausse précision ; montrer couverture et intervalle, pas seulement un total. |
| Dossier de diagnostic exportable | Aider le support sans ouvrir tout le modèle client. | Résolution plus rapide avec moins d'accès exceptionnel. | Codes d'erreur, étapes, identifiants techniques de corrélation, expurgation. | Moyen | Moyen | Exposition involontaire d'informations dans le diagnostic. |

### 4.3 Différenciantes ou innovantes

| Proposition | Besoin couvert | Valeur attendue | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|---|
| Explication complète d'un accès | Répondre « pourquoi cette application peut-elle lire ce coffre en prod ? ». | Rend le câblage implicite auditable ; utile pour revue et incident. | Graphe d'origines déjà prévu, propriétaires, résultat de déploiement connu. | Moyen | Élevé après socle | Ne pas confondre accès voulu et accès effectif ; afficher les deux niveaux. |
| Plan de changement orienté conséquences | Résumer « perte d'accès », « redémarrage », « données conservées », « coût ajouté », avec les environnements touchés. | Relecture possible sans expertise complète du code généré. | Plan neutre, classification des changements par type, limites explicites. | Moyen à élevé | Élevé | Promettre l'exhaustivité de l'impact applicatif serait excessif. |
| Revue ciblée des propositions IA | Mettre en avant nouvelles autorisations, exposition publique et recréations avant acceptation. | Utiliser l'IA sur des opérations vérifiables et compréhensibles. | Moteur de propositions sûr, règles d'impact, validation serveur. | Moyen | Moyen | Ajouter un score opaque ; préférer faits et conséquences expliqués. |

### 4.4 À éviter ou repousser

| Proposition à repousser | Besoin théorique | Valeur attendue dans le pilote | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|---|
| Six langages Pulumi et migrations génériques | S'adapter à toutes les équipes. | Faible sans clients engagés sur ces langages. | Parité comportementale, états, imports, extensions, maintenance des SDK. | Élevé | Faible | Multiplication des combinaisons et promesse de migration trop large. |
| Graphe éditable | Modéliser visuellement. | La lecture et l'analyse d'impact capturent déjà une grande partie de la valeur. | Accessibilité, grands graphes, édition transactionnelle. | Élevé | Faible | Déplacer la complexité vers une interaction moins prévisible. |
| Import complet ARM/Bicep/Azure avec rapprochement automatique | Reprendre tout un existant. | Incertaine tant que la cible initiale n'est pas validée. | Ressources non couvertes, propriétés non reprises, états propriétaires. | Élevé | Faible pour le pilote | Altération d'une infrastructure existante ; commencer par références externes. |
| Catalogue complet de CI applicative et stratégies avancées | Remplacer la configuration de build existante. | Limitée pour tester conventions et câblage Azure. | Piles applicatives, outils tiers, artefacts, compatibilité des données. | Élevé | Faible pour le pilote | Construction d'un second produit et concurrence avec les pipelines déjà fiables du client. |

## 5. Challenge du MVP

### Le périmètre actuel est-il réellement un MVP ?

**Non, si le MVP désigne le plus petit produit permettant d'obtenir une preuve d'usage et de valeur.** Le lot 1 constitue une première version commerciale ambitieuse. Le découpage en trois jalons aide, mais le premier jalon porte déjà organisation, permissions, onze types, dépendances intercomposants, trois abonnements dans l'exemple, secrets, SQL, deux dépôts, publication, installation, suivi et exploitation.

L'hypothèse principale que je recommande de tester est : **une équipe Azure peut créer puis faire évoluer un service conforme à ses conventions, avec moins de travail manuel et sans retouche des fichiers gérés.** Le maintien après création doit faire partie de l'essai ; sinon on démontre seulement un générateur initial.

### Fonctions supprimables ou reportables sans casser cette proposition

- GitHub comme fournisseur git pendant le pilote ; retenir Azure Repos pour réduire les scénarios d'intégration avec ADO.
- Web App, Function App, Static Web App, Redis et les profils de build en mode Code ; garder une application conteneur.
- Authentification locale et génération de mots de passe ; garder identités managées et un secret de pipeline vers Key Vault.
- Import de paramètres, convertisseur v0 et import d'infrastructure, sauf si un client pilote identifié les rend nécessaires.
- MCP complet, mode direct des agents, recherche globale avancée et graphe éditable.
- Restauration arbitraire de tout le modèle, étiquettes et historique propriété par propriété ; garder audit, versions et diff des changements.
- Publication dans de multiples dépôts et écriture directe dans le pilote ; garder une PR dans un dépôt.
- Facturation automatique et grand catalogue de plans au stade pilote contractualisé ; définir malgré tout les limites d'usage et la sortie des données.
- Prototypes Terraform et GitHub couvrant tout le projet de référence : les remplacer initialement par quelques cas représentatifs bornés, puis élargir lors d'une décision commerciale.

**Condition essentielle :** si les pilotes exigent réseau privé, modules internes ou import, cette réduction n'est pas adaptée. Il faut changer le segment ou remonter le prérequis concerné, pas présenter le lot 1 actuel comme suffisant pour toutes les entreprises.

### Fonctions manquantes qui rendent le MVP incohérent ou inutilisable

1. Un premier déploiement séquencé et reprenable, y compris les droits et secrets.
2. Un retrait d'accès réellement révoqué et une conservation des données explicitement séparée.
3. Un aperçu disponible avant l'approbation, rattaché au contenu réellement déployé.
4. Une autorisation des effets indirects et une séparation des identités de build/déploiement.
5. Une corrélation par cible et artefact, avec états partiels et inconnus.
6. Un mécanisme pour ne pas livrer un travail incomplet d'un autre utilisateur, ou une limitation claire à un éditeur coordinateur.
7. Un parcours de découverte et de génération cohérent en l'absence de connexion git.

Ces éléments apportent surtout des règles et garanties manquantes ; ils n'exigent pas tous de nouveaux écrans.

### Plus petit périmètre livrable recommandé

| Dimension | Pilote proposé |
|---|---|
| Cible | 3 à 5 équipes pilotes qualifiées, développant un nouveau service Azure ; nombre proposé, à ajuster selon accès aux clients. |
| Outils | Bicep + Azure DevOps + Azure Repos, une source AVM épinglée. |
| Tenant / déploiement | Un tenant, deux environnements `dev` et `prd`, approbation en `prd`, topologie réseau explicitement acceptée. |
| Dépôts | Un dépôt ; publication uniquement par PR ; un chemin de sortie stable. |
| Modèle | Un socle et un composant applicatif, mode `PerEnvironment`, conventions, surcharges, présence et ressources externes limitées. |
| Catalogue | Log Analytics, Key Vault, identité affectée, registre, environnement Container Apps et Container App ; ajouter SQL uniquement si un pilote en a besoin pour démontrer sa valeur. |
| Application | Un conteneur témoin, identité managée, secret Key Vault, contrôle de santé utile et artefact identifié de façon immuable. |
| Collaboration | Propriétaire, éditeur, lecteur ; audit ; changements atomiques ; une personne responsable de l'intégration des changements. |
| Livraison | Validation, aperçu, approbation, installation en phases, suivi par cible, reprise après échec et révocation effective. |
| Sortie | Archive du code et export du modèle ; déploiements autonomes si IFS est indisponible. |

**Séquence de démonstration :** créer le service → livrer une première image → changer un paramètre → ajouter un accès → retirer cet accès → faire échouer une phase → reprendre → relivrer une image précédente compatible. Mesurer temps actif, interventions plateforme et retouches manuelles à chaque étape.

### Alternative encore plus simple à expérimenter

Un atelier accompagné utilise le projet de référence et produit une PR dans le dépôt du client. Il mesure d'abord la compréhension du modèle, la qualité du diff et l'effort d'installation. Ce pilote permet de tester le besoin avant de construire l'ensemble de l'administration SaaS.

Pour une équipe qui possède déjà de bons pipelines applicatifs, un autre périmètre réduit consiste à générer l'infrastructure et son contrat de consommation, avec une intégration explicite à ses pipelines existants. Cette alternative doit être proposée aux pilotes : elle pourrait offrir un meilleur rapport valeur/effort que le remplacement de leur chaîne de build.

### Décisions nécessaires avant le démarrage du développement produit

- Segment et cas d'usage initial, prérequis d'accès acceptables et critères d'exclusion du pilote.
- Sémantique de retrait, révocation, restauration et changement de noms pour chaque famille d'objets.
- Propriétaire de chaque objet généré partagé et permissions sur les effets indirects.
- Séquencement d'installation, séparation des identités, approche d'aperçu et approbation.
- Modèle d'état publié/fusionné/appliqué, preuve de version et reprise des effets externes.
- Stratégie de préparation des changements et traitement des versions de catalogue.
- Invariants de livraison, calendrier de preuve, équipe responsable et budget de maintenance.

## 6. Verdict final

### Verdict : non prêt pour le développement

Ce verdict porte sur **l'implémentation complète de la v1 telle qu'elle est écrite**. La spécification est suffisamment détaillée pour lancer des prototypes ciblés de faisabilité et préparer un pilote. Plusieurs règles centrales produisent encore des comportements incompatibles ; les laisser à l'interprétation de l'implémentation ferait choisir des règles produit à travers des correctifs techniques.

La phrase « aucun point ouvert » devrait être remplacée par une liste d'arbitrages à résoudre et de preuves à obtenir. Une décision documentée clôt un débat ; elle ne démontre pas que le comportement promis est réalisable.

### Les cinq risques prioritaires

1. **Révocation et cycle de vie incorrects** : accès conservés après retrait, objets dérivés sans propriétaire et retraits perdus lorsqu'une cible saute une révision — A11, A12, A16.
2. **Premier déploiement incomplet** : ordre coffre/droits/secrets/application, SQL/Graph et préparation des politiques CI — A08, A23.
3. **Approbation sans aperçu fiable** : `what-if` de pile non disponible, approbation avant le stage et changement de contexte entre aperçu et application — A21, A22, S08.
4. **Périmètres de sécurité trop permissifs** : cascade hors composant, élévation via attribution/restauration et identité de build trop puissante — A03, A04, A06.
5. **Périmètre excessif avant preuve de valeur** : nombreux types et variantes, collaboration globale et maintenance du catalogue — A01, A10, A18, A19, A40.

### Les cinq opportunités les plus intéressantes

1. **Faire du changement sûr le cœur du produit** : expliquer avant publication les conséquences, puis montrer ce qui a été appliqué.
2. **Rendre chaque accès explicable et révocable** : origine, bénéficiaire, cible, propriétaire et statut d'application.
3. **Proposer un parcours de départ complet et court** : un service réellement utilisable, puis sa première évolution.
4. **Permettre une adoption progressive dans l'existant** : contrats de ressources externes et raccordement aux pipelines du client.
5. **Transformer le modèle en cadre de revue des changements proposés par l'IA** : les propositions deviennent vérifiables sans obliger l'utilisateur à relire tout le code.

### Les dix questions de clarification les plus importantes

1. Quel client précis utilisera le premier pilote, et quelles contraintes réseau, IaC et CI peut-il accepter ?
2. Le lot 1 doit-il gérer une production réelle, ou uniquement démontrer le parcours sur un abonnement de test ?
3. Retirer une liaison doit-il révoquer automatiquement son droit dans Azure, y compris en production, et selon quel propriétaire ?
4. Quelle version sert de référence aux retraits lorsqu'une cible saute une révision ou qu'une PR n'est pas fusionnée ?
5. Quel séquencement garantit la première création des coffres, droits, secrets, applications et utilisateurs SQL ?
6. Quel aperçu est présenté avant approbation, et comment prouve-t-on qu'il correspond à ce qui sera appliqué ?
7. Une équipe peut-elle obtenir un accès à une ressource d'une autre équipe sans accord de son propriétaire ?
8. Accepte-t-on une identité disposant de Contributor à l'abonnement pour les builds, ou impose-t-on dès le MVP des identités distinctes ?
9. Comment isoler une modification en cours et figer le catalogue pour permettre un correctif urgent reproductible ?
10. Quelles preuves d'usage et quelle économie d'exploitation justifieront le passage du pilote au lot 1 commercial ?

### Plan d'action en trois horizons

| Horizon | Action priorisée | Responsable proposé | Livrable / condition de sortie |
|---|---|---|---|
| Avant cadrage / conception détaillée | P1 — Qualifier les clients pilotes, leurs contraintes et leur processus actuel. | Produit + architecte plateforme | Segment unique, cas d'usage, baseline de temps et conditions d'accès documentées. |
| Avant cadrage / conception détaillée | P2 — Remplacer le lot 1 comme objectif initial par un pilote borné ; chiffrer support, maintenance et essais cloud. | Produit + direction + lead technique | Périmètre accepté, budget et critères d'arrêt ou d'élargissement. |
| Avant cadrage / conception détaillée | P3 — Réouvrir les décisions sur cycle de vie, permissions et preuve de livraison. | Produit + sécurité + architecture | Réponses écrites aux questions 3 à 8 ; propriétaires d'arbitrage nommés. |
| Avant développement | P1 — Prototyper création depuis zéro, retrait RBAC, aperçu/approbation et livraison avec cible en retard. | Architecture + QA | Démonstrations reproductibles sur les outils retenus ; limites intégrées à la spec. |
| Avant développement | P2 — Spécifier états, permissions des effets, idempotence, propriété des objets et versions du catalogue. | Produit + architecture + sécurité | Tables de transitions et critères observables S01–S22 pour le périmètre pilote. |
| Avant développement | P3 — Tester la compréhension du parcours et du diff avec les pilotes. | UX + produit | L'utilisateur identifie sans assistance excessive la prochaine action, son responsable et le risque. |
| Avant développement | P4 — Réconcilier décisions, corpus, catalogue initial et projet de référence. | Responsable de spec + QA | Une seule règle par sujet, renvois corrigés, capacités livrables explicites. |
| Avant mise en production | P1 — Démontrer les scénarios nominaux, de retrait, d'interruption, de concurrence et d'isolation. | QA + sécurité | Aucun bloquant ouvert ; preuves attachées aux exigences critiques. |
| Avant mise en production | P2 — Valider restauration IFS, reprise des opérations externes, support, fraîcheur du suivi et communication incident. | Opérations + support | Exercice de reprise, procédures accessibles et responsabilités convenues. |
| Avant mise en production | P3 — Valider conservation, export/effacement, secrets accidentels et sous-traitants ; effectuer les contrôles de sécurité prévus par la spec. | Sécurité + référent données + juridique | Politique cohérente et procédures effectivement exécutables. |
| Avant mise en production | P4 — Mesurer activation, deuxième modification, support nécessaire et coût par client ; tester les règles de facturation si ouverture payante. | Produit + finance | Décision d'ouverture fondée sur résultats des pilotes et marge attendue. |

**Décision proposée :** lancer les arbitrages et quatre prototypes ciblés, puis développer le pilote réduit lorsque ses invariants de sécurité et de livraison sont démontrés.
