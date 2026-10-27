# Auto-validation R-03 — Revue des preuves

Verdict : VALIDÉ

- **Segment** : P-07, P-08 et revue des résultats P1–P9
- **Branche / pull request** : `impl/preuves` — [PR #2](https://github.com/FlorianDrevet/InfraFlowSculptor/pull/2), vers `impl/socle`
- **Commits** : `91c7710` (retrait du crochet P7) ; consignation et transition R-03 dans les commits de clôture de cette PR.

## Vérifications exécutées

| Commande | Résultat observé |
|---|---|
| `python tools/plan/gate.py lint` | 105 étapes et 18 verrous cohérents ; liens de J0-01 résolus. |
| `python tools/plan/gate.py check` | R-03 autorisée avant auto-validation. |
| `Invoke-Pester -Path reference/release-module/tests -Output Minimal -PassThru` | 147 réussis, 0 échec, 0 ignoré. |
| `Test-ReferencePipelines.ps1` | 25 YAML valides. |
| `Test-ReferenceBicep.ps1` | Core, data, orders et platform compilés, formatés et lintés sans avertissement. |
| `Test-ReferenceDeterminism.ps1` | 79 fichiers conformes à l'encodage et au format déterministes. |
| `Update-ManifestExample.ps1 -Check` | Manifeste cohérent sur 79 fichiers. |
| `Invoke-ScriptAnalyzer -Path reference/pilot/bicep-azdo/.ifs/templates/scripts/IfsRelease.psm1 -IncludeRule PSUseApprovedVerbs` | Aucune violation de la règle ciblée sur le module livré. |
| `git diff --check` | Aucun espace ou marqueur de conflit fautif. `testResults.xml` reste non suivi et hors commit. |
| GitHub Actions, SHA `91c7710` | Run CI [40446458695](https://github.com/FlorianDrevet/InfraFlowSculptor/actions/runs/40446458695) et contrôle Plan [40446458660](https://github.com/FlorianDrevet/InfraFlowSculptor/actions/runs/40446458660) réussis ; tous les jobs requis de la PR sont verts. |

Le run précédent sur l'ancien SHA `be21cd5` avait échoué avant les tests, pendant le téléchargement de Pester depuis PowerShell Gallery. Le run sur `91c7710` a installé les modules et validé tous les jobs.

## Recette exécutée

| Scénario | Action exécutée | Résultat observé | Preuve |
|---|---|---|---|
| P1 — Shop44 | Tentative core #900/#901 KO car le secret requis était absent, avant What-If/écriture ; core #904, data #906, platform #908 et API orders #929 ont suivi après résolution. | PASS après correction — rapports DEV/PRD et santé applicative réussis ; aucune valeur de secret lue. | `resultats.md` ; captures `p1-shop44-*` et `p1-shop44-orders-api-*`. |
| P2 — Shop43 | Reprise après `AuthorizationFailed` #830 avec attribution temporaire bornée ; vérification de la révocation et du refus `denyDelete`. | PASS — #831 réussi ; protection de suppression maintenue. | `resultats.md` ; captures P2 shop43. |
| P3 — Concurrence et dérive | #702 a refusé rev1 devenue obsolète ; #705 a révélé un échec DEV, corrigé puis kit réinstallé avant #712. Message SQL non classé #770 corrigé par PR #77, reprise #778. | PASS avec échecs conservés — #778 n'a modifié ni ressource ni pare-feu. | `resultats.md` ; PR #77 et runs #702/#705/#711/#712/#770/#778. |
| P4 — Cible en retard | #836 a laissé `data-access=Started` après une résolution d'identité non aboutie ; #837 a repris. PRD #840 a été refusée avant toute tâche d'écriture ; #843 a réussi. What-If des piles protégées réconcilié propriété par propriété, sans seuil général de tolérance. | PASS — rev5 réussie DEV/PRD et nettoyage exact achevé. | `resultats.md` ; `P-08-blocage-whatif-shop43-20261026.md`, captures #843 et C5. |
| P5 — Échec partiel | #860 a produit `PartiallyApplied` (`deploy-unit=Done`, `secret-write=Started`) ; reprise #861 avec les mêmes IDs d'opération. | PASS — reprise sans doublon. | `resultats.md` ; captures P5 shop43. |
| P7 — Interruption et reprise | Interruption #933, échec de révocation #959, reprise #960 sous rev3. | PASS partiel — les opérations rev2 `deploy-unit` et `data-access` sont terminées avec les mêmes IDs ; l'opération `revoke` a été créée en rev3 puis terminée. | `resultats.md` ; journal et runs #933/#959/#960. |
| P8 — Changement pendant l'attente | #761 arrêtée sur `data-access=Started` avant Deploy ; #779 a échoué sur la résolution de version avant les tâches ; D3 #780 a réussi après correction. | PASS pour #780 ; #761 et #779 n'ont produit aucune écriture. #748 reste non diagnostiqué selon le plan. | `resultats.md` ; captures #761/#779/#780. |
| P9 — Accès entre composants | Déploiement data #906 et API orders #929 ; contrôle SQL et `/health/dependencies`. | PASS — tables lues, contrôle de moindre privilège réussi, règles firewall temporaires retirées. | `resultats.md` ; captures #906/#929. |
| Procédure T — Azure | Suppression shop43 et shop44 depuis les IDs inventoriés ; comparaison C5/C3 finale. | PASS — shop44 : 144 ressources, 19 groupes, 0 pile, 40 rôles, 7 budgets ; baseline conservée. Deux ressources VPD et leurs rôles restent hors périmètre. | [`P-08-nettoyage-shop44-20261027.md`](P-08-nettoyage-shop44-20261027.md) ; inventaire hors dépôt. |
| Nettoyage Azure DevOps | Désactivation des définitions, suppression des groupes de variables et tentative de retrait standard des connexions WIF. | PASS — 17/16/16 définitions désactivées ; groupes supprimés ; historique conservé. 0 connexion en shop43/shop44 ; 6 restent en shop42. La suppression de `ifs-shop42-dev` a été refusée à cause de credentials fédérés Entra. Aucun run/build, PR ou release classique actif. | État vérifié le 2026-10-27 ; `NEXT.md` et recette R-03. |
| Préparation J0-01 | Contrôle statique des liens et références du plan suivant. | PASS — J0-01 reste exécutable tel qu'écrit ; aucune implémentation J0 commencée. | `python tools/plan/gate.py lint` ; `docs/plan/02-jalon-0-pilote.md`. |

## Écarts

| Étape | Prévu | Réalisé | Raison |
|---|---|---|---|
| P7 | Après l'arrêt, la reprise retrouve également `revoke` en attente dans le journal. | Le journal #933 ne contenait que `deploy-unit=Started` et `data-access=ToDo`; `revoke` a été ajouté en rev3. | La reprise d'un `revoke` déjà inscrit n'est donc pas démontrée. L'écart est reporté dans les résultats, la recette et la référence pilote. |
| P8 | Diagnostiquer tous les KO du segment. | #748 est conservé sans diagnostic. | Le plan demande de le laisser non diagnostiqué ; aucune conclusion n'est inventée. |
| Contrôle de coût | Mesure détaillée quotidienne par groupe après suppression. | La requête par groupe a répondu HTTP 429 ; le contrôle J+1 shop42 était inconclusif avec les données disponibles seulement jusqu'au 24/10. Florian a demandé de clôturer sans attendre un autre jour de publication. | Le total global réel, la prévision et la réserve sont consignés ; aucune nouvelle écriture Azure facturable n'a été lancée. |
| Azure DevOps | Les définitions, groupes variables et connexions restent jusqu'à R-03. | Les définitions sont désactivées et les runs conservés ; les groupes variables sont supprimés. Les six connexions shop42 restent en place ; la suppression de `ifs-shop42-dev` a été refusée. | La conservation des historiques protège les preuves ; ne pas toucher aux credentials fédérés ou aux inscriptions Entra. Aucun `--deep` utilisé. |

## Contrôles de coût et sécurité

- Mesure au 2026-10-27 : `ActualCost` `153,046178599 EUR HT`, prévision restante `31,936758518 EUR`, réserve d'essai `15 EUR`. Total prudent `199,982937117 EUR HT`, soit `0,017062883 EUR` sous l'alerte non plafonnante de `200 EUR`. Les taxes ne sont pas comprises ; la requête détaillée par groupe reste en 429.
- Aucun nouveau déploiement ou update Azure facturable après le nettoyage final. Les tombstones Key Vault sont conservés sans purge ; les ressources et rôles VPD préexistants restent hors périmètre.
- Les valeurs des secrets des groupes shop44 n'ont pas été lues. Les groupes ont été supprimés après les preuves. Aucun secret ni credential n'est ajouté au dépôt ou à la mémoire.
- Les pipelines des trois projets de test sont tous désactivés. Les six connexions WIF shop42 restent présentes ; la suppression standard de `ifs-shop42-dev` a été refusée car ses credentials fédérés Entra existent encore. Aucun changement Entra n'a été fait.

## Risques résiduels

- P7 ne démontre pas la reprise d'une révocation déjà présente au journal ; cette limite reste à couvrir lorsque le produit générera lui-même les releases.
- #748 n'est pas diagnostiqué, conformément à la consigne de recette.
- La projection prudente est très proche de l'alerte de 200 EUR et n'inclut pas les taxes ; aucune action Azure facturable supplémentaire n'est autorisée par cette revue.
- Les preuves P1–P5 et P7–P9 sont prototypées sur la sortie écrite à la main. P6 reste spécifiée et P10 décidée ; elles ne sont pas déclarées vérifiées par IFS.
