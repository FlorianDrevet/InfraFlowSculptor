# Jalon 3 — Outillage

> **Niveau : découpé.** Détaillé par Claude au verrou [`R-15`](04-jalon-2-largeur.md#-r-15--sortie-du-jalon-2).

**But** ([04 § 2.3](../specs/04-perimetre-et-lots.md)) : les agents IA, la navigation à l'échelle, la sortie et l'entrée
de projet, le mode découverte — puis l'ouverture commerciale du lot 1.

### J3-01 — Serveur MCP complet

| | |
|---|---|
| **Spécifications** | [25](../specs/25-agent-ia-mcp.md) : RG-MCP-01 à 07, outils de haut niveau, ressources, prompts ; [DEC-31](../specs/03-decisions.md), [DEC-96](../specs/03-decisions.md) |
| **Technique** | [DT-05](../technique/01-decisions.md#dt-05--minimal-api-v1-openapi-intégré--scalar) (outils dérivés du catalogue de commandes) |
| **Maquette** | panneau « Agents IA : préférez OAuth » de [ApiTokens](../design/maquette-v1/preview/ApiTokens.html) |
| **Commit** | `feat(mcp): serveur MCP dérivé de l'API` |

🎯 **Objectif.** Un agent IA lit et modifie un projet avec exactement les règles de l'écran.
🔧 **À faire.** Projet `InfraFlowSculptor.Mcp` (SDK MCP C# officiel, transport HTTP), OAuth 2.1 Entra et jetons d'API,
génération des outils depuis les `EndpointNames` et OpenAPI (hors administration), outils de haut niveau, demande de
publication confirmée côté serveur, limites de [RG-MCP-04](../specs/25-agent-ia-mcp.md), traçabilité.
✅ **Vérification automatique.** Parité : chaque commande publique non administrative a son outil ; isolation de chaque outil (EXG-01).
🧪 **Test manuel.** Depuis Claude, ajouter le serveur MCP local, demander « ajoute une file Service Bus » → proposition à relire.

### J3-02 — Vue graphe en lecture et équivalent liste

| | |
|---|---|
| **Spécifications** | [UC-LIA-03](../specs/16-liaisons-identites-et-acces.md), [RG-UI-09](../specs/26-interface.md) |
| **Maquette** | [ProjectGraph](../design/maquette-v1/preview/ProjectGraph.html) |
| **Commit** | `feat(web): graphe des liaisons` |

🎯 **Objectif.** Voir les dépendances d'un projet, filtrer par type et environnement.
🔧 **À faire.** Mise en page calculée côté serveur (ELK) ou client — tranché au détail du jalon ; arêtes explicites pleines,
implicites pointillées ; liste accessible équivalente.
✅ **Vérification automatique.** `axe` ; équivalence graphe/liste.
🧪 **Test manuel.** Graphe du projet de référence : suivre au clavier la liaison `ca api` → `sbns orders`.

### J3-03 — Recherche globale et notifications

| | |
|---|---|
| **Spécifications** | [RG-UI-06](../specs/26-interface.md), [26 § 4](../specs/26-interface.md) (RG-UI-12 à 14) |
| **Maquette** | [CommandPalette](../design/maquette-v1/preview/CommandPalette.html), [Notifications](../design/maquette-v1/preview/Notifications.html), préférences de [Profile](../design/maquette-v1/preview/Profile.html) |
| **Commit** | `feat(web): recherche globale et centre de notifications` |

🎯 **Objectif.** Trouver n'importe quoi en `Ctrl+K` ; ne rien manquer d'important.
🔧 **À faire.** Index de recherche par organisation (PostgreSQL plein texte), palette ; notifications en application et
préférences e-mail, résumé quotidien.
✅ **Vérification automatique.** Isolation de la recherche ; tous les événements du tableau de [26 § 4](../specs/26-interface.md).
🧪 **Test manuel.** `Ctrl+K` « payments » → le paramètre `Payments__ApiKey`.

### 🔒 R-16 — Revue MCP et navigation

### J3-04 — Journal d'audit complet

| | |
|---|---|
| **Spécifications** | [10 § 8](../specs/10-organisations-et-acces.md) RG-ORG-22 (filtres, export CSV) |
| **Maquette** | [OrgAudit](../design/maquette-v1/preview/OrgAudit.html) complet |
| **Commit** | `feat(audit): filtres et export` |

🎯 **Objectif.** Enquêter et prouver.
🔧 **À faire.** Filtres auteur, objet, période ; export CSV journalisé.
✅ **Vérification automatique.** Export conforme ; isolation.
🧪 **Test manuel.** Exporter un mois d'audit.

### J3-05 — Import de projet, convertisseur v0, dossier de transmission

| | |
|---|---|
| **Spécifications** | [UC-PRJ-08](../specs/11-projets-et-environnements.md), [RG-PRJ-09](../specs/11-projets-et-environnements.md), [DEC-84](../specs/03-decisions.md), [DEC-108](../specs/03-decisions.md), [91 T13](../specs/91-scenarios-critiques.md) |
| **Maquette** | point de départ « Importer » de [NewProject](../design/maquette-v1/preview/NewProject.html) |
| **Commit** | `feat(projet): import JSON, collisions et convertisseur v0` |

🎯 **Objectif.** Copier un projet sans collision avec les ressources gérées ; reprendre les projets de l'ancienne application.
🔧 **À faire.** Import `ifs-project/v1` (migration de schéma), collisions de noms, convertisseur ponctuel depuis la base de
l'ancien dépôt `infra-pipeline-editor`, dossier de transmission et de sortie.
✅ **Vérification automatique.** T13 ; aller-retour export/import.
🧪 **Test manuel.** Exporter `shop`, l'importer en `shop2` avec deux noms forcés → deux erreurs de collision.

### J3-06 — Mode démonstration et documentation d'architecture

| | |
|---|---|
| **Spécifications** | [DEC-112](../specs/03-decisions.md), [DEC-75](../specs/03-decisions.md) |
| **Commit** | `feat(produit): mode démonstration et documentation générée complète` |

🎯 **Objectif.** Essayer IFS sans droits Azure ; livrer une documentation d'architecture complète.
🔧 **À faire.** Organisation de démonstration exclue des indicateurs ; `README.ifs.md` complet.
✅ **Vérification automatique.** Indicateurs excluant la démonstration.
🧪 **Test manuel.** Parcours de démonstration de bout en bout.

### J3-07 — Plans, limites et mode découverte

| | |
|---|---|
| **Spécifications** | [DEC-78](../specs/03-decisions.md), [10 § 3](../specs/10-organisations-et-acces.md) (plan), [RG-VAL-01](../specs/20-validation.md) |
| **Maquette** | zone plan de [OrgSettings](../design/maquette-v1/preview/OrgSettings.html) |
| **Commit** | `feat(produit): plans Découverte et Équipe` |

🎯 **Objectif.** Les limites de chaque plan, le compteur des membres actifs, la découverte sans connexion git.
🔧 **À faire.** Limites (402 `PLAN_LIMIT`), membres actifs du mois, lecture seule au-delà, grâce d'impayé.
✅ **Vérification automatique.** Une limite par test.
🧪 **Test manuel.** Plan Découverte : la 26ᵉ ressource est refusée avec le plan supérieur cité.

### 🔒 R-17 — Revue outillage

### J3-08 — Préparation de l'ouverture commerciale

| | |
|---|---|
| **Spécifications** | [EXG-03](../specs/27-exigences-non-fonctionnelles.md) (test d'intrusion), [EXG-12](../specs/27-exigences-non-fonctionnelles.md), [EXG-13](../specs/27-exigences-non-fonctionnelles.md), [DT-30](../technique/01-decisions.md#dt-30--thème-clair) |
| **Commit** | `chore(produit): préparation de l'ouverture` |

🎯 **Objectif.** Tout ce qui conditionne la vente : sécurité auditée, accessibilité, langues, thème, licences d'icônes.
🔧 **À faire.** Corrections du test d'intrusion externe, audit WCAG 2.2 AA complet, thème clair si Strata le publie,
validation juridique des icônes Azure (note de licence Strata), environnement `prod`.
✅ **Vérification automatique.** `axe` sur tous les écrans ; aucune clé de traduction manquante.
🧪 **Test manuel.** Parcours P-01 complet au lecteur d'écran (NVDA).

### 🔒 R-18 — Sortie du lot 1

**Claude** : bilan du lot 1, puis détaille la vague A du lot 2 ([`06-lots-2-a-4.md`](06-lots-2-a-4.md)).
