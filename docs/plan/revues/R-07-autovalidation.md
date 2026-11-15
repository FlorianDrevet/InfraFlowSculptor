# Auto-validation R-07 — Revue de la publication

Verdict : VALIDÉ

Motif : corrections backend revues, testées et prêtes pour la pull request R-07.

- **Segment** : J0-26 à J0-28 — verrou R-07.
- **Branche / pull request** : `impl/j0-publication` → PR vers `impl/j0-generation`, à créer après le commit et l'auto-validation du verrou.
- **Base du segment** : `origin/impl/j0-generation`, commit `91ffc1878cf740169865deb13eef5c3488c364d9`.
- **HEAD au début des corrections R-07** : `c3b9106b04347f0bf8a956b39743f35575c27476`.

## Vérifications exécutées

| Commande / contrôle | Résultat |
|---|---|
| `dotnet build src/backend/InfraFlowSculptor.slnx --configuration Release --no-restore --warnaserror -m:1` | Réussi après les dernières corrections : 0 avertissement, 0 erreur. |
| Suites .NET Release séquentielles, hors projet Acceptance | 937 réussis, 4 ignorés, aucun échec : API 147, Application 106, Architecture 9, Catalog 41, Domain 168, Emitters 95 réussis/4 ignorés, Engine 239, Infrastructure 132. |
| `IfsDbTransactionTests.DisposeDoesNotRetryRollbackAfterSqlRollbackFailure` | Rouge avant le correctif : la libération répétait le rollback SQL en échec. Vert après correction : 1/1; le rollback explicite n'est tenté qu'une fois. |
| Régressions de reprise et de manifeste | Les suites Domain 168/168 et Infrastructure 132/132 passent. Le test ciblé sur le hash d'un manifeste `Unchanged` a échoué avant correction puis passe 1/1; un hash invalide est rejeté avant toute mutation. |
| `dotnet ef migrations has-pending-model-changes` | Réussi : aucun écart entre le modèle courant et la dernière migration. |
| Tests frontend, lint/i18n, OpenAPI et build | La vérification du 2026-11-14 reste valide pour les corrections frontend : 218 tests Angular + 13 auxiliaires; 1034 clés i18n; `api:check` 224 modèles/11 services; build réussi avec avertissements de budgets consignés dans la recette. |
| `python -m graphify update .` | Mis à jour après les dernières modifications : 14 969 nœuds, 44 576 arêtes, 524 communautés. Trois fichiers SQL ne sont pas extraits faute de `tree_sitter_sql`. |
| `python tools/plan/gate.py lint` | Réussi : 105 étapes et 18 verrous cohérents. |
| `python tools/plan/gate.py check` | Autorise R-07; aucun blocage du plan. |
| `git diff --check` et `git diff --cached --check` | À relancer après les dernières mises à jour documentaires et avant commit. |

Le projet Acceptance n'a pas été relancé : son testhost Aspire avait précédemment bloqué pendant son démarrage. Aucun résultat Acceptance n'est revendiqué. Les quatre tests ignorés dans Emitters nécessitent le CLI Bicep opt-in; ils sont distingués des tests exécutés.

## Revue Claude CLI

