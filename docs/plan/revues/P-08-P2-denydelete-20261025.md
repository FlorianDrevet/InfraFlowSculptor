# P-08 P2 — refus de suppression par denyDelete (2026-10-25)

## Résultat

L’essai de suppression dans le portail Azure a été refusé par l’affectation Deny créée par la pile `ifs-shop43-core-prd`, à la portée exacte du workspace `log-shop43-main-prd`. La case de suppression définitive est restée décochée. Azure n’a supprimé ni le workspace ni ses données ; le portail signalait aussi zéro solution installée.

Après l’essai, `az resource show` retourne toujours `provisioningState=Succeeded`. Le workspace reste dans la pile avec `denyStatus=denyDelete`. Le C5 relu après l’essai est identique au C5 après le run #831 pour les cinq collections contrôlées : 172 ressources, 29 groupes, 7 piles, 63 affectations de rôle et 7 budgets, sans ID ajouté ni retiré. L’échec du contrôle `/health/dependencies` reste limité à `logs`, comme attendu après la révocation ; `/health` PRD reste à 200.

Le portail a explicitement attribué le refus à l’affectation Deny de la pile. La notification a été vérifiée à l’écran ; ses identifiants de compte ne sont pas reproduits ici. Azure Activity Log n’a retourné aucune entrée Failed lors de la lecture après l’essai, donc cette lecture n’est pas utilisée comme preuve du refus.

## Coûts et captures

Cost Management API, lecture réussie à `2026-10-25T21:08:14Z` : ActualCost MTD `142,251456 EUR` HT ; Forecast du 26 au 31/10 `31,972886 EUR` ; projection `174,224342 EUR` HT. La tentative refusée n’a changé aucun ID Azure.

Une lecture ultérieure dans le portail Cost Analysis à `2026-10-25T21:23Z` affiche `142,25 EUR` réels et `178,99 EUR` prévus pour octobre, contre `174,224342 EUR` dans la dernière projection API. Les appels API frais ont répondu HTTP 429 ; le contrôle portail a réussi. Pour décider, la suite retient la projection portail, plus élevée.

Pour préparer P4/P5/P7, le scénario prudent ajoute la borne haute initiale complète de `22,15 EUR` à `178,99 EUR`, soit `201,14 EUR` HT. Cette borne avait couvert les deux jeux complets et surestime donc le reste puisque le baseline et P2 de shop43 sont déjà exécutés ; elle n’est pas un plafond. Le budget préexistant de 200 EUR reste une alerte, pas un coupe-circuit, et Florian a explicitement autorisé la poursuite sans plafond au profil minimal. Le retard possible de Cost Management et la durée des ressources maintenues jusqu’aux preuves de P5 peuvent faire varier le montant ; actualiser coût et C5 avant les actions Azure significatives.

Les réponses Cost Management, C5, piles, ressources, groupes et rôles sont conservées hors dépôt sous `%LOCALAPPDATA%\InfraFlowSculptor\P08\after-p2-denydelete-20261025T210809Z\`. Les rapports P2 #831 et la capture du run #832 sont référencés dans [`resultats.md`](../preuves/resultats.md). La capture d’écran du refus a été contrôlée pendant l’exécution ; elle n’est pas conservée avec des données de compte dans le dépôt.

## Conclusion

P2 est OK : la révocation du Reader et la dégradation attendue du contrôle `logs` ont été observées, l’accès au workspace a été protégé par `denyDelete`, et l’essai de suppression a été refusé sans changement d’inventaire.
