# Revue du plan d’implémentation — 4 octobre 2026

Revue demandée par l’utilisateur, réalisée par Luna. Ce document n’est pas une revue de verrou et ne vaut pas approbation d’une étape.

Version examinée : branche locale `docs/plan-implementation`, commit `92d07b5`. Le `git pull` initial a échoué faute de branche distante de suivi. Le plan, les spécifications et la conception technique n’ont pas été modifiés.

## Portée et résultat

Lecture des phases S, P, J0, J1, J2, J3 et de la feuille de route des lots suivants, du projet pilote de référence, des recettes associées et du mécanisme de progression. Confrontation aux spécifications et décisions techniques pertinentes. Les jalons annoncés comme « découpés » et les lots volontairement grossiers ne sont pas jugés sur l’absence de classes ou de commandes détaillées.

**18 constats** : 12 à traiter avant les étapes concernées (priorité P1), 6 incohérences de séquencement ou de vérification à corriger (P2). Les corrections ci-dessous sont des demandes de précision pour Claude, pas des décisions de conception appliquées.

Le plan couvre les principales fonctions, mais plusieurs critères ne peuvent pas être satisfaits dans l’ordre prescrit. Les points les plus sensibles concernent les preuves Azure, la capture des révisions asynchrones et les garanties transverses du socle.

Contrôles réalisés :

- `python tools/plan/gate.py check` : code 0, étape S-01 autorisée.
- `python tools/plan/gate.py lint` : code 0, 105 étapes dont 18 verrous.
- Lecture de l’ordre produit par `load_steps()` : `J1-10 → J1-11 → J1-12 → R-11 → R-12`.
- Vérification documentaire Microsoft des droits SQL et du comportement de `SessionIdleTimeout`.
- Aucun déploiement, aucune exécution des futures recettes : leurs problèmes sont des constats documentaires, pas des échecs observés sur Azure.

## Constats prioritaires

### PLAN-01 — P1 — Le contrôle SQL du témoin exige des droits que le pilote ne lui attribue pas

