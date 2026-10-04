# Jalon 2 — Largeur

> **Niveau : découpé.** Détaillé par Claude au verrou [`R-12`](03-jalon-1-tranche-verticale.md#-r-12--sortie-du-jalon-1).

**But** ([04 § 2.2](../specs/04-perimetre-et-lots.md)) : couvrir tout le catalogue du lot 1 et la collaboration à
plusieurs équipes, avec un modèle principal toujours générable ([DEC-93](../specs/03-decisions.md)).

### J2-01 — Les six types restants du lot 1 et le build en mode Code

| | |
|---|---|
| **Spécifications** | [15 § 3.5-3.8, 3.14, 3.18](../specs/15-catalogue.md) ; [19 § 4](../specs/19-applications-build-et-deploiement.md) ; [16 § 6](../specs/16-liaisons-identites-et-acces.md) (PostgreSQL) ; politique d'accès Redis |
| **Commit** | `feat(catalogue): PostgreSQL, Web App, Function App, plan App Service, stockage, Managed Redis` |

🎯 **Objectif.** Les 17 types du lot 1 ; les applications Code construites par profil de pile.
🔧 **À faire.** Descripteurs vérifiés, enfants (conteneurs, files, tables, partages, règles), accès aux données PostgreSQL,
profils de build (.NET, Node, Angular, Python, Java, PowerShell), extension du module de release et des émetteurs ; mise à
jour de `reference/`.
✅ **Vérification automatique.** Descripteurs, émetteurs, projet de référence étendu.
🧪 **Test manuel.** Recette : une Function App Flex avec stockage hôte par identité, déployée et appelée.

### J2-02 — Authentification locale déconseillée et mots de passe générés

| | |
|---|---|
| **Spécifications** | [DEC-51](../specs/03-decisions.md), [17 § 7](../specs/17-parametres-applicatifs-et-secrets.md), `VAL-SEC-*`, [91 T08, T09](../specs/91-scenarios-critiques.md) |
| **Commit** | `feat(securite): authentification locale déconseillée et mots de passe générés` |

🎯 **Objectif.** Permettre l'authentification locale sans jamais connaître le secret.
🔧 **À faire.** Valeurs **D**, génération par la release du composant du coffre, lecture au stage Aperçu, identifiants
admin du registre, coffres existants (propriétaire unique).
✅ **Vérification automatique.** Pester (génération une seule fois), moteur (`VAL-SEC-ORDRE`, `VAL-SEC-GENERE-EXISTANT`).
🧪 **Test manuel.** Serveur SQL `EntraEtSql` : mot de passe généré au premier déploiement, inchangé au second.

### J2-03 — Équipes, portées par composant, abonnement par composant

| | |
|---|---|
| **Spécifications** | [10 § 3.3, 4.1](../specs/10-organisations-et-acces.md) (RG-ORG-09, 10), [DEC-69](../specs/03-decisions.md), [DEC-89](../specs/03-decisions.md) |
| **Maquette** | [OrgTeams](../design/maquette-v1/preview/OrgTeams.html), [ProjectMembers](../design/maquette-v1/preview/ProjectMembers.html) (portées), [ComponentSettings](../design/maquette-v1/preview/ComponentSettings.html) (abonnement par environnement) |
| **Commit** | `feat(acces): équipes, portées par composant et abonnements par composant` |

🎯 **Objectif.** Plusieurs équipes dans un projet, chacune limitée à ses composants, y compris les effets indirects.
🔧 **À faire.** Équipes, attribution limitée à des composants, contrôle des effets indirects ([DEC-89](../specs/03-decisions.md)),
abonnement et connexion par (composant, environnement), kit étendu.
✅ **Vérification automatique.** [91 T05 préparatoire](../specs/91-scenarios-critiques.md) : suppression en cascade refusée.
🧪 **Test manuel.** bob limité à `orders` ne peut pas supprimer `kv main` (critère 15 de [90 § 4](../specs/90-projet-de-reference.md)).

### 🔒 R-13 — Revue largeur et droits fins

### J2-04 — Versions étiquetées, consultation, comparaison, annulation, restauration

| | |
|---|---|
| **Spécifications** | [31 § 4-6](../specs/31-historique-et-versions.md), [EXG-22](../specs/27-exigences-non-fonctionnelles.md), RG-HIS-12 (expurgation, [DEC-95](../specs/03-decisions.md), [DEC-107](../specs/03-decisions.md)) |
| **Maquette** | [ProjectHistory](../design/maquette-v1/preview/ProjectHistory.html), [HistoryCompare](../design/maquette-v1/preview/HistoryCompare.html) |
| **Commit** | `feat(historique): versions, comparaison, annulation et restauration` |

🎯 **Objectif.** Revenir en arrière sans jamais effacer l'histoire.
🔧 **À faire.** Étiquettes, lecture seule à une version, comparaison, annulation (directe ou proposition en conflit),
restauration par proposition avec plan de retour, expurgation et révisions contaminées.
✅ **Vérification automatique.** EXG-22 (< 3 s), T11.
🧪 **Test manuel.** Supprimer une ressource par erreur puis la restaurer (critère de [31 § 6](../specs/31-historique-et-versions.md)).

### J2-05 — Brouillons

| | |
|---|---|
| **Spécifications** | [31 § 7](../specs/31-historique-et-versions.md) : RG-HIS-10, 11, 13 ; [DEC-93](../specs/03-decisions.md) |
| **Maquette** | [Drafts](../design/maquette-v1/preview/Drafts.html) |
| **Commit** | `feat(historique): brouillons et modèle principal toujours générable` |

🎯 **Objectif.** Préparer sans bloquer les autres ; le modèle principal n'accepte plus une erreur.
🔧 **À faire.** Brouillon (copie logique), validation propre, révision d'essai non publiable, mise à jour avec conflits par
propriété, soumission en proposition, archivage à 90 jours.
✅ **Vérification automatique.** RG-HIS-13 (application refusée si elle ajoute une erreur).
🧪 **Test manuel.** bob prépare un brouillon incomplet ; alice publie une autre modification entre-temps.

### J2-06 — Propositions de modification

| | |
|---|---|
| **Spécifications** | [25 § 4](../specs/25-agent-ia-mcp.md) : UC-MCP-01, 02, RG-MCP-08 à 10 |
| **Maquette** | [Proposals](../design/maquette-v1/preview/Proposals.html), [ProposalReview](../design/maquette-v1/preview/ProposalReview.html) ; zone propositions de [Main](../design/maquette-v1/preview/Main.html) |
| **Commit** | `feat(propositions): propositions de modification et relecture` |

🎯 **Objectif.** Toute modification préparée par un tiers (agent, brouillon, import, restauration) est relue puis appliquée
en une transaction.
🔧 **À faire.** Propositions (commandes ordonnées, version de base, aperçu, états), périmée, mise en tête des nouveaux accès,
écran de relecture.
✅ **Vérification automatique.** Proposition périmée ; application atomique.
🧪 **Test manuel.** Proposition créée par l'API avec un jeton `propose` ; relecture et application par alice.

### J2-07 — Demandes d'accès à double consentement

| | |
|---|---|
| **Spécifications** | [RG-LIA-24](../specs/16-liaisons-identites-et-acces.md), [DEC-104](../specs/03-decisions.md), [91 T05, T06](../specs/91-scenarios-critiques.md) |
| **Commit** | `feat(liaisons): demandes d'accès entre composants` |

🎯 **Objectif.** Ouvrir un accès vers le composant d'une autre équipe sans droits croisés.
🔧 **À faire.** Demande créée par la liaison, approbation côté cible, revérification, chute de l'approbation si l'effet change.
✅ **Vérification automatique.** T05, T06.
🧪 **Test manuel.** Critère 15 de [90 § 4](../specs/90-projet-de-reference.md) complet.

### 🔒 R-14 — Revue historique et collaboration

### J2-08 — Pipelines du client et rapport de livraison

| | |
|---|---|
| **Spécifications** | [RG-APP-22](../specs/19-applications-build-et-deploiement.md), [DEC-109](../specs/03-decisions.md), [91 T15](../specs/91-scenarios-critiques.md) |
| **Commit** | `feat(application): contrat de livraison des pipelines du client` |

🎯 **Objectif.** Une équipe garde sa chaîne de build ; IFS génère l'infrastructure et le contrat.
🔧 **À faire.** Livraison « Pipelines du client », contrat dans `README.ifs.md`, modèle d'étape `ifs-app-report.json`, suivi.
✅ **Vérification automatique.** Instantané du contrat ; T15 simulé.
🧪 **Test manuel.** Pipeline du client minimal publiant le rapport ; le suivi l'affiche.

### J2-09 — Import de paramètres et comparaison de révisions

| | |
|---|---|
| **Spécifications** | [UC-PAR-01](../specs/17-parametres-applicatifs-et-secrets.md), [UC-GEN-03](../specs/21-generation-et-revisions.md) |
| **Commit** | `feat(modele): import de paramètres et comparaison de révisions` |

🎯 **Objectif.** Reprendre la configuration existante d'une application ; comparer deux révisions.
🔧 **À faire.** Lecteurs des cinq formats, valeurs suspectes proposées en secrets **sans** leur valeur, fichier jamais conservé ;
diff de plan puis de fichiers.
✅ **Vérification automatique.** Un test par format ; aucune valeur suspecte en base.
🧪 **Test manuel.** Importer un `appsettings.json` contenant une chaîne de connexion.

### J2-10 — Comptes Microsoft personnels

| | |
|---|---|
| **Spécifications** | [DEC-59](../specs/03-decisions.md), [RG-ORG-01](../specs/10-organisations-et-acces.md), [04 § 2.2](../specs/04-perimetre-et-lots.md) |
| **Commit** | `feat(auth): comptes Microsoft personnels` |

🎯 **Objectif.** Les essais individuels deviennent possibles ; une organisation peut les exclure.
🔧 **À faire.** Inscriptions Entra en `AzureADandPersonalMicrosoftAccount`, revendications des comptes personnels, exclusion par
restriction de tenants.
✅ **Vérification automatique.** Résolveur d'adresse vérifiée (tenant des comptes personnels).
🧪 **Test manuel.** Connexion à l'IFS `dev` avec un compte outlook.com.

### 🔒 R-15 — Sortie du jalon 2

**Claude** : critères du jalon, puis **détaille le jalon 3**.