- Revue initiale en lecture seule du diff `origin/impl/j0-generation...HEAD`, modèle Sonnet. Un suivi en lecture seule a ensuite relu les corrections; Claude n'a exécuté aucun test.
- Les dix constats initiaux ont été vérifiés puis traités : conservation des fichiers lors d'une re-préparation; filtre SQL traduisible du job d'expiration; incrément de version unique; usage Git ajouté par l'assistant; protection des connexions encore utilisées; rotation PAT avec référence Key Vault versionnée, compensation de rollback et suppression de l'ancienne clé par outbox; priorité de l'alerte J-3 sans doublon; confirmation explicite d'une révision périmée dans le client; `Location` de la route de développement corrigée; vérification du challenge Key Vault limitée au développement local explicite et découverte d'instance Azure rétablie.
- La relecture Sonnet suivante des corrections n'a relevé aucun défaut bloquant ni de sécurité, mais a signalé deux points faibles : l'historique achevé bloquait indéfiniment la suppression d'une connexion; un rollback SQL en échec pouvait empêcher la compensation et masquer l'exception principale.
- Premier correctif vérifié : le handler bloque uniquement les plans actifs et publications en cours; `20261114233242_J028PublicationHistoryReference` retire la FK de l'historique, qui conserve l'identifiant de connexion comme snapshot.
- Second correctif vérifié : la compensation n'agit qu'après confirmation du rollback SQL; si son résultat est incertain, le secret temporaire est conservé pour ne pas casser une référence potentiellement commitée. `UnitOfWorkBehavior` conserve l'exception de commande et rattache l'erreur secondaire. Le test rouge/vert confirme qu'un rollback SQL échoué n'est pas retenté explicitement lors du dispose.
- La revue Claude finale a confirmé que la reprise exige le même tag, la même connexion, le même dépôt, la même branche, le même SHA de base, le commit en tête et les mêmes fichiers générés. Elle a détecté deux défauts : la comparaison omettait le manifeste, et un manifeste distant pouvait déclarer son propre chemin parmi les fichiers gérés. Le correctif compare désormais tous les fichiers préparés, manifeste inclus; une entrée qui vise le manifeste est exclue et provoque une demande de confirmation. La revue signalait aussi que l'absence de suivi d'état de PR peut provoquer une confirmation répétée avant fusion; cela reste conservateur, et le suivi des PR est prévu en J0-30.
- La relecture incrémentale Claude après les derniers tests n'a trouvé aucun défaut bloquant. Un constat précédent était confirmé : le chemin de reprise pouvait remplacer le SHA d'un manifeste `Unchanged` par un hash différent. Le test ajouté échoue avant la correction, puis passe après l'appel à `IsValidFileChange`, qui préserve l'invariant avant toute mutation.
- Les remarques faibles sur la casse de `BaseCommitSha` ne s'appliquent pas aux valeurs persistées : `PublicationTarget` valide leur forme en minuscules avant enregistrement. Le filtre des reprises porte volontairement sur les cibles `Failed`; les cibles déjà `Completed` conservent leur PR et ne sont pas des commits incertains à reprendre. Les deux prédicats de chemin de manifeste traitent des niveaux de confiance différents : le Worker normalise les chemins distants non fiables, tandis que le domaine ne stocke que les chemins canoniques validés (barres obliques inverses et slash initial refusés).
- Les tests unitaires couvrent les règles de sélection des commits, les manifestes et l'enregistrement atomique; aucun test EF PostgreSQL n'exerce directement `FindPriorCommitReferencesAsync`, et aucun test du Worker ne traverse tout `WriteAsync` avec un fournisseur simulé. Ces lacunes de couverture sont conservées comme limites explicites; elles ne remettent pas en cause la recette P1–P8 ni les invariants ciblés vérifiés.

## Recette exécutée

La recette manuelle P1–P8 est passée et est détaillée dans [`recettes/suivi.md`](../recettes/suivi.md), entrée du 2026-11-14. Les captures bureau/mobile et références sont dans [`captures/R-07/`](captures/R-07/). Les PR Gitea de recette ont été fusionnées; la PR Azure Repos de recette #97 reste ouverte. Le résultat généré contient 81 fichiers (80 plus le manifeste), alors que la recette P3 annonçait 41; cet écart est conservé et signalé sans changer le plan.

Les corrections R-07 n'ont pas changé le parcours produit P1–P8; la recette manuelle n'a pas été rejouée après les correctifs exclusivement backend. La suite backend complète hors Acceptance, la compilation stricte, les tests ciblés des reprises/manifeste, de la suppression de connexion et du rollback, le contrôle des migrations et la mise à jour Graphify ont été exécutés après les dernières corrections.

## Écarts

- Acceptance n'est pas exécuté localement; le testhost Aspire avait précédemment bloqué au démarrage. Cette limite est également consignée dans le suivi de recette.
- Aucun test EF PostgreSQL n'exerce directement la requête `FindPriorCommitReferencesAsync`, et aucun test d'intégration complet ne traverse `WriteAsync` lors d'une reprise de commit; les règles de récupération, l'agrégat et le parcours manuel P6 couvrent les niveaux disponibles.
- Le chemin distant d'un manifeste auto-référencé est couvert via son helper de détection et par le contrôle statique du processeur; aucun test d'intégration complet n'exécute cette réponse fournisseur jusqu'à `MarkPrepared`.
- La rotation PAT réussie jusqu'au commit et le chemin d'actualisation sans nouveau PAT ne disposent pas encore de tests d'intégration dédiés.
- Les savepoints et l'échec de commit ne sont pas couverts par un test d'intégration de bout en bout; les checkpoints sont testés isolément et le rollback propriétaire est couvert par tests ciblés.
- Les cas d'alerte d'expiration autres que l'échéance J-3 ne sont pas tous testés; le test de job planifié vérifie le filtre traduisible.
- L'envoi e-mail à plusieurs administrateurs reste au moins une fois : en cas de livraison partielle puis d'échec, les destinataires déjà servis peuvent recevoir l'alerte à nouveau au prochain passage.
- Le client de relance couvre la confirmation d'une révision périmée; le cas non périmé n'est pas testé spécifiquement. Le test du `Location` valide le helper de réponse, pas le routage HTTP complet.
- Le rapport Graphify ignore trois SQL faute de `tree_sitter_sql`.

## Contrôles de coût et sécurité

- Aucune ressource Azure facturable n'a été créée et aucune écriture Azure n'a été réalisée pendant cette revalidation; coût incrémental nul.
- Le projet Azure DevOps `shop42` reste dans le périmètre de test autorisé; sa PR de recette #97 est conservée ouverte.
- Aucun secret, PAT, jeton ou mot de passe n'a été ajouté au dépôt, à la documentation ou à la mémoire.
- L'AppHost Aspire sous Podman et les volumes persistants ont été conservés.
