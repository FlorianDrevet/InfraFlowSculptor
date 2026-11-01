# Auto-validation R-05 — Revue du modèle et du moteur

Verdict : VALIDÉ

- **Segment** : J0-06 à J0-20 — verrou R-05
- **Branche / pull request** : `impl/j0-modele` — sans PR
- **Commit de base** : `fd361b2` (avant le diff R-05).

## Vérifications exécutées

| Commande | Résultat observé |
|---|---|
| `git pull` | Branche déjà à jour en début de session. |
| `python tools/plan/gate.py check` | Avant la résolution, code 3 : le blocage M13 était la seule condition restante. Après consignation de la preuve, `unblock R-05` puis `auto-review R-05` lèvent le blocage et valident le verrou. |
| `dotnet build src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore --warnaserror` | Réussi : 0 avertissement, 0 erreur. |
| `dotnet test src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore` | 683/683 : Domain 132, Catalog 39, Engine 213, Application 75, Infrastructure 81, Architecture 9, API 131, Acceptance 3. Zéro échec ou saut. |
| `dotnet test ...Engine.Tests.csproj ... --filter FullyQualifiedName~ResourceConstraintRuleDoesNotRejectAnUnverifiedProfileOnAnExistingEnvironment` | 1/1 après correction. Le test échoue avant le garde pour un environnement existant avec `ExistingIds[dev]`, puis passe après; le nouveau nom `existingIds` est compilé et vérifié par la relance ciblée. |
| `dotnet test ...Api.Tests.csproj ... --filter FullyQualifiedName~ResourceApplicationRequestTests` | 5/5 : collections null rejetées, liste vide acceptée. |
| `dotnet test ...Catalog.Tests.csproj ... --filter FullyQualifiedName~SqlServerEntraAdministratorUsesTypedPropertiesAndAnExplicitBicepObjectMapping` | 1/1; assertions regex comportementales alignées sur le motif portable du catalogue (noms réservés rejetés, `sqladmin` accepté, saut de ligne final rejeté). |
| `dotnet test ...Api.Tests.csproj ... --filter 'FullyQualifiedName~ImplicitDiagnosticLinkDoesNotHideExplicitReplacementOption|FullyQualifiedName~SqlDatabaseHostingIsLimitedToItsComponent'` | 2/2. La version qui recomptait les liaisons Diagnostics implicites faisait échouer le test; le comptage des seuls liens explicites passe les deux scénarios. |
| `dotnet test ...Engine.Tests.csproj ... --filter FullyQualifiedName~RequiredSqlHostingLinkDoesNotOfferAnIncompatibleCrossComponentFix` | 1/1; un constat de portée regroupe deux liens invalides et le constat obligatoire reste unique malgré deux environnements. |
| `npm test` dans `src/frontend/ifs-web` | 199 tests passent : Angular 188, tokens 3, icônes 3, normaliseur API 1, motifs de catalogue 4. |
| `npm run lint` | ESLint et i18n passent; 815 clés FR/EN présentes et utilisées. |
| `npm run tokens:check` / `npm run api:check` | Tokens actuels; OpenAPI 3.1.1 et client généré vérifié (150 modèles, 8 services). |
| `npm run build` | Réussi. Avertissements de budgets : bundle initial 516,13 kB/500 kB; SCSS `resource-type-picker` 4,17 kB, `project-naming` 4,76 kB, `resource-links-panel` 5,38 kB, `resource-editor` 7,98 kB et `resource-create` 6,57 kB (budget de 4 kB). |
| `python -m unittest discover -s tools/plan/tests -v` | 12/12. |
| `python tools/plan/gate.py lint` | Plan cohérent : 105 étapes dont 18 verrous. |
| `python tools/plan/gate.py unblock R-05 docs/plan/revues/R-05-resolution-20261101.md` | Réussi après vérification de la preuve M13; le statut est revenu à `EN_COURS`. |
| `python tools/plan/gate.py check` | Réussi après résolution; le travail sur R-05 est autorisé. |
| `python tools/plan/gate.py auto-review R-05` | Réussi après la recette et l'auto-revue; le verrou est validé et l'étape courante devient J0-21. |
| `git diff --check` / `git diff --cached --check` | Aucun défaut d'espacement ni marqueur de conflit; seules les alertes de normalisation CRLF/LF attendues sous Windows apparaissent. |
| `python -m graphify update .` | Extraction mise à jour sur 1 477 fichiers : 5 265 nœuds, 11 813 arêtes, 828 communautés. Le rendu HTML échoue car le graphe dépasse la limite de 5 000 nœuds. |

