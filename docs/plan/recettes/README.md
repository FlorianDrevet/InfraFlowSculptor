# Recettes — vérifications exécutées par Codex

Chaque étape du plan a son test 🧪. Une **recette** enchaîne ceux d'un segment et ajoute un parcours de bout en
bout ; Codex l'exécute avant l'auto-validation du verrou 🔒. Aucun résultat n'est renvoyé à Claude ou à un autre modèle.

| Recette | Verrous | Environnement |
|---|---|---|
| [`00-socle.md`](00-socle.md) | R-01 | Votre machine (`aspire run`) |
| [`01-preuves.md`](01-preuves.md) | R-03 | Vos abonnements Azure de test, votre Azure DevOps de test (rédigée par Luna à l'étape P-07) |
| [`02-jalon-0.md`](02-jalon-0.md) | R-04 à R-08 | Votre machine, puis l'IFS `dev` hébergé et Azure DevOps |
| `03-jalon-1.md` … | R-09 et suivants | Rédigées au détail de chaque jalon |

## Comment exécuter une recette

1. Lire `NEXT.md` : la recette demandée et sa section y sont citées.
2. Partir de l'état indiqué en tête de section (souvent : `aspire run`, puis `seed demo`).
3. Suivre les actions dans l'ordre. Si une action est ambiguë, noter l'ambiguïté et ne pas inventer le résultat attendu.
4. Pour chaque ligne, comparer à « Attendu ». Au premier écart, consigner l'étape, le résultat observé, l'heure et la preuve ;
   continuer seulement si les étapes suivantes en sont indépendantes.
5. Reporter directement le résultat dans [`suivi.md`](suivi.md), puis poursuivre l'étape. Demander à Florian uniquement une
   authentification qu'il doit effectuer lui-même, une décision de conception manquante ou un plafond de coût qui ne peut pas
   être déduit des contrôles existants.

## Utilisateurs de démonstration

Guide de Keycloak : [technique 09](../../technique/09-keycloak.md). Voir [technique 05 § 3](../../technique/05-execution-locale.md#3-utilisateurs-de-démonstration-royaume-keycloak-ifs). Mot de
passe commun en local : `Ifs-Demo-2026!`. Utiliser une fenêtre de navigation privée par utilisateur pour en tenir
plusieurs à la fois.
