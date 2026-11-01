# J0-22 — blocage sur les métadonnées de génération Bicep — 2026-11-01

Blocage résolu : CONFIRMÉ

## Vérifications effectuées

- L’étape J0-22 est `EN_COURS` et dépend de J0-21. Le blocage précédent sur les diagnostics est résolu par J0-21b et consigné dans `J0-21b-correctif-diagnostics-20261101.md`.
- Le test `PilotPlanEmitsBicepFilesThatMatchTheReference` construit maintenant un `ModelSnapshot` pilote avec le catalogue `2026.10-pilot.5`, puis le passe aux vrais `WiringEngine`, `ComponentOrdering`, `NamingEngine` et `DeploymentPlanBuilder`. Le JSON de plan `.3` obsolète a été supprimé.
- Comparaison des fichiers de release et scripts SQL : **2/2 tests réussis** (`ReleaseAndDataAccessFilesMatchTheReference`, `EmitIsDeterministic`).
- Comparaison Bicep complète : le test échoue, avec **22 fichiers Bicep et paramètres de référence non émis** (`core`, `data`, `orders`, `platform`). L’émetteur courant ne produit encore que les fichiers release et scripts SQL.
- Le descripteur `ContainerApp` de `2026.10-pilot.5` expose ses propriétés et `generation.Bicep.map`, mais ne décrit pas la conversion `ingress` → `ingressExternal`, la forme imbriquée des conteneurs (`cpu`, `memoryGi`), ni les valeurs ajoutées par la référence (`image`, sécurité ingress). Un émetteur ne peut pas former ces champs depuis le seul plan sans logique propre au type ou métadonnées déclaratives supplémentaires.
- `ResourcePlan` ne porte aucune relation de dépendance/hébergement. Pour `orders`, la référence génère `environmentResourceId`, les dépendances de modules et les conditions `deploy` à partir de liens Container App → Container Apps Environment. L’émetteur ne peut pas relire `WiringResult` selon RG-GEN-07.
- Les descripteurs `.5` n’exposent pas les expressions Bicep pour les sorties des ressources, les `apiVersion` de ressources `existing`, ni les contrats des modules d’attribution de rôle. `CatalogLoader` ne charge pas `roles.json` dans `CatalogVersion`; la référence comporte aussi une attribution de livraison d’application absente du plan.
- Revue consultative en lecture seule par **Claude Sonnet**, sans Opus : son analyse de la partie `orders` confirme le manque de métadonnées declaratives et de relations de dépendance. Son avis est traité comme un constat à vérifier, pas comme le verdict du gate.

Ces éléments rendent incompatibles, avec les entrées actuelles, les trois contraintes de J0-22 : génération octet pour octet, traduction pure du plan (RG-GEN-07) et correspondances de propriétés issues des descripteurs sans noms en dur.

## Décision et correctif

La recommandation de Claude Sonnet était un correctif amont J0-21c : publier une nouvelle version de catalogue sans modifier rétroactivement `.5`, y ajouter les métadonnées déclaratives Bicep manquantes, charger les rôles dans le catalogue et ajouter au plan les relations de ressources indépendantes du langage. Florian a autorisé ce correctif le 2026-11-01.

Le correctif est réalisé : le catalogue immuable `2026.10-pilot.6` décrit les conversions, valeurs additionnelles, expressions, déclarations imbriquées et contrats de rôle; `CatalogLoader` expose les rôles; `ResourcePlan.Dependencies` transporte les relations résolues sans dépendance au langage. L’émetteur Bicep traduit le plan et les métadonnées sans relire `WiringResult` ni coder les types de ressources. Les catalogues `.3` et `.5` restent inchangés.

## Vérifications effectuées

