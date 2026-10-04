# Recettes — les tests que vous faites vous-même

Chaque étape du plan a son test manuel 🧪. Une **recette** enchaîne ceux d'un segment et ajoute un parcours de bout en
bout ; elle est exigée à chaque verrou 🔒 avant l'approbation de Claude.

| Recette | Verrous | Environnement |
|---|---|---|
| [`00-socle.md`](00-socle.md) | R-01 | Votre machine (`aspire run`) |
| [`01-preuves.md`](01-preuves.md) | R-03 | Vos abonnements Azure de test, votre Azure DevOps de test (rédigée par Luna à l'étape P-07) |
| [`02-jalon-0.md`](02-jalon-0.md) | R-04 à R-08 | Votre machine, puis l'IFS `dev` hébergé et Azure DevOps |
| `03-jalon-1.md` … | R-09 et suivants | Rédigées au détail de chaque jalon |

## Comment exécuter une recette

1. `git pull`, puis lire `NEXT.md` : la recette demandée et sa section y sont citées.
2. Partir de l'état indiqué en tête de section (souvent : `aspire run`, puis `seed demo`).
3. Suivre les actions dans l'ordre, sans improviser : si une action est ambiguë, c'est un défaut de la recette — le noter.
4. Pour chaque ligne, comparer à « Attendu ». Au premier écart, noter : l'étape, ce que vous avez vu (capture si
   possible), l'heure ; continuer si c'est sans dépendance, sinon s'arrêter.
5. Reporter le résultat dans [`suivi.md`](suivi.md) et le transmettre à Claude pour la revue (ou à Luna si la
   recette est celle des preuves, P-08).

## Utilisateurs de démonstration

Guide de Keycloak : [technique 09](../../technique/09-keycloak.md). Voir [technique 05 § 3](../../technique/05-execution-locale.md#3-utilisateurs-de-démonstration-royaume-keycloak-ifs). Mot de
passe commun en local : `Ifs-Demo-2026!`. Utiliser une fenêtre de navigation privée par utilisateur pour en tenir
plusieurs à la fois.