**Références :** [P-01](../plan/01-preuves.md#p-01--application-témoin), ligne 47 ; [référence pilote § 2.3](../plan/reference-pilote.md#23-éléments-implicites-par-environnement), ligne 133.

P-01 demande au contrôle de santé SQL de créer une table si elle n’existe pas, puis d’insérer et de lire une ligne avec l’identité système de l’application. La référence attribue à cette identité seulement `db_datareader` et `db_datawriter`.

Ces rôles couvrent la lecture et la modification des données ; créer une table persistante exige `CREATE TABLE` et un droit `ALTER` sur son schéma. Sur une base neuve, le témoin peut donc échouer alors que la liaison « LectureÉcriture » a été correctement câblée. Sources : [rôles de base de données](https://learn.microsoft.com/en-us/sql/relational-databases/security/authentication-access/database-level-roles?view=sql-server-ver17), [permissions de CREATE TABLE](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql?view=sql-server-ver17#permissions).

**À préciser :** faire préparer le schéma du témoin par une identité autorisée, puis vérifier les seuls droits promis à l’application. Ne pas élargir les droits applicatifs pour rendre le test vert.

**Vérification attendue :** base initialement vide, préparation du témoin, puis lecture/écriture réussie avec les seuls rôles prévus ; création d’une autre table refusée.

### PLAN-02 — P1 — Les secrets attendus, leur propriétaire et leur injection dans les stages se contredisent

**Références :** [P-02](../plan/01-preuves.md#p-02--bicep-du-projet-pilote-écrit-à-la-main), ligne 123 ; [P-03](../plan/01-preuves.md#p-03--module-de-release-powershell), ligne 171 ; [P-04](../plan/01-preuves.md#p-04--pipelines-azure-devops-du-projet-pilote), lignes 230–234 et 253.

Le `release.dev.json` d’`orders` contient `MAIN_PAYMENTS_API_KEY` dans `expectedSecrets`, avec `writer: core`. P-03 exige que chaque `expectedSecrets.variable` existe dans l’environnement du processus. Pourtant, le test manuel P-04 exige que cette variable n’apparaisse que dans `core`, son écrivain. Appliquer les trois instructions bloque la release d’`orders`.

De plus, le groupe de variables et le mapping `env:` sont explicitement placés dans `Deploy_<cible>`, alors que `Test-IfsSecretVariable` appartient à l’étape 3 du stage `Preview_<cible>`.

**À préciser :** distinguer les secrets que le composant écrit des références qu’il consomme, et définir exactement les variables accessibles à chaque stage. Aligner le schéma, l’exemple et le YAML sur ce contrat.

**Vérification attendue :** aperçu de `core` avec variable présente/absente ; aperçu et déploiement d’`orders` sans recevoir la valeur secrète écrite par `core`.

### PLAN-03 — P1 — Le stage Aperçu ouvre un journal en écriture avant l’approbation

**Références :** [P-03](../plan/01-preuves.md#p-03--module-de-release-powershell), ligne 168 ; [spécification des stages](../specs/22-pipelines.md#31-release-dinfrastructure-dune-cible--aperçu-puis-déploiement).

`Open-IfsOperationJournal` est affecté aux étapes **1 et 6**, avec création du blob absent et prise d’un bail exclusif renouvelé. L’étape 1 appartient à l’Aperçu ; la spécification lui impose de ne rien modifier. Sur la première exécution, l’aperçu crée donc un état Azure avant approbation. Le partage de cette fonction laisse également indéfinie la durée du bail pendant l’attente humaine.

**À préciser :** le contrat de lecture de l’Aperçu, le traitement d’un journal absent et le moment exact où commencent création, bail et écritures sous le stage Déploiement.

**Vérification attendue :** aucun appel d’écriture ni acquisition de bail pendant l’aperçu, journal absent compris ; écritures protégées pendant le déploiement.

### PLAN-04 — P1 — J0-05 requiert un projet persistant avant l’étape qui crée le modèle et sa persistance

**Références :** [J0-05](../plan/02-jalon-0-pilote.md#j0-05--permissions-rôles-de-projet-et-autorisation), lignes 200–206 ; [J0-07](../plan/02-jalon-0-pilote.md#j0-07--projets-et-environnements-api) ; [recette R-04, A14](../plan/recettes/02-jalon-0.md).

J0-05 reconnaît que les projets n’existent qu’en J0-07, mais demande de créer un projet « directement en base par la fixture ». Le modèle `ProjectAggregate` et ses champs n’arrivent qu’en J0-07. La fixture ne résout pas l’absence de contrat de persistance. La recette R-04 exige aussi un projet de démonstration pour appeler `/permissions/me`, avant que J0-07 soit autorisée.

**Conséquence :** pour terminer le segment accès, Luna doit anticiper une partie de J0-07 ou inventer une représentation provisoire, deux choix que le plan lui interdit de faire seule.

**À préciser :** avancer explicitement la fondation minimale du projet ou déplacer les commandes, contrôles et recettes qui dépendent de son existence. La recette R-04 doit être réalisable avec les seules étapes qu’elle clôt.

### PLAN-05 — P1 — La génération asynchrone ne prévoit pas comment conserver la version demandée

**Références :** [J0-24](../plan/02-jalon-0-pilote.md#j0-24--révisions--génération-asynchrone-contrôles-de-sortie-consultation), lignes 768–775 ; [technique, révision](../technique/03-moteur-et-generation.md#6-révision) ; [J1-03](../plan/03-jalon-1-tranche-verticale.md#j1-03--historique-du-modèle--jeux-de-modifications-et-auteur-par-propriété).

La conception demande au worker de charger l’instantané **à la version demandée**. J0-24 met un travail en file et conserve un numéro de version, mais ne définit ni la capture transactionnelle ni le stockage de cet instantané. J0-11 construit un instantané depuis la base courante ; l’historique permettant de reconstruire les anciennes versions n’arrive qu’en J1-03.

**Scénario manquant :** demande sur v10, modification du projet en v11 pendant l’attente, puis démarrage du worker. Sans conservation de l’entrée, il ne peut plus générer v10 et risque d’associer les fichiers de v11 aux métadonnées de v10.

**À préciser :** quand et où sont figés le modèle complet, le catalogue, le langage et la plateforme ; comment cette capture est liée atomiquement à la révision et à l’outbox.

**Vérification attendue :** suspendre le worker, demander une génération, modifier le modèle, reprendre le worker et vérifier que fichiers et métadonnées correspondent à la même entrée figée.

### PLAN-06 — P1 — L’équité des files n’est pas garantie par le mécanisme ni par le test prévus

**Références :** [S-08](../plan/00-socle.md#s-08--worker-outbox-et-files-à-sessions), lignes 470–488 ; [DT-08 et DT-32](../technique/01-decisions.md#dt-32--files-et-équité) ; [EXG-23](../specs/27-exigences-non-fonctionnelles.md).

Le plan utilise huit sessions simultanées et `SessionIdleTimeout = 5 s`, puis teste deux organisations. Ce test démontre que deux sessions peuvent progresser en parallèle. Il ne démontre pas une rotation quand les huit places sont occupées.

Microsoft définit ce délai comme l’attente d’un **nouveau message** : une session alimentée continuellement n’est pas inactive. On en déduit que huit organisations ayant toujours des messages peuvent conserver les huit places et retarder une neuvième, malgré le test vert. Source : [contrat de SessionIdleTimeout](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebussessionprocessoroptions.sessionidletimeout?view=azure-dotnet).

**À préciser :** le mécanisme qui assure une rotation bornée sous saturation, ainsi que l’effet des budgets de DT-32 sur cette rotation. Cette correction touche aussi la justification technique de DT-08.

**Vérification attendue :** saturer toutes les sessions disponibles, continuer à les alimenter, puis introduire une autre organisation et borner son délai de prise en charge.

### PLAN-07 — P1 — Le schéma d’audit conserve le nom de l’auteur dans une table qu’il interdit de modifier

**Références :** [J0-04](../plan/02-jalon-0-pilote.md#j0-04--journal-daudit-écriture-et-consultation-simple), lignes 159–165 ; [DT-25](../technique/01-decisions.md#dt-25--journal-daudit-en-ajout-seul-données-personnelles-à-part) ; [J1-12](../plan/03-jalon-1-tranche-verticale.md#j1-12--exigences-transverses-avant-le-pilote-élargi).

J0-04 place `actor_display` directement dans `audit_events`, puis interdit `UPDATE` et `DELETE`. DT-25 prévoit une pseudonymisation par suppression d’une table de correspondance. Si le nom reste copié dans chaque événement, supprimer la correspondance ne suffit pas ; le modifier viole le déclencheur prévu.

**Conséquence :** J1-12 ne peut pas respecter simultanément le schéma prescrit et sa recette de suppression du compte avec audit pseudonymisé. C’est une contradiction interne au contrat du produit, sans besoin d’interprétation juridique.

**À préciser :** la représentation de l’acteur dès J0-04 et l’emplacement des informations identifiantes. Étendre le contrôle aux différences avant/après susceptibles de contenir des données de profil.

**Vérification attendue :** après suppression du compte, la consultation et le stockage de l’audit ne conservent pas le nom via `actor_display`, tout en conservant la trace des actes prévue par la spec.

### PLAN-08 — P1 — La télémétrie du pilote démarre avant le contrôle de désactivation promis

**Références :** [J0-01](../plan/02-jalon-0-pilote.md#j0-01--profils-dutilisateur-et-organisations), ligne 33 ; [J0-32](../plan/02-jalon-0-pilote.md#j0-32--accueil-parcours-complet-et-préparation-du-pilote), ligne 1061 ; [DEC-83](../specs/03-decisions.md#dec-83--télémétrie-produit).

J0-32 active la télémétrie produit pseudonymisée. J0-01 exclut le réglage de télémétrie jusqu’à J1 ; aucune étape J1 n’attribue explicitement la réalisation de ce réglage. DEC-83 permet pourtant à l’administrateur de désactiver cette collecte.

**À préciser :** prévoir le contrôle serveur et l’interface au plus tard lors de l’activation de la collecte, ainsi que sa conservation de 13 mois.

**Vérification attendue :** désactiver la télémétrie pour une organisation empêche ses événements produit d’être émis, sans supprimer les traces de sécurité prévues.

### PLAN-09 — P1 — Le jalon 1 exige un critère livré seulement au jalon 2

**Références :** [critère de sortie J1](../plan/03-jalon-1-tranche-verticale.md), ligne 11 ; [référence complète, critère 15](../specs/90-projet-de-reference.md#4-critères-dacceptation), ligne 341 ; [J2-03 et J2-07](../plan/04-jalon-2-largeur.md).

Le plan exige les **18 critères** du projet de référence à la sortie de J1. Le critère 15 nécessite les droits limités à un composant et la création d’une demande d’accès vers un autre composant. Ces fonctions arrivent en J2-03 et J2-07, qui annonce lui-même le critère 15 « complet ».

**Conséquence :** R-12 ne peut pas être approuvé à la lettre, tandis que J2 ne peut commencer avant son approbation.

**À préciser :** la liste exacte des critères exigibles à R-12 et ceux reportés à R-15, avec une version intermédiaire explicite si nécessaire.

### PLAN-10 — P1 — P-08 emploie un état bloquant sans définir sa procédure de reprise compatible avec AGENTS.md

**Références :** [P-08](../plan/01-preuves.md#p-08--exécution-des-preuves-accompagnement), lignes 394–397 ; [AGENTS.md](../../AGENTS.md), lignes 13–17 ; [skill executer-etape](../../.agents/skills/executer-etape/SKILL.md), ouverture.

P-08 demande à Luna de passer à `BLOQUE` en attendant les résultats utilisateur, puis de repasser elle-même à `EN_COURS` quand ils arrivent. Or l’ouverture obligatoire impose `gate.py check` puis, sur code 3, « ne fais rien d’autre » et arrêt. `BLOQUE` produit précisément ce code.

Une instruction explicite de l’utilisateur peut lever cette ambiguïté, mais le cycle autonome décrit dans le plan ne prévoit pas comment le faire dans le respect du contrôle initial.

**À préciser :** un état d’attente de recette distinct ou une procédure explicite de reprise autorisée avant le contrôle, avec l’acteur habilité. Ne pas désactiver globalement les verrous de revue.

**Vérification attendue :** nouvelle session après un résultat KO, après un résultat partiel et après la fin de recette : à chaque fois, une seule conduite autorisée et documentée.

### PLAN-11 — P1 — Le job e2e obligatoire n’a pas d’étape de réalisation

**Références :** [S-15](../plan/00-socle.md#s-15--intégration-continue), ligne 778 ; [CI cible](../technique/06-tests-et-qualite.md#6-ci-githubworkflowsciyml).

S-15 reporte les jobs `e2e`, `release-module` et `images` à leurs étapes futures. P-06 ajoute explicitement `release-module`, J0-31 exige `images`, mais aucune étape J0 n’ajoute le job Playwright contre l’AppHost. Les tests e2e sont écrits et lancés localement à plusieurs étapes, ce qui ne crée pas leur exécution en CI.

La conception technique rend pourtant ce job bloquant dès J0. Une implémentation littérale peut donc conserver une CI verte avec un parcours utilisateur cassé.

**À préciser :** l’étape responsable du job, le démarrage et l’arrêt de l’AppHost en CI, les données, ports et artefacts nécessaires.

**Vérification attendue :** une régression volontaire d’un parcours fait échouer le job distant ; captures et traces Playwright sont récupérables.

### PLAN-12 — P1 — La restauration et les brouillons utilisent les propositions avant leur réalisation

**Références :** [J2-04, J2-05, J2-06](../plan/04-jalon-2-largeur.md), lignes 51–92.

J2-04 doit restaurer via une proposition et créer une proposition en cas d’annulation conflictuelle. J2-05 doit soumettre les brouillons en propositions. Le modèle, les commandes, les états et l’application atomique des propositions sont créés seulement en J2-06.

Même si ces étapes doivent être détaillées à R-12, leur ordre actuel impose soit une anticipation, soit des étapes déclarées terminées sans leur comportement essentiel.

**À préciser :** placer le socle des propositions avant ses consommateurs, puis ordonner leurs interfaces et recettes. Le détail futur doit reprendre un graphe de dépendances réalisable.

## Incohérences de séquencement et de vérification

### PLAN-13 — P2 — Le socle du moteur est créé après ses premiers consommateurs

**Références :** [J0-08](../plan/02-jalon-0-pilote.md#j0-08--assistant-de-création-de-projet), lignes 310–312 ; [J0-11](../plan/02-jalon-0-pilote.md#j0-11--ressources--propriétés-surcharges-présence-existantes-enfants), lignes 404–406 ; [J0-12](../plan/02-jalon-0-pilote.md#j0-12--moteur--cibles-présences-valeurs-effectives-et-tags), ligne 430.

J0-08 calcule déjà un aperçu par le moteur, J0-11 produit son type `ModelSnapshot`, mais J0-12 crée le projet `InfraFlowSculptor.Engine`. Le moteur de nommage provisoire est autorisé ; son projet d’accueil et les contrats partagés ne le sont pas explicitement.

**À préciser :** une étape de création des projets et contrats avant leur premier usage, distincte de l’implémentation des calculs complets. Cela évite de déplacer les types ou d’inventer une couche provisoire.

### PLAN-14 — P2 — R-11 est positionné après une étape qu’il est censé précéder

**Références :** [fin du jalon 1](../plan/03-jalon-1-tranche-verticale.md), lignes 195–215 ; [parseur du plan](../../tools/plan/gate.py).

L’ordre réellement lu est `J1-10 → J1-11 → J1-12 → R-11 → R-12`. Pourtant R-11 ne couvre que J1-10 et J1-11, et R-12 couvre J1-12. Il n’y a donc aucun travail entre les deux verrous, et J1-12 est exécutée avant la revue du segment qui la précède conceptuellement.

**À préciser :** positionner le titre de R-11 au bon endroit et renseigner la branche ainsi que la recette de la suite, ou assumer explicitement un seul segment/revue pour ces étapes.

### PLAN-15 — P2 — La preuve P3 ne définit pas la mutation qui doit invalider l’aperçu

**Références :** [référence pilote, critère 9](../plan/reference-pilote.md#4-critères-dacceptation-du-jalon-0), ligne 224 ; [P-04](../plan/01-preuves.md#p-04--pipelines-azure-devops-du-projet-pilote) ; [DEC-102](../specs/03-decisions.md#dec-102--ce-que-couvre-une-approbation).

Le critère exige qu’un commit modifiant un rôle, fusionné pendant l’approbation, fasse échouer la release en attente. Or la release est déclenchée par une CI qui produit un artefact `infra`. Le seul nouveau commit ne précise ni une modification d’Azure ni le remplacement de l’artefact que la release en cours applique.

**Conséquence :** une release relisant le même artefact contre un état Azure inchangé peut conserver une empreinte identique tout en respectant DEC-102. Le résultat attendu du test dépend d’un comportement qui n’est pas défini.

**À préciser :** quel artefact est figé dans le run et quelle modification effective intervient avant le recalcul. Si l’on veut aussi invalider les runs devenus anciens après une nouvelle fusion, spécifier ce mécanisme séparément.

### PLAN-16 — P2 — La preuve P4 omet le déploiement initial de l’objet qu’elle attend de détacher

**Référence :** [référence pilote, critère 10](../plan/reference-pilote.md#4-critères-dacceptation-du-jalon-0), lignes 226–228 ; reprise dans la recette R-08, L5/L6.

Le scénario dit que la révision 3 ajoute `id extra`, que la révision 4 la retire et se déploie seulement en dev, puis que la révision 5 se déploie partout. Il ne demande jamais de déployer la révision 3 en prd. Si prd est restée à la révision 1, l’identité n’y a jamais existé et ne peut pas apparaître dans l’inventaire des ressources détachées.

**À préciser :** déployer d’abord la révision qui crée l’objet sur les deux cibles, constater sa présence dans la pile prd, puis faire sauter uniquement la révision de retrait à prd.

### PLAN-17 — P2 — La comparaison intégrale à la référence inclut un fichier qui ne doit pas être généré

**Références :** [P-06](../plan/01-preuves.md#p-06--readmeifsmd-manifeste-dexemple-et-contrôles-en-ci) ; [J0-23](../plan/02-jalon-0-pilote.md#j0-23--émetteurs-azure-devops-kit-dinstallation-et-readme) ; [J0-24](../plan/02-jalon-0-pilote.md#j0-24--révisions--génération-asynchrone-contrôles-de-sortie-consultation) ; [référence pilote § 3](../plan/reference-pilote.md#3-sortie-attendue-dépôt-shop).

P-06 place `.ifs/manifest.example.json` dans `reference/pilot/bicep-azdo/`. J0-23 exige une égalité de **toute l’arborescence** ; J0-24 précise que le manifeste est absent de l’archive, et la conception réserve sa production à la publication.

**À préciser :** isoler les fichiers de recette hors du dossier comparé, ou nommer explicitement les exclusions du comparateur et de la recette G3. L’exception doit être identique pour les tests automatiques et la comparaison manuelle.

### PLAN-18 — P2 — Le garde-fou ne distingue pas un jalon détaillé d’un jalon seulement découpé

**Références :** [README du plan](../plan/README.md), lignes 40–42 ; [gate.py](../../tools/plan/gate.py), `cmd_lint` et `cmd_check` ; en-tête du [jalon 1](../plan/03-jalon-1-tranche-verticale.md).

Le README présente le contrôle des rubriques comme la garantie qu’une étape insuffisamment détaillée ne peut pas être exécutée. Mais J1, J2 et J3 contiennent déjà les trois marqueurs requis `🔧`, `✅`, `🧪`. Le lint passe aujourd’hui sur les 105 étapes. `check` contrôle l’état et les verrous, pas le niveau de détail.

**Conséquence :** le verrou protège bien l’attente de la revue, mais si Claude lève R-08 sans terminer le détail, aucun contrôle mécanique ne distingue ce cas d’un plan exécutable. La garantie de détail repose actuellement sur la procédure humaine.

**À préciser :** soit décrire honnêtement cette garantie comme procédurale, soit ajouter un état explicite de préparation des jalons contrôlé avant leur exécution. Le simple ajout de rubriques ne mesure pas le détail.

## Points à conserver dans la préparation des corrections

Trois incohérences secondaires méritent d’être traitées avec les étapes concernées, sans en faire de nouveaux constats prioritaires :

- **Exception R-02 :** le plan maintient la même branche et la PR ouverte jusqu’à R-03, contrairement au cycle générique « nouvelle branche depuis main après un verrou ». Cette exception est explicite dans P ; la faire aussi apparaître dans la procédure de reprise évitera de perdre du temps à la résoudre.
- **Enregistrements Azure DevOps :** J0-30 exige des réponses réelles enregistrées pendant P, mais P-07/P-08 demandent surtout liens, extraits de rapports et captures. Prévoir précisément la collecte des réponses nécessaires à WireMock, leur anonymisation et leur emplacement, pendant que les runs sont disponibles.
- **Build Code en J1/J2 :** la contradiction de périmètre est déjà reconnue au début de J1 et reportée à R-08. Elle reste à arbitrer ; ce n’est pas un oubli nouveau découvert par cette revue.

## Ordre proposé pour la reprise par Claude

1. Corriger les preuves et leurs contrats avant de figer la sortie de référence : PLAN-01 à 03, 15 à 17.
2. Compléter les garanties du socle et du pilote avant leurs implémentations : PLAN-04 à 08, 11, 13.
3. Rendre la progression cohérente : PLAN-09, 10, 12, 14 et 18.

Les futures revues de verrou pourront alors vérifier des critères réalisables, sans devoir choisir entre suivre une étape et respecter sa spécification.
