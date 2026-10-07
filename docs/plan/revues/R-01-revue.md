# Revue R-01 — Revue du socle

Verdict : APPROUVÉ

- **Relu** : segment S-01 → S-17, commits `3d7fe87..bb31d18`; demande du 2026-10-06; recette du 2026-10-06 : partielle, avec contrôles Rider et clone neuf reportés dans `NEXT.md`.
- **Relecture Claude Code** : modèle CLI `claude-sonnet-5`; avis final ciblé sur les corrections R-01 (`cda435c..b00d64a`) et les preuves de la demande. La tentative d'analyse exhaustive du diff historique a atteint le plafond de tours; les étapes antérieures disposaient de leurs validations et revues ciblées. Aucun outil d'écriture n'était activé.
- **Vérifications relancées** : build .NET (15 projets, 0 avertissement/erreur), tests .NET hors Acceptance (58 réussis), suites frontend (35 Angular + 3 tokens + 3 icônes), lint/build frontend, prérequis (10/10), tests du garde-fou (7 réussis), `gate.py lint`, `git diff --check`. La PR #1 sur `bb31d18` a 14/14 contrôles CI verts, y compris Acceptance et E2E.

## Constats

### INFO-1 — Politique d'avertissement OIDC liée aux fournisseurs présents

- Fichier : `src/frontend/ifs-web/src/app/core/auth/oidc.providers.ts`
- `disableRefreshTokenOfflineAccessScopeWarning: !isEntra` est correct pour les deux fournisseurs actuels (Entra et Keycloak). L'ajout futur d'un autre fournisseur OIDC devra expliciter sa politique de jetons hors ligne.
- Aucun changement requis pour R-01.

### INFO-2 — Texte d'erreur de l'émulateur Service Bus reconnu par sous-chaîne

- Fichier : `src/backend/InfraFlowSculptor.Worker/Services/ServiceBusProcessorErrorClassifier.cs`
- La sous-chaîne anglaise exacte peut évoluer avec l'émulateur ou le SDK. Le filtre reste limité à l'émulateur local, à `AcceptSession`, à une exception transitoire `GeneralError` et au message attendu; tout autre événement reste au niveau Error. Une évolution du texte fera donc perdre la suppression du bruit sans masquer silencieusement une erreur.
- À surveiller lors d'une mise à jour du SDK/émulateur; aucune correction avant merge.

### INFO-3 — Cinq colonnes ResourceIcon dans l'application contre six dans la référence

- Fichier : `src/frontend/ifs-web/src/app/features/dev/design-system/design-system-components.css`
- L'application limite le contenu à 1216 px; la référence est pleine largeur. Les cinq colonnes gardent les 25 libellés lisibles à 1440 px et le layout mobile reste sans débordement. Écart de reflow attendu, sans correction.

### INFO-4 — Acceptance locale et E2E local partiels

- Le runner Acceptance local attendait une ressource DCP `Waiting`; l'arrêt du runner a conservé l'AppHost principal et ses conteneurs.
- Le dernier E2E local a passé les deux scénarios publics et ignoré les quatre scénarios authentifiés faute de variable `IFS_E2E_PASSWORD`. Le run CI du commit exact `bb31d18` a réussi les checks Acceptance et E2E. Pas de défaut bloquant.

## Décisions prises pendant la revue

- La mitigation Keycloak est validée par l'A/B, mais la cause interne exacte de `invalid_token` n'est pas établie. La documentation le dit explicitement et ne confond pas le flux Authorization Code + PKCE avec l'issue hybride amont.
- La dette S-08 (seam E2E pour le report de budget/dead-letter et la boucle hébergée de renouvellement du runner) ne bloque pas R-01 : les chemins de réussite, l'équité, l'outbox, le bail atomique et le seuil d'échec définitif ont leurs couvertures. La dette reste ouverte et devra être planifiée dans une étape Worker dédiée; R-01 ne la marque pas comme soldée.
- Les contrôles Rider et le clone README seul restent en attente dans `NEXT.md`; ils ne changent pas le résultat des builds, de la suite Acceptance CI ni de l'E2E CI du head examiné.

## Pour la suite

R-01 est approuvé; l'étape courante devient P-01 — Application témoin. La PR [#1](https://github.com/FlorianDrevet/InfraFlowSculptor/pull/1) attend la fusion conformément au plan. Les conteneurs Aspire locaux restent actifs.
