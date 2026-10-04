# Lots 2, 3 et 4 — feuille de route technique

> **Grossier, volontairement.** Le principe de VPD s'applique : « ce qui n'est pas mesuré n'est pas décidé ». Ces lots se
> détaillent quand le lot précédent tient, au verrou de sortie qui les précède. Ce fichier dit seulement ce que chaque
> vague demandera **techniquement**, pour que les choix du lot 1 ne la rendent pas impossible.

## Lot 2 — Production d'entreprise ([04 § 3](../specs/04-perimetre-et-lots.md))

| Vague | Contenu | Ce que le lot 1 doit déjà permettre | Points techniques connus |
|---|---|---|---|
| **A — Réseau privé** | [18](../specs/18-reseau-et-exposition.md) complet | Exposition par environnement déjà dans le modèle ; module de release capable d'exécuteurs privés | Calcul d'adressage CIDR dans le moteur ; zones DNS privées implicites ; Managed DevOps Pools (fournisseur à inscrire) ; [90 § 5](../specs/90-projet-de-reference.md) |
| **B — IA** | [32](../specs/32-ia-et-foundry.md), Cosmos DB | Catalogue extensible sans code ([P7](../specs/01-principes.md)) | Quotas de modèles (connexion Azure en lecture), liaison « usage d'IA » ([AiUsageLink](../design/maquette-v1/preview/AiUsageLink.html), [FoundryAccount](../design/maquette-v1/preview/FoundryAccount.html)) |
| **C — Livraison** | Stratégies, catalogue d'étapes complet, Static Web App | Étapes décrites par descripteur ; stratégie « directe » isolée dans le module applicatif | Bleu/vert Container Apps (étiquettes de révision), slots ; [DeploymentDetail](../design/maquette-v1/preview/DeploymentDetail.html) |
| **D — GitHub** | Émetteur GitHub Actions, kit GitHub, suivi GitHub | Prototype P6 (J1-11) ; plan neutre | Webhooks `workflow_run` ; environnements `-apercu` ([DEC-101](../specs/03-decisions.md)) |
| **E — Gouvernance** | [33](../specs/33-gouvernance-couts-et-supervision.md), diagnostic guidé, plan Entreprise | Constats et règles en données ; audit complet | API des prix Azure ; pipeline de dérive ; [OrgPolicies](../design/maquette-v1/preview/OrgPolicies.html), [OrgRoles](../design/maquette-v1/preview/OrgRoles.html), [ProjectCosts](../design/maquette-v1/preview/ProjectCosts.html) |
| **F — Exposition et intégration** | Front Door, Application Gateway, APIM, Event Grid, Event Hubs, Jobs | Portées RBAC sur les enfants | — |
| **G — Collaboration** | Commentaires, modèles, sources de modules, connexion Azure en lecture, graphe éditable, webhooks | Source des modules par type dans le descripteur | AVM embarqués : copie épinglée sous `modules/avm/` |

## Lot 3 — Ouverture ([04 § 4](../specs/04-perimetre-et-lots.md))

Émetteur Terraform (état, blocs `removed`/`moved`/`import`, migration assistée), GitLab (dépôts, émetteur, kit), import ARM /
Bicep / groupe de ressources ([29](../specs/29-import.md), [ImportSource](../design/maquette-v1/preview/ImportSource.html),
[ImportReview](../design/maquette-v1/preview/ImportReview.html)), modules du client avec contrat, P10, types complémentaires.
**Exigence héritée** : parité entre langages [EXG-19](../specs/27-exigences-non-fonctionnelles.md) → export normalisé des
ressources déployées, outil de comparaison à écrire dès la vague D du lot 2.

## Lot 4 — Langages complémentaires et grande échelle ([04 § 5](../specs/04-perimetre-et-lots.md))

OpenTofu, Pulumi (six langages), multi-région, AKS (cluster), instance dédiée. **Exigence héritée** : aucun calcul dans les
émetteurs, sans quoi six émetteurs Pulumi dupliqueraient des décisions.
