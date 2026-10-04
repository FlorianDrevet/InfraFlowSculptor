# Recette du socle (verrou R-01)

**Durée estimée** : 45 minutes. **État de départ** : un clone à jour de la branche `impl/socle`, Docker Desktop démarré.

## 1. Machine et garde-fous

| # | Action | Attendu |
|---|---|---|
| 1.1 | `pwsh tools/dev/check-prereqs.ps1` | Toutes les lignes `OK` |
| 1.2 | `python tools/plan/gate.py status` | Étape courante `R-01`, statut `EN_ATTENTE_DE_REVUE` |
| 1.3 | Créer un fichier `src/essai.txt`, `git add src/essai.txt`, `git commit -m test` | Commit **refusé** : « Revue en attente : commit refusé pour src/essai.txt » |
| 1.4 | `git reset src/essai.txt` puis supprimer le fichier | Dépôt propre |

## 2. Démarrage local

| # | Action | Attendu |
|---|---|---|
| 2.1 | `cd src/backend ; aspire run` | Tableau de bord ouvert ; en moins de 3 min, toutes les ressources « Running » / « Healthy » : postgres, storage, servicebus, redis, mailpit, keycloak, gitea, api, worker, web |
| 2.2 | Lien `pgweb` | Base `ifs`, tables en `snake_case`, migrations `S_06_Socle` et `S_08_Jobs` |
| 2.3 | Lien `mailpit` | Interface vide |
| 2.4 | Lien `redis` (RedisInsight) | Connexion établie |
| 2.5 | `pwsh tools/dev/gitea-init.ps1` puis `http://localhost:3000` | Organisation `contoso`, dépôts `shop`, `shop-infra`, `shop-app` ; seconde exécution sans erreur |

## 3. Connexion et API

| # | Action | Attendu |
|---|---|---|
| 3.1 | `http://localhost:4200` | Page de connexion identique à `docs/design/maquette-v1/preview/Login.html` |
| 3.2 | Se connecter en `alice@contoso.example` | Coquille, « Bonjour Alice Martin » |
| 3.3 | Fenêtre privée, `nina@contoso.example` ; puis Scalar de l'API, `GET /v1/me` en nina | `verifiedEmail: null` |
| 3.4 | Scalar sans autorisation, `GET /v1/me` | 401, corps `problem+json` |
| 3.5 | Scalar en alice, `POST /v1/dev/ping-job` | 202 ; journaux du worker : « PingJob … traité » ; trace reliant api et worker |
| 3.6 | Réduire la fenêtre à 390 px | Barre latérale remplacée par un bouton menu ; aucun défilement horizontal |
| 3.7 | Arrêter la ressource `api`, recharger l'application | Page d'erreur avec une référence |

## 4. Design system

| # | Action | Attendu |
|---|---|---|
| 4.1 | `docs/design/strata/index.html` à côté de `http://localhost:4200/dev/design-system` | Mêmes couleurs, mêmes noms de tokens |
| 4.2 | Pour chaque fichier `docs/design/strata/rendered/*.html`, la section correspondante de la galerie | Même apparence (hauteurs, rayons, couleurs, icônes) |
| 4.3 | Galerie au clavier seul | Tout est atteignable, focus toujours visible |
| 4.4 | Onglet Réseau des outils de développement, recharger | Aucune requête vers Google Fonts |

## 5. CI

| # | Action | Attendu |
|---|---|---|
| 5.1 | GitHub → Actions, dernier run de `impl/socle` | Tous les jobs verts, dont `plan` |
| 5.2 | Clone neuf, section « Démarrer » du README seule | « Bonjour Alice » atteint sans autre information |
