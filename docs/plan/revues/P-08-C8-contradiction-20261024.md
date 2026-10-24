# P-08 — Contradiction C8 et écritures de données (2026-10-24)

## Constat

C8 reste KO et P-08 reste BLOQUE. La reprise du journal `Started` ne peut pas rendre le critère C8 vrai avec le module contenu dans l’artefact N #759 : l’aperçu compte systématiquement les accès SQL déclarés comme des écritures de données prévues.

## Éléments vérifiés

- L’artefact D3-pre dérivé du CI N #759 identifie `sourceCiRunId=759`, le SHA `492dfa0df7f9c22f182f1fec443f90c041ed823d`, et l’identité de préflight `personal-az-cli`. Le SHA-256 du module livré, `140f6b9f5a7040d82d8bdcb4bb8375f0172e50bf26a38af29b0eb17e0ccefce8`, égale `metadata.inputSha256.module`.
- Le `release.dev.json` de cet artefact a le SHA-256 `cbe3ddddbf6a02aba187fe7f8fabeab5bc7c48ed15a301c20ccff1bb78c6100a` et contient une entrée `dataAccess` : base `sqldb-shop42-orders-dev`, identité système de `ca-shop42-api-dev`, niveau `ReadWrite`.
- Dans `IfsRelease.psm1` livré par N #759, `Invoke-IfsPreview` parcourt chaque entrée `ReleaseData.dataAccess` et ajoute une ligne `database-access` à `dataWriteItems` sans consulter le journal ni l’état SQL. `dataWrites` est ensuite inclus dans l’aperçu et dans l’empreinte des effets.
- Le préflight C4 du 2026-10-23 donne déjà `pendingOperations=1` et `dataWrites=1`. L’aperçu publié du run #761 confirme le même résultat. Marquer l’opération journalisée `Done` peut retirer l’opération en attente, mais ne retire pas cette entrée `dataWrites`.
- Le plan exige pour C8 zéro opération en attente et zéro écriture de données prévue. Le second invariant reste faux tant que le module de l’artefact compte l’accès SQL configuré comme une écriture prévue. Une nouvelle release D3 ne peut donc pas passer C8 par la seule reprise du journal.

## Actions non exécutées

Aucun appel SQL n’a été lancé ; le journal Azure n’a pas été pris sous bail ni modifié ; aucun rôle Azure supplémentaire n’a été attribué ; aucun nouveau run Azure DevOps n’a été lancé. Le module `SqlServer 22.4.5.1` a seulement été installé dans le profil PowerShell local pour préparer la reprise ; aucune connexion à la base n’a eu lieu.

## Question à trancher

Pour permettre une recette D3 vérifiable, quelle règle appliquer ?

1. **Préserver C8.** Faire évoluer la release pour que l’aperçu n’annonce aucune écriture SQL seulement après vérification en lecture seule que l’identité, son SID, le marqueur `ifs-managed` et les rôles attendus sont déjà exacts ; sous le verrou de release, ne pas rejouer le SQL si l’état n’a pas dérivé. Ajouter des tests de dérive et de refus fail-closed.
2. **Autoriser l’écriture idempotente.** Amendement explicite de C8 pour permettre cette opération `data-access` après contrôle de son identité, de ses rôles, de son script transactionnel et de l’absence de toute autre opération ou effet interdit.
3. **Ne pas relancer D3.** Conserver #761 comme résultat KO et laisser P-08 bloqué.

Option 1 conserve l’objectif de C8, mais modifie le comportement de prévisualisation et de reprise ; elle demande une décision de conception avant implémentation. Aucune option n’est appliquée dans cette preuve.

## Contrôles complémentaires

- Cost Management au 2026-10-24T07:33Z : 126,903610 EUR HT MTD ; prévision restante 42,753640 EUR ; projection fin octobre 169,657250 EUR. L’estimation conditionnelle d’une heure de reprise SQL DEV et d’un D3 complet ajoute 7,0691 EUR HT, sans constituer un plafond.
- Resource Graph à 07:36Z : 164 ressources, 26 groupes, 7 piles, 59 attributions de rôles et 7 budgets. Les 21 IDs Shop42 correspondent exactement au snapshot C5 du 2026-10-23. Une nouvelle ressource de déploiement VPD hors Shop42 a été créée par une application à 07:27Z ; elle n’a pas été modifiée.
- Vérifications : Pester de référence 122/122 ; tests Python du plan 11/11 ; python tools/plan/gate.py lint confirme 105 étapes et 18 verrous ; git diff --check est propre.
