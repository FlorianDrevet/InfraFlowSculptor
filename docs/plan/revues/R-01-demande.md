# Demande de revue R-01 — Revue du socle

- **Segment** : S-01 → S-17
- **Branche / pull request** : `impl/socle` → `main` — PR à créer après le push (aucune PR ouverte actuellement)
- **Commits** : `3d7fe87..b00d64a`

| Étape | Commits du segment |
|---|---|
| S-01 | `3d7fe87` outillage ; `7e531cb` étape terminée |
| S-02 | `9e2ffe0` squelette CQRS ; `6f81553` étape terminée |
| S-03 | `baf78dd` modernisation ; `183e2dd` étape terminée |
| S-04 | `e83d255` tests et architecture ; `fa2d1b9` étape terminée |
| S-05 | `4e4bba0` AppHost et émulateurs ; `16ec1e8` étape terminée |
| S-06 | `119bca7` persistance ; `07ecd12` étape terminée |
| S-07 | `992b2b6` OIDC et utilisateur ; `f03744e` étape terminée |
| S-08 | `a9c5736` worker/outbox ; `e5f8627` étape terminée |
| S-09 | `09d7a79` Angular ; `199437f` étape terminée ; `399df9f` journal |
| S-10 | `c5c65ee` tokens/styles ; `50bf73b` étape terminée |
| S-11 | `4879ded` composants Strata ; `ed67036` étape terminée |
| S-12 | `9a95ae2` icônes et motifs ; `d7d3412` étape terminée |
| S-13 | `26a3252` shell et E2E ; `3373d2d` étape terminée |
| S-14 | `69d33fc` OpenAPI/client ; `f4773a0` étape terminée |
| S-15 | `e8d8059`, `173be58`, `548b840`, `f2231de`, `a3c2299`, `80b2250`, `ce6f280` CI et corrections |
| S-16 | `6e3e086` observabilité ; `5eabeaf` étape terminée |
| S-17 | `4fbd85c` mémoire/graphe ; `5acd5db` étape terminée |
| Correctifs préparés pour R-01 | `cda435c` session OIDC locale ; `b00d64a` libellés ResourceIcon |

## Vérifications exécutées

| Commande / parcours | Résultat |
|---|---|
| `rtk dotnet build src/backend/InfraFlowSculptor.slnx --no-restore --warnaserror -m:1` | 15 projets, 0 erreur, 0 avertissement |
| `rtk dotnet test src/backend/InfraFlowSculptor.slnx --no-build --no-restore -m:1 --filter "Category!=Acceptance"` | 58 réussis, 0 échec (6 projets) |
| `rtk dotnet test …` avec Acceptance en local | Arrêté après attente prolongée de l'API de fixture en état DCP `Waiting`; le runner a été arrêté, les 19 conteneurs de l'AppHost principal sont restés actifs. Les runs CI Acceptance de S-16 étaient verts; le run du présent commit est attendu après push. |
| `rtk npm run lint` | Réussi; 46 clés i18n fr/en présentes et utilisées |
| `rtk npm test` | 35 tests Angular + 3 tests tokens + 3 tests extraction icônes réussis |
| `rtk npm run build` | Réussi; génération de `public/config.json` incluse |
| `rtk npm run e2e` (dernier run local) | 2 tests publics réussis, 4 tests authentifiés ignorés car `IFS_E2E_PASSWORD` n'était pas défini dans ce processus. Un run antérieur après le correctif OIDC avait réussi les 6 scénarios; les secrets E2E de CI sont configurés et le nouveau run CI est attendu. |
| `rtk npm run api:check` / `rtk npm run tokens:check` | Réussis |
| `rtk python tools/plan/gate.py lint` | Réussi : 105 étapes, 18 verrous cohérents |
| `rtk python -m unittest discover tools/plan/tests` | 7 tests réussis |
| `rtk pwsh tools/dev/check-prereqs.ps1` | 10/10 prérequis OK |
| `rtk git diff --check` | Réussi |
| Hook précommit après `rtk python tools/plan/gate.py request R-01` | Commit temporaire de `src/essai.txt` refusé avec le message attendu; fichier et index nettoyés ensuite |
| Parcours visuel Playwright | Les neuf composants S-11 comparés à leurs références à 1440 px via un serveur HTTP local. `ResourceIcon` : les 25 entrées sont lisibles à 1440 et 390 px. À 390 px, la page reste à 390 px et le tableau utilise un défilement interne effectif (354 px visibles, 640 px de contenu). Captures R-01 dans `captures/R-01/`. |
| AppHost et persistance | 19 conteneurs vérifiés `Up`; les ressources de dépendance ont leur cycle persistant entre `aspire stop/start`. L'instance principale n'a pas été arrêtée pour cette revue. |

