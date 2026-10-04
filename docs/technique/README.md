# Conception technique — InfraFlowSculptor v1

> **Statut : cible (2026-10-03).** Ce dossier dit **comment** le produit décrit dans
> [`../specs/`](../specs/README.md) est construit. Il est écrit par Claude (concepteur) et appliqué par
> Codex (modèle Luna, exécutant). Il ne redéfinit aucune règle fonctionnelle : il s'y réfère par leurs
> identifiants (`RG-…`, `UC-…`, `DEC-…`, `EXG-…`).
>
> Luna n'arbitre rien. Tout choix technique est ici, sous la forme d'une décision `DT-nn`. Ce qui n'y
> est pas et qui manque pour exécuter une étape est une **question pour Claude** (voir
> [`../plan/README.md`](../plan/README.md) § « Quand le plan ne suffit pas »), jamais une improvisation.

| Vous cherchez… | Lisez |
|---|---|
| L'architecture d'ensemble, les processus, les flux | [00 — Vue d'ensemble](00-vue-d-ensemble.md) |
| Pourquoi tel outil, telle version, tel compromis | [01 — Décisions techniques `DT-nn`](01-decisions.md) |
| Le backend : solution, couches, conventions, évolutions du template CQRS | [02 — Backend](02-backend.md) |
| Le moteur de calcul, le catalogue, la génération, les émetteurs | [03 — Moteur et génération](03-moteur-et-generation.md) |
| Le frontend Angular, le design system Strata, les maquettes | [04 — Frontend](04-frontend.md) |
| Lancer tout en local avec Aspire et les émulateurs | [05 — Exécution locale](05-execution-locale.md) |
| Tests automatiques, qualité, CI | [06 — Tests et qualité](06-tests-et-qualite.md) |
| Fournisseurs git, Azure DevOps, e-mail, Entra | [07 — Intégrations](07-integrations.md) |
| L'hébergement d'IFS lui-même dans Azure | [08 — Hébergement](08-hebergement.md) |
| Se servir de Keycloak (connexion locale) | [09 — Keycloak](09-keycloak.md) |
| Ajouter une fonction sans refactoriser : points d'extension | [10 — Extensibilité](10-extensibilite.md) |
| Dans quel ordre construire | [`../plan/`](../plan/README.md) |
| À quoi ressemblent les écrans | [`../design/`](../design/README.md) |

## Les quatre règles qui priment sur tout le reste

1. **Un seul moteur de calcul, côté serveur** ([DEC-05](../specs/03-decisions.md), [P2](../specs/01-principes.md)).
   Noms, valeurs effectives, éléments implicites, ordre, constats, plan de déploiement : une seule
   bibliothèque (`InfraFlowSculptor.Engine`), pure, sans base de données ni réseau. L'API, le worker, le
   MCP et les émetteurs la consomment ; le frontend n'en refait jamais une partie.
2. **Le catalogue est de la donnée** ([P7](../specs/01-principes.md), [DEC-13](../specs/03-decisions.md)).
   Un type de ressource se décrit dans un fichier JSON versionné, pas dans du code réparti sur huit couches.
3. **La sortie de référence fait foi.** Le projet de référence ([90](../specs/90-projet-de-reference.md))
   a une sortie attendue, versionnée sous `reference/`, prouvée sur Azure avant d'être générée
   ([plan, phase P](../plan/01-preuves.md)). Un émetteur est juste quand il la reproduit octet pour octet.
4. **Isolation par défaut** ([EXG-01](../specs/27-exigences-non-fonctionnelles.md)). Aucune route n'existe sans
   son test d'isolation entre organisations et entre projets.