Les validations frontend ont été rejouées durant cette reprise après le motif SQL portable : `npm test`, lint/i18n, contrôle OpenAPI, tokens et build passent. Le build conserve les dépassements de budgets Angular consignés ci-dessous. Les tests Playwright M12/M13 restent ceux consignés le 2026-10-30; aucun parcours E2E n'a été relancé durant cette reprise.

## Revue Claude CLI

- **Modèle** : Claude Sonnet explicitement demandé à chaque appel; Opus n'a pas été utilisé.
- **Commandes et bases** : revue complète en lecture seule sur le diff de `HEAD` (`fd361b2`) contre l'arbre courant, avec le contenu des sources/catalogues/tests nouveaux non suivis (seuls les fichiers de schéma Azure Pipelines générés étaient exclus), transmise à `claude -p --model sonnet --restricted`. Une revue de suivi ciblée a relu `ResourceConstraintRule`, ses tests, le contrat des étapes, RG-RES-10 et l'invariant de `LinkAggregate`, avec le même modèle et le même mode restreint. Claude n'a exécuté aucun test; Codex les a exécutés. Aucun Opus ni modèle de repli utilisé.
- **Constats confirmés et corrections** :
  - La validation n'émettait pas `VAL-LIA-COMPOSANT` pour une liaison hors composant. La règle enregistrée par DI est ajoutée avec test.
  - La correction en un clic pouvait ajouter une liaison obligatoire à côté d'une liaison invalide existante. Elle est maintenant proposée seulement quand la cardinalité et le scénario le permettent.
  - `PreviewLink` utilisait les rôles bruts; il passe par la même normalisation que POST/PUT. Le test de rôles avec espaces échouait avant correction puis passe.
  - Preview pouvait accepter `identityResourceId` avec `identityKind=None`, alors que l'agrégat rejetait ensuite la liaison. Le garde-fou partagé renvoie maintenant `LINK_IDENTITY_PARAMETER_INVALID`; les tests couvrent POST et Preview.
  - Une base avec plusieurs liaisons d'hébergement hors composant recevait plusieurs constats identiques. `LinkComponentScopeRule` les regroupe par ressource et joint les IDs; test à deux liaisons et deux environnements.
  - GET options pouvait proposer un ajout que POST refusait à cause d'une liaison explicite héritée invalide. Les options comptent tous les liens explicites persistés, même invalides, comme POST/PUT. Un test reproduit `LINK_CARDINALITY_EXCEEDED`, puis vérifie le retrait de l'ancien lien, le retour de la cible locale et l'ajout réussi.
- **Constats vérifiés non actionnables dans le périmètre** :
  - Les liens implicites ne comptent pas dans POST/PUT. Diagnostics implicite peut être remplacé par un explicite; le test dédié échoue avec l'ancien comptage et passe avec le comptage explicite seul.
  - PUT exclut le lien qu'il modifie. GET options est une route d'ajout; l'interface n'a pas d'édition de lien et expose la suppression des liens explicites.
  - `VAL-LIA-OBLIGATOIRE` décrit l'absence de liaison valide; `VAL-LIA-COMPOSANT` décrit la liaison existante invalide. Les deux cas sont distincts. Les tests vérifient un constat unique de chaque type.
  - Le tri des IDs est déterministe (`Guid` comparé par son comparateur par défaut). `RoleAssignmentPolicy.IsForbiddenRole` applique lui-même `Trim()`.
  - L'agrégat refuse déjà `identityResourceId` avec `None`/`System`; la garde d'application uniformise le code d'erreur. Le chemin `SourceActsAsIdentity` pour une identité comme source est couvert par les tests API existants.
