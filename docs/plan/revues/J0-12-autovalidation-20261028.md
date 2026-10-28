# Auto-validation J0-12 — cibles, présences, valeurs effectives et tags

Verdict : VALIDÉ

- **Segment** : J0-12
- **Branche / pull request** : `impl/j0-modele` — PR à ouvrir après le push
- **Commits** : à compléter après le commit de l'étape

## Vérifications exécutées

| Commande | Résultat observé |
|---|---|
| `dotnet build src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore` | Réussi, 0 avertissement, 0 erreur |
| `dotnet test src/backend/InfraFlowSculptor.slnx --configuration Release --no-build` | 335/335 : Domain 85, Catalog 7, Engine 43, Application 27, Infrastructure 67, Architecture 9, API 94, Acceptance 3 |
| `npm test` | 82 tests frontend réussis (75 Angular, 3 tokens, 3 icônes, 1 normaliseur) |
| `npm run lint` | Réussi, 447 clés traduites |
| `npm run build` | Réussi ; avertissement de budget connu : bundle initial 505,13 kB, 5,13 kB au-dessus de 500 kB |
| `npm run api:check` | Client OpenAPI à jour, 77 modèles et 8 services |
| `python tools/plan/gate.py lint` | Plan cohérent : 105 étapes, 18 verrous |
| `python -m graphify update .` | Réussi : 3 288 nœuds, 6 106 arêtes, 519 communautés |
| `git diff --check` et `git diff --cached --check` | Réussis |

## Revue Claude CLI

- **Commande et base** : revues read-only via `git diff --no-ext-diff --unified=25 origin/impl/j0-acces -- <fichiers>` transmis à `claude -p --restricted`; base du segment `origin/impl/j0-acces`.
- **Portée** : snapshots, résolveurs de cibles/tags/valeurs, endpoint, tests Engine/API, client OpenAPI généré.
- **Constats et traitement** :
  - Claude a recommandé de traiter `requiredWhen:` comme une condition d'applicabilité, comme `requires:`. Choix retenu, cohérent avec la définition de Condition dans RG-RES-02 et les descripteurs `LogAnalyticsWorkspace` et `SqlServer`.
  - Les surcharges d'une propriété `overridable: false` étaient appliquées : corrigé et couvert par `IgnoresStoredOverrideForNonOverridableProperty`.
  - Les préfixes de condition inconnus, dépendances inconnues/vides et cycles pouvaient masquer des propriétés : ils sont désormais rejetés par erreur de descripteur, avec tests.
  - Les conditions en chaîne lisaient la valeur d'une dépendance déjà masquée et les nombres étaient comparés selon leur texte JSON : corrigé; les tests couvrent les chaînes de dépendances, `1.0` contre `1`, les nombres extrêmes et le débordement d'exposant.
  - Le contrat « a une valeur » conserve `false`, `0` et la chaîne vide comme valeurs définies. Les comparaisons avec `=` sont limitées aux scalaires; les tableaux et objets ne sont pas assimilés à une chaîne vide. Ces choix sont testés.
  - Descripteur avec `properties` absent, définition sans nom/non objet et nom non textuel : erreur `ArgumentException` désormais uniforme, avec test.
  - La comparaison numérique BigInteger répétait des divisions pour normaliser la valeur et pouvait devenir quadratique sur une longue entrée : remplacée par une normalisation linéaire de chaîne, avec un test de condition sur 10 000 zéros.
  - Le builder de test écrasait la valeur `systemTagsEnabled=false` fournie au projet avec son défaut privé : corrigé en rendant l'override explicite et couvert par un test.
  - Le signalement `api.ts` absent est faux positif : ce fichier n'est pas une sortie générée attendue; `npm run api:check` valide le client effectivement généré (77 modèles, 8 services).
  - La dernière revue a relevé que la validation des propriétés est court-circuitée pour une ressource existante. Constat non retenu : le fournisseur valide le catalogue complet au chargement, et le contrat J0-12 demande explicitement une liste vide pour une ressource existante. Le chemin ne calcule ni n'expose les propriétés du catalogue dans ce cas.
  - Les erreurs d'invariant (descripteur mal formé, dépendance inconnue/cycle ou identifiant vide) peuvent encore remonter jusqu'au handler. Pas de changement : le catalogue est validé au chargement et les identifiants proviennent du modèle validé; risque résiduel faible limité à une donnée interne incohérente.
  - Le mapping de production de `ModelSnapshotBuilder` est explicite et `TargetsAllEnvironments=false` représente une sélection ciblée valide; pas de changement. Aucun défaut bloquant ou moyen confirmé ne reste. Les tests ont été exécutés séparément par Codex après les revues.

## Recette exécutée

| Scénario | Action exécutée | Résultat observé | Preuve |
|---|---|---|---|
| Contrat `GET /v1/projects/{projectId}/resources/{resourceId}/effective-values` | Avec Alice sur Keycloak local, création d'un projet temporaire, composant `Core`, cibles `dev`/`prd`, groupe `main` et Key Vault `main`; lecture Scalar après configuration | GET 200 : `dev` est présent en `francecentral`, `purgeProtection=false`/`override`; `prd` est présent en `francecentral`, `purgeProtection=true`/`resource`. Les deux renvoient leurs tags système; `sku=standard` et `softDeleteRetentionInDays=90` sont `catalogDefault`. Le projet temporaire a ensuite été supprimé logiquement en 204, version 12. | Réponse Scalar GET 200 et PUT propriétés 200; DELETE projet 204; ligne J0-12 dans `docs/plan/recettes/suivi.md` |
| Protection irréversible `purgeProtection` | Essai direct de passage de la valeur catalogue par défaut `true` à la propriété de ressource `false`, puis séquence proposée par Claude : base `false`, surcharge `dev=false`, base `true` | L'essai direct est refusé en 400 `RESOURCE_PROPERTY_IRREVERSIBLE`. La séquence valide les deux mutations en 200 et produit le contrat demandé sans désactivation : `dev` reste `false`, `prd` passe uniquement de `false` à `true`. | Réponses Scalar 400/200; résultat GET ci-dessus |

## Écarts

| Étape | Prévu | Réalisé | Raison |
|---|---|---|---|
| J0-12, ordre de configuration | Scalar : `kv main`, valeur `dev` surchargée, valeur `prd` issue de la ressource et défaut à 90 | L'ordre littéral de l'essai direct est refusé par l'irréversibilité; l'ordre base false → surcharge dev false → base true atteint exactement le contrat attendu, chaque transition restant false→true ou sans changement | Proposition de Claude vérifiée par Codex sur le projet local temporaire; réponse 400 pour l'essai direct, PUT 200 et GET 200 après séquençage |

## Contrôles de coût et sécurité

- Coût réel / prévision / estimation incrémentale : sans objet, aucun appel ni changement Azure.
- Portée des changements externes : aucun changement externe; vérifications sur le poste local et tests éphémères.
- Secrets, permissions, approbations et nettoyage : aucun secret ni jeton consigné; projet temporaire local supprimé de façon réversible (204, fenêtre de récupération de 30 jours). Aucune ressource Azure créée ou modifiée.

## Risques résiduels

- `provenance` reste une chaîne libre dans OpenAPI, même si les quatre valeurs sont fixées et testées côté serveur.
- La requête construit un snapshot du projet complet; acceptable pour le volume du pilote, à réévaluer avec les volumes réels.
- Des exceptions d'invariants peuvent remonter au handler si une donnée interne incohérente contourne la validation normale du catalogue/modèle.
- Le build Angular dépasse le budget initial de 5,13 kB; l'étape passe, mais le dépassement reste visible.