- `PilotPlanEmitsBicepFilesThatMatchTheReference` passe : les fichiers Bicep et paramètres des quatre composants correspondent octet pour octet à `reference/pilot/bicep-azdo/`; les JSON de release et scripts SQL gardent leur parité, et le test de déterminisme passe.
- TDD des constats de la revue finale précédente : le test du seeder échoue avec deux workspaces Log Analytics (`Sequence contains more than one matching element`); le test de description échoue sur l’apostrophe et l’interpolation `${...}` non échappées. Après correction, chacun passe **1/1**.
- Suite backend complète : `dotnet test src/backend/InfraFlowSculptor.slnx --no-restore --logger "console;verbosity=minimal"` — **741 réussis, 1 ignoré, 0 échec** (Domain 132, Catalog 40, Engine 224, Emitters 45 + 1 ignoré, Application 75, Architecture 9, Infrastructure 81, API 132, Acceptance 3).
- Build strict : `dotnet build src/backend/InfraFlowSculptor.slnx --no-restore --configuration Release -warnaserror` — **0 avertissement, 0 erreur**.
- Test de génération et validation par le Bicep CLI épinglé (`IFS_RUN_BICEP_CLI_TESTS=1`, Bicep 0.47.16) — **1/1 réussi**; couvre `build`, `lint`, `build-params` et `format` sur les fichiers émis.
- `pwsh reference/tools/Test-ReferenceBicep.ps1` — `core`, `data`, `orders`, `platform` valides, formatés et sans avertissement.
- `git diff --check` — propre (les avertissements CRLF/LF de Git ne sont pas des erreurs de whitespace).
- `python -m graphify update .` a extrait 1 592 fichiers, puis échoué à produire le rapport HTML parce que le graphe contient 5 698 nœuds, au-dessus de la limite de visualisation de 5 000. Le rapport HTML n’est donc pas déclaré actualisé.
- La suite exécute également `GenerateOpenApiDocuments`; les documents OpenAPI générés restent inclus dans les changements correspondants.
- Le plan J0-22 ne demande pas de recette manuelle distincte : la comparaison automatique à la référence est la preuve prévue.
- Aucun appel ni changement Azure, aucune ressource externe et aucun coût.

## Revue Sonnet et auto-vérification des constats

- La revue consultative précédente avait signalé l’absence supposée de `roles.json`/`pins.json` dans les catalogues antérieurs, un doute sur la validation ignorée par `GetDeploymentPlan`, le chargement des ressources EF sans `Include`, la cardinalité des workspaces, l’échappement des descriptions de livraison, une cible de déploiement nullable et l’exposition du plan au droit `ProjectRead`.
- Faux positif catalogue : contrôle sur disque confirmé, `roles.json` et `pins.json` existent pour `2026.10-pilot` et `.1` à `.6`.
- Doute `GetDeploymentPlan` non retenu pour J0-22 : la route expose le plan déterministe prévu par RG-GEN-06; la validation est fournie séparément et l’émetteur refuse les entrées qu’il ne sait pas traduire via les métadonnées du catalogue. Aucun changement de comportement d’API n’est spécifié par J0-22.
- Chargement/cardinalité du seeder confirmé puis corrigé : `Resources` est explicitement inclus et seul `LogAnalyticsWorkspace/main` est utilisé pour le rattrapage; le test avec un workspace secondaire confirme le résultat et l’absence d’exception.
- Échappement confirmé puis corrigé : les descriptions de paramètres passent maintenant par `BicepCatalogMetadata.SingleQuoted`; le test cible apostrophe, interpolation, antislash et retour à la ligne.
- Fausse alerte connexion : `ComponentSingleTarget.Create` normalise une connexion vide ou nulle vers `ifs-{project}-{code}` et vérifie sa longueur.
- Accès au plan non retenu : l’API applique `ProjectRead` et le contrôle project-scoped; RG-GEN-06 prévoit explicitement l’exposition du plan. Les valeurs secrètes elles-mêmes restent exclues des réglages du plan.
- Une nouvelle revue Sonnet après ces deux correctifs a été tentée en lecture seule avec `claude -p --restricted --model sonnet`; le CLI a refusé la requête avec « You've hit your 5-hour limit · resets 8pm (Europe/Paris) ». Aucun appel Opus n’a été fait. Les constats des revues précédentes ont été vérifiés, corrigés ou documentés ci-dessus. J0-22 n’est pas un verrou de revue; la prochaine revue obligatoire du plan est R-06 après J0-25.

Le correctif amont a été intégré à la tranche J0-22 car le gate J0-21 était déjà clos; l'écart d'ordre est explicité au lieu de réécrire l'historique de J0-21.