- **Solution de conception Claude Sonnet, appliquée à la demande explicite de Florian** : DSL déclaratif fermé avec `compare` et `allowedTuples`, évalué par le catalogue sur les valeurs effectives; les violations deviennent des constats par cible et l'édition n'est pas rejetée pour une relation croisée, ce qui permet les modifications progressives. Les règles CPU/mémoire, min/max réplicas et tuples SKU sont déclarées dans les catalogues pilotes. Les specs et décisions protégées n'ont pas été modifiées.
- Une revue finale du diff complet a identifié un faux constat `allowedFrom` pour un environnement `IsExisting`; le défaut est confirmé et corrigé (garde avant lecture des valeurs effectives) avec test rouge/vert portant un `ExistingId` présent. Le second examen Sonnet de suivi conclut à **aucun défaut actionnable nouveau ou restant** sur ce correctif.
- Faux positifs et lacunes vérifiés : `.4` n'est pas dans `HEAD` (le manifeste de `HEAD` ne contient que `.0`–`.3`); l'invariant d'agrégat interdit déjà un `identityResourceId` résiduel pour `System`; `steps` est requis dans OpenAPI et les tests distinguent collection `null` et `[]`; la suppression de l'implicite Diagnostics lors d'un lien explicite est faite dans le handler; les tests de portée couvrent l'absence de dépendance et le groupement des IDs; le seeder du projet de référence reste épinglé volontairement sur une version historique.
- Les contraintes croisées d'items restent évaluées par le moteur au niveau du projet, pas au moment de chaque écriture : c'est le comportement choisi, pour permettre la correction progressive. Les contrôles de forme/type de la valeur restent évalués à l'écriture.
- **Conclusion de revue de code** : aucun autre défaut actionnable confirmé après vérification et exécution des tests.

## Recette exécutée

| Scénario | Action exécutée | Résultat observé | Preuve |
|---|---|---|---|
| M1–M11 | Résultats locaux et captures déjà consignés aux étapes J0-06 à J0-20 | Vérifiés selon le suivi existant; non rejoués durant cette reprise | [`suivi.md`](../recettes/suivi.md), captures R-05 |
| M12 | Playwright mobile 390 × 844 sur les vues d'ensemble, ressource et constats | PASS, 1/1; pas de défilement horizontal | Captures `R05-M12-Overview-390.png`, `R05-M12-Resource-390.png`, `R05-M12-Findings-390.png` |
| M13 automatisé | Playwright bureau et mobile; Chloé n'a aucun contrôle de gestion et la commande de rôle répond `403 PROJECT_PERMISSION_REQUIRED` | PASS, 2/2; résultat auparavant consigné | `project-reader.spec.ts` et tests API d'autorisation |
| M13 manuel Scalar | Connexion de Chloé dans Scalar, lecture des droits et commande de rôle prévue vers Bob | PASS : `GET /v1/me` confirme Chloé; droits projet `project.read` uniquement; POST unique avec `Auditor`, portée `Project`, version attendue 1661 répond 403 `PROJECT_PERMISSION_REQUIRED`, permission `members.manage`. | [Preuve de résolution R-05](R-05-resolution-20261101.md); affichage de la réponse dans Scalar |
| Vérifications Azure | Aucune action Azure n'est requise par R-05 | Aucun changement, coût ou ressource créée | Aucune écriture Azure |

## Écarts