## Écarts au plan

| Étape | Plan | Réalisé | Raison |
|---|---|---|---|
| S-07/S-09 | Demander `openid profile email offline_access` dans l'application navigateur | La portée par défaut du client Web est maintenant `openid profile email`; `offline_access` reste optionnelle côté realm/client et les rôles seed restent disponibles pour les consommateurs qui en ont réellement besoin | A/B sur Keycloak 26.6.4 : avec `offline_access`, `/userinfo` renvoie 401 `invalid_token` et seule la session hors ligne est présente; sans cette portée, `/userinfo` répond 200 et le login Web réussit. Le test identifie un contournement, pas la cause interne exacte. Le guide Keycloak 26.1 sur les sessions en ligne/hors ligne est référencé dans `docs/technique/09-keycloak.md`. |
| S-11/S-12 | Comparer les références locales à la galerie et vérifier le tableau étroit | Références servies localement en HTTP pour comparaison; le minimum de colonne `ResourceIcon` est aligné sur les 200 px de la référence et son breakpoint mobile garde les libellés lisibles; le tableau défile dans son conteneur | Le contenu de l'application est plafonné à 1216 px, contre une référence pleine largeur : la galerie produit cinq colonnes à 1440 px contre six dans la maquette, sans texte tronqué. |
| S-15/S-16 | Vérifier les Acceptance tests en CI et en local | CI Linux verte lors des étapes précédentes; le run Acceptance local de cette revue a bloqué sur l'état DCP `Waiting` | Le runner local a été interrompu pour préserver l'AppHost principal et ses dépendances persistantes. Le run CI du push R-01 doit confirmer à nouveau Acceptance. |
| S-17 | Démarrer un clone neuf uniquement depuis le README | Vérifications de prérequis terminées; démarrage d'un second clone différé | Les ports fixes 4200, 8080, 5257 et 3000 sont occupés par l'AppHost de travail que l'utilisateur souhaite garder actif. |

## Dette de test ouverte (`.github/test-debt.md`)

S-08 : branches de report de budget, escalade/dead-letter et boucle hébergée de renouvellement du runner `SessionJobProcessorService` encore sans seam de test bout en bout. Merci de décider si cette dette bloque l'approbation de R-01 ou si elle reste une tâche ultérieure; les scénarios de réussite, d'équité, d'outbox, de bail atomique et de seuil d'échec définitif sont couverts.

## Tests manuels préparés

La recette du socle est dans `docs/plan/recettes/00-socle.md` (sections 1 à 5). Les comptes de démonstration et services locaux sont déjà en place. La connexion admin Keycloak a été rétablie par ajout d'un administrateur local sans suppression du volume ni des utilisateurs. Les contrôles navigateur réalisables sont exécutés et consignés dans `docs/plan/recettes/suivi.md`.

Restent impossibles depuis l'automatisation de cette session : ouverture de la solution et du panneau Unit Tests dans Rider (S-02/S-04), et test « README seul » sur un clone neuf pendant que l'AppHost réserve les ports (S-17). Le corps HTTP du 404 S-03 a été vérifié; la navigation directe Chrome demeure un contrôle visuel local. Le run E2E authentifié local nécessite `IFS_E2E_PASSWORD`; CI dispose de son secret masqué.

## Captures

- `captures/R-01/galerie-1440.png`, `captures/R-01/galerie-390.png`
- `captures/R-01/coquille-1440.png`, `captures/R-01/coquille-390.png`

## Points d'attention pour la relecture

- Relire le segment complet S-01 à S-17 par rapport au plan, aux spécifications et aux décisions, puis examiner les deux commits de correction R-01.
- Vérifier que la SPA n'a pas besoin d'un jeton hors ligne et que l'avertissement `offline_access` reste pertinent pour Entra; aucun rôle ni scope optionnel n'a été supprimé du royaume.
- L'événement Keycloak local ne rapporte que `invalid_token`; ne pas présenter l'issue amont #39037 comme cause démontrée, car son flux hybride diffère du Authorization Code + PKCE testé ici.
- Décider explicitement du sort de la dette S-08 mentionnée ci-dessus.
- Le résultat E2E local le plus récent a quatre scénarios ignorés faute de secret dans le processus; vérifier le run CI déclenché par le push avant approbation.
