# 25 — Agent IA (serveur MCP)

## 1. Objectif

Permettre à un agent IA (Claude, Copilot, autre client MCP) de lire et de modifier un projet IFS avec
**exactement** les mêmes règles que l'écran, et donner à l'humain le dernier mot sur ce que l'agent
change ([DEC-31](03-decisions.md)).

## 2. Accès

**RG-MCP-01 — Authentification.**
- OAuth 2.1 avec Entra ID, selon la spécification d'autorisation MCP : l'utilisateur consent depuis son
  client MCP, sans copier de jeton.
- Ou jeton d'API ([10 § 6](10-organisations-et-acces.md)).

Dans les deux cas, l'agent agit avec les droits de l'utilisateur, limités par les portées.

**RG-MCP-02 — Portées OAuth.** Mêmes portées que les jetons d'API : `read`, `propose`, `write`,
`generate`, `publish`. Le consentement les affiche en clair.

**RG-MCP-03 — Traçabilité.** Toute action d'un agent est journalisée avec l'utilisateur, le client MCP
et le jeton ou la session OAuth.

**RG-MCP-04 — Limites de débit.** Par utilisateur : 120 appels par minute en lecture, 30 en écriture,
5 générations et 2 publications par minute. Au-delà, réponse « trop de requêtes » avec le délai
d'attente.

## 3. Outils

**RG-MCP-05 — Parité par construction.** Chaque commande et chaque requête de l'API publique est exposée
comme outil MCP, avec un nom `snake_case` dérivé de son nom, sa description et son schéma d'entrée. Les
outils sont produits depuis le même catalogue de commandes que l'API. Une commande ajoutée à l'API est
disponible en MCP sans travail supplémentaire.

**RG-MCP-06 — Outils de haut niveau.** En plus des commandes unitaires :

| Outil | Rôle |
|---|---|
| `get_project_model` | Modèle complet d'un projet (ou d'un composant) en JSON structuré, avec valeurs effectives et éléments implicites. |
| `describe_resource_type` | Descripteur d'un type : propriétés, valeurs, liaisons, sorties, rôles. |
| `validate_project` | Constats de validation. |
| `explain_name` | Calcul détaillé du nom Azure d'une ressource. |
| `get_deployment_plan` | Plan de déploiement neutre d'un composant et d'une cible ([21 § 3](21-generation-et-revisions.md)), indépendant du langage et de la plateforme. |
| `preview_change` | Applique un ensemble de commandes « à blanc » : constats et résumé des changements, sans rien enregistrer. |
| `create_change_proposal` | Crée une proposition de modification (section 4). |
| `generate_revision`, `diff_revisions` | Génération et comparaison ([21](21-generation-et-revisions.md)). |
| `publish_revision` | Publication ([24](24-depots-et-publication.md)) ; le client MCP demande une confirmation à l'utilisateur avant l'appel. |

**RG-MCP-07 — Erreurs exploitables.** Une commande refusée renvoie les mêmes messages par champ que
l'écran, avec le code de règle, pour que l'agent puisse corriger et réessayer.

**Ressources MCP** : `ifs://projects/{id}` (résumé), `ifs://projects/{id}/model`,
`ifs://catalog/{version}/types/{type}`.

**Prompts MCP** : `create_project` (parcours guidé de création), `add_service` (ajout d'un composant
applicatif type), `review_findings` (corriger les constats d'un projet).

## 4. Propositions de modification

| Champ | Contenu |
|---|---|
| Projet | Le projet visé. |
| Titre, explication | Rédigés par l'agent ou l'utilisateur. |
| Commandes | Liste ordonnée de commandes du modèle. |
| Version de base | Version du modèle sur laquelle la proposition a été préparée. |
| Aperçu | Résumé des changements et constats, calculés à la création. |
| État | `Ouverte`, `Appliquée`, `Rejetée`, `Périmée`. |

**UC-MCP-01 — Créer une proposition** (portée `propose`). L'agent prépare les commandes ; IFS calcule
l'aperçu. Rien n'est modifié.

**UC-MCP-02 — Relire et appliquer une proposition** (contributeur, dans l'écran). L'utilisateur voit le
résumé, les constats et le détail des commandes, puis applique (toutes les commandes en une
transaction) ou rejette avec un commentaire.

**RG-MCP-08 — Périmée.** Si le modèle a changé depuis la version de base, IFS recalcule l'aperçu à
l'ouverture. Si une commande ne s'applique plus, la proposition passe `Périmée` et ne peut pas être
appliquée telle quelle.

**RG-MCP-09 — Conservation.** Les propositions sont persistées 90 jours, visibles par les membres du
projet.

**RG-MCP-10 — Mode direct.** Avec la portée `write`, un agent peut appliquer des commandes sans
proposition. Le propriétaire d'un projet peut interdire ce mode pour son projet : les agents n'ont alors
que `propose`.