| Étape | Prévu | Réalisé | Raison |
|---|---|---|---|
| R-05 / M13 | Envoyer depuis Scalar la commande prévue sous Chloé | Scalar confirme 403 `PROJECT_PERMISSION_REQUIRED` pour `members.manage`; le parcours Playwright bureau/mobile confirme aussi l'absence de contrôle de gestion (2/2) | Le mot de passe de test déjà présent dans le realm Keycloak local a été utilisé; aucune réinitialisation ni modification n'a été nécessaire. La valeur n'a pas été consignée. |
| R-05 / contraintes croisées | Validation des règles des specs 14, 15 et 20 | DSL fermé `compare`/`allowedTuples` implémenté; constats par cible effective | Solution recommandée par Claude Sonnet à la demande de Florian; aucune modification des documents protégés. |
| R-05 / résultat pilote | Résultats du projet pilote identiques à `reference-pilote.md` | Le texte de `docs/plan/reference-pilote.md` est aligné sur DT-42 : A, B et C sont le même abonnement pour les preuves, donc aucun finding de portée externe n'est attendu pour le pilote | La formulation antérieure contredisait la décision de portée existante; le dossier `reference/`, les specs et les décisions n'ont pas été modifiés. |
| Graphify | Actualiser le graphe et son rapport | Extraction faite; rendu HTML non produit | 5 171 nœuds dépassent la limite de rendu 5 000. |

## Contrôles de coût et sécurité

- Aucune écriture Azure, Azure DevOps, Entra ou dépôt externe; coût incrémental nul.
- Aucun secret, jeton ou mot de passe ajouté au dépôt, à la documentation ou à la mémoire.
- Conteneur PostgreSQL IFS relancé et monté sur le volume existant `ifs-postgres-17-data`; aucun volume ni projet de recette n'a été supprimé ou réinitialisé. Les AppHosts Vole-Papillon autorisés à l'arrêt restent arrêtés; le conteneur Keycloak conflictuel a été arrêté sans suppression.
- Jeton OAuth effacé de Scalar après la recette; aucun secret, jeton ou mot de passe ajouté au dépôt, à la documentation ou à la mémoire.

## Risques résiduels

- Le rendu Graphify HTML reste indisponible au-dessus de 5 000 nœuds (5 265 nœuds extraits); l'extraction AST fonctionne. Cette limite de visualisation n'affecte pas le verrou R-05.

## Reprise et résolution M13 — 2026-11-01

L'AppHost IFS a été relancé en mode persistant avec le volume PostgreSQL existant. `/alive` répond 200, `/scalar` redirige vers `/scalar/` (200), et la découverte OIDC du realm `ifs` répond 200. La session OAuth locale confirme Chloé par `GET /v1/me` (`Chloe Petit`, `chloe@contoso.example`). Les droits de Chloé sur `shop` sont `project.read` seuls, version 1661; la liste des membres Contoso confirme Bob Bernard.

Le POST prévu dans M13 a été envoyé une seule fois depuis Scalar pour attribuer `Auditor` à Bob dans le projet `shop`, avec la portée `Project` et la version attendue 1661. La réponse affichée est HTTP 403 `PROJECT_PERMISSION_REQUIRED`, permission `members.manage`. Voir [la preuve de résolution](R-05-resolution-20261101.md). Le mot de passe était déjà défini dans le realm local de test; il n'a pas été modifié. Le jeton OAuth a été effacé de Scalar après la vérification.

Les AppHosts Vole-Papillon restent arrêtés; le volume PostgreSQL n'a pas été modifié ni supprimé. Aucun changement Azure ou coût. `git diff --check`, `git diff --cached --check` et `gate.py lint` restent propres; les tests de code et le build complets R-05 déjà consignés ci-dessus n'ont pas été relancés, car aucun code de production n'a changé depuis. Le rendu Graphify dépasse toujours sa limite documentée de 5 000 nœuds, sans effet sur R-05.
