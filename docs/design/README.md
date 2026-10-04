# Design — maquette v1 et design system Strata

| | Source (Claude Design) | Export statique (dans ce dépôt) | Archive |
|---|---|---|---|
| **Maquette v1** — 72 écrans | [InfraFlowSculptor — Maquette v1](https://claude.ai/artifact/8Vqk73EgXnLj9yzcn3uMAT) | [`maquette-v1/preview/index.html`](maquette-v1/preview/index.html) | [`maquette-v1.zip`](maquette-v1.zip) |
| **Design system Strata** | [Strata — InfraFlowSculptor](https://claude.ai/artifact/4M9UorSE1agmiyxSa3vuqJ) | [`strata/index.html`](strata/index.html), règles : [`strata/README.md`](strata/README.md) | [`strata.zip`](strata.zip) |

Export du 2026-10-04 (maquette : version `1791102459-f891`, n° 28 ; Strata : version `1791059335-ccc5`). Le canvas antérieur
« InfraFlowSculptor — Refonte UI » (2026-10-02) est **remplacé** par la maquette v1 et ne sert plus de référence.

## Ouvrir

Double-cliquer `maquette-v1/preview/index.html` : sommaire par page du canvas, chaque écran s'ouvre seul, **sans
réseau** (hors polices Google, remplacées par des polices locales dans l'application). Même chose pour
`strata/index.html` : couleurs, espacements, rayons, et chaque composant rendu (`strata/rendered/<Composant>.html`).

## Ce que contient chaque dossier

```
maquette-v1/
├── preview/                 rendu statique de chaque écran (DOM rendu, styles en ligne, sans script)
│   ├── index.html           sommaire
│   ├── <Écran>.html
│   └── ds/strata/components/bundle.css
└── sources/                 fichiers du canvas tels quels (*.dc.html, canvas.json, ds/strata/) — pour réimporter
strata/
├── README.md                principes, écriture, couleur, typographie, mise en page, motifs, « Passer au code »
├── tokens.json              SOURCE des valeurs (générée en CSS par l'application, DT-19)
├── tokens.css               variables CSS générées pour l'aperçu
├── design-system.json
├── components/<Nom>/        README.md (règles d'usage) + preview.html (exemples)
├── components/bundle.css    géométrie exacte de chaque composant (classes st-*)
├── components/bundle.js     composants React de référence + icônes Azure (data URI) + table resourceTypes
├── components/index.d.ts    API des composants (entrées)
├── assets/Azure/README.md   icônes Azure officielles et leur licence
└── rendered/<Nom>.html      chaque composant rendu en statique
```

## Règles pour l'implémentation (Luna)

1. **La maquette est la référence visuelle, Strata la référence des valeurs.** Libellés mot pour mot depuis
   `preview/<Écran>.html` ; couleurs, espacements, rayons uniquement par les variables `--ifs-*` générées depuis
   `strata/tokens.json` ; géométrie des composants depuis `strata/components/bundle.css`.
2. **Les données de la maquette sont des exemples** (Contoso, `shop`, révision 15…) : jamais recopiées en dur.
3. **Ce qui n'est pas dans le jalon n'apparaît pas** ([P9](../specs/01-principes.md)) : la maquette montre tous les lots. Le
   tableau ci-dessous et chaque étape du plan disent quelles zones implémenter.
4. **Écrans en 1 440 px** dans la maquette : le comportement sous 760 px suit le README de Strata (§ Mise en page) et
   [RG-UI-16](../specs/26-interface.md).

## Défauts connus de la maquette

| Défaut | Effet | Traitement |
|---|---|---|
| ~~11 écrans passaient `options="a,b,c"` (`Segmented`) ou `items="a,b,c"` (`Tabs`) en chaîne au lieu d'un tableau~~ | L'artboard ne se rendait pas (`options.map is not a function`) | **Corrigé dans le canvas le 2026-10-04** (version 28) : valeurs déplacées dans `renderVals()`. Le correctif de rendu de `tools/design/export_design.py` reste en filet de sécurité |
| Strata est sombre uniquement | Pas de thème clair ([RG-UI-07](../specs/26-interface.md)) | [DT-30](../technique/01-decisions.md#dt-30--thème-sombre-seul-changement-de-thème-prêt) |

## Mettre à jour l'export

Après une modification de la maquette ou de Strata dans Claude Design, Claude applique
[`.claude/skills/exporter-design/SKILL.md`](../../.claude/skills/exporter-design/SKILL.md) : relire les fichiers publiés,
puis `python tools/design/export_design.py maquette|strata …`, puis mettre à jour ce README (versions) et signaler dans
`NEXT.md` les étapes du plan touchées.

## Écrans et jalons

| Page du canvas | Écran (`preview/`) | Jalon / étape | Zones des jalons ultérieurs |
|---|---|---|---|
| Espace de travail | [Main](maquette-v1/preview/Main.html) | J0-32 | Propositions (J2-06) |
| | [Projects](maquette-v1/preview/Projects.html) | J0-09 | — |
| | [Proposals](maquette-v1/preview/Proposals.html), [ProposalReview](maquette-v1/preview/ProposalReview.html) | J2-06 | — |
| | [CommandPalette](maquette-v1/preview/CommandPalette.html), [Notifications](maquette-v1/preview/Notifications.html) | J3-03 | — |
| Accès & organisation | [Login](maquette-v1/preview/Login.html) | S-13 | — |
| | [InviteAccept](maquette-v1/preview/InviteAccept.html) | J0-03 | — |
| | [OrgMembers](maquette-v1/preview/OrgMembers.html) | J0-02, J0-03 | Équipes (J2-03) |
| | [OrgTeams](maquette-v1/preview/OrgTeams.html) | J2-03 | — |
| | [OrgConnections](maquette-v1/preview/OrgConnections.html) | J0-26 | GitHub (J1-07), connexion Azure (L2-G) |
| | [OrgAudit](maquette-v1/preview/OrgAudit.html) | J0-04 | Filtres complets, export (J3-04) |
| | [OrgSettings](maquette-v1/preview/OrgSettings.html) | J0-01, J0-02 | Accès support, télémétrie (J1-10), plan (J3-07) |
| | [OrgPolicies](maquette-v1/preview/OrgPolicies.html), [OrgRoles](maquette-v1/preview/OrgRoles.html) | L2-E | — |
| | [Profile](maquette-v1/preview/Profile.html) | J0-32 | Notifications (J3-03), thème (DT-30) |
| | [ApiTokens](maquette-v1/preview/ApiTokens.html), [ApiTokenCreate](maquette-v1/preview/ApiTokenCreate.html) | J1-04 | Panneau MCP (J3-01) |
| Création de projet | [NewProject](maquette-v1/preview/NewProject.html) | J0-08 | Import (J3-05), modèles (L2-G) |
| | [WizardIdentity](maquette-v1/preview/WizardIdentity.html), [WizardTools](maquette-v1/preview/WizardTools.html), [WizardEnvironments](maquette-v1/preview/WizardEnvironments.html), [WizardNaming](maquette-v1/preview/WizardNaming.html), [WizardReview](maquette-v1/preview/WizardReview.html) | J0-08 | Outils autres que Bicep/Azure DevOps (lots 2+) |
| | [WizardPublishing](maquette-v1/preview/WizardPublishing.html) | J0-08, J0-27 | Autres préréglages (J1-07) |
| Projet | [ProjectOverview](maquette-v1/preview/ProjectOverview.html) | J0-09 → J0-30 | Coûts (L2-E) |
| | [ProjectGraph](maquette-v1/preview/ProjectGraph.html) | J3-02 | Édition (L2-G) |
| | [ProjectEnvironments](maquette-v1/preview/ProjectEnvironments.html), [EnvironmentEdit](maquette-v1/preview/EnvironmentEdit.html) | J0-09 | Fenêtres, délais, DNS privé (L2) |
| | [ProjectNaming](maquette-v1/preview/ProjectNaming.html) | J0-13 | Disponibilité par API Azure (L2-G) |
| | [ProjectPublishPlan](maquette-v1/preview/ProjectPublishPlan.html) | J0-27 | Préréglages, relecteurs (J1-07) |
| | [ProjectMembers](maquette-v1/preview/ProjectMembers.html) | J0-05, J0-09 | Huit rôles (J1-04), équipes et portées (J2-03) |
| | [ProjectSettings](maquette-v1/preview/ProjectSettings.html) | J0-09, J0-25 | Catalogue (J1-01), outils et exécuteurs (J1-05, J1-06) |
| | [ProjectFindings](maquette-v1/preview/ProjectFindings.html) | J0-20 | Politiques (L2-E) |
| | [ProjectHistory](maquette-v1/preview/ProjectHistory.html) | J1-03 | Étiquettes, restauration (J2-04) |
| | [HistoryCompare](maquette-v1/preview/HistoryCompare.html) | J2-04 | — |
| | [Drafts](maquette-v1/preview/Drafts.html) | J2-05 | — |
| | [ProjectCosts](maquette-v1/preview/ProjectCosts.html) | L2-E | — |
| Composants & ressources | [Component](maquette-v1/preview/Component.html), [ComponentSettings](maquette-v1/preview/ComponentSettings.html) | J0-10 | Duplication, retrait, code additionnel (J1-05), abonnement par environnement (J2-03) |
| | [AddResourceType](maquette-v1/preview/AddResourceType.html), [AddResourceConfigure](maquette-v1/preview/AddResourceConfigure.html) | J0-14 | Types des lots suivants |
| | [ResourceGeneral](maquette-v1/preview/ResourceGeneral.html), [ResourceProperties](maquette-v1/preview/ResourceProperties.html), [ResourceExisting](maquette-v1/preview/ResourceExisting.html), [DeleteImpact](maquette-v1/preview/DeleteImpact.html), [VersionConflict](maquette-v1/preview/VersionConflict.html), [ResourceTabs](maquette-v1/preview/ResourceTabs.html) | J0-09, J0-14 | Sélection depuis Azure (L2-G) |
| | [ResourceChildren](maquette-v1/preview/ResourceChildren.html) | J1-01 | — |
| | [ResourceLinks](maquette-v1/preview/ResourceLinks.html), [AddLink](maquette-v1/preview/AddLink.html), [ResourceIdentity](maquette-v1/preview/ResourceIdentity.html) | J0-18 | Lecture de configuration (J1-02), usage d'IA (L2-B), demande d'accès (J2-07) |
| | [ResourceAppSettings](maquette-v1/preview/ResourceAppSettings.html) | J0-18 | Clés App Configuration (J1-02), import (J2-09) |
| | [ResourceApplication](maquette-v1/preview/ResourceApplication.html) | J0-19 | Mode Code (J2-01), pipelines du client (J2-08), stratégies et étapes (L2-C) |
| | [ResourceNetwork](maquette-v1/preview/ResourceNetwork.html) | J0-18 | Privé, domaines (L2-A) |
| Réseau & IA | [NetworkTopology](maquette-v1/preview/NetworkTopology.html), [NetworkVnet](maquette-v1/preview/NetworkVnet.html) | L2-A | — |
| | [FoundryAccount](maquette-v1/preview/FoundryAccount.html), [AiUsageLink](maquette-v1/preview/AiUsageLink.html) | L2-B | — |
| Production & livraison | [Revisions](maquette-v1/preview/Revisions.html), [GenerateBlocked](maquette-v1/preview/GenerateBlocked.html) | J0-24 | — |
| | [RevisionDetail](maquette-v1/preview/RevisionDetail.html) | J0-24, J0-28 | Comparaison (J2-09) |
| | [PublishDialog](maquette-v1/preview/PublishDialog.html), [PublishResult](maquette-v1/preview/PublishResult.html) | J0-28 | Mode direct, plusieurs dépôts (J1-07) |
| | [InstallChecklist](maquette-v1/preview/InstallChecklist.html) | J0-29 | GitHub (L2-D), réseau privé (L2-A) |
| | [Deployments](maquette-v1/preview/Deployments.html) | J0-30 | Dérive, coûts (L2-E) |
| | [DeploymentDetail](maquette-v1/preview/DeploymentDetail.html) | J0-30 | Bleu/vert (L2-C) |
| Import | [ImportSource](maquette-v1/preview/ImportSource.html), [ImportReview](maquette-v1/preview/ImportReview.html) | L3 | — |
| Exploitation IFS | [BackofficeCatalog](maquette-v1/preview/BackofficeCatalog.html), [BackofficeSupport](maquette-v1/preview/BackofficeSupport.html) | J1-10 | — |
| Coquille | [Sidebar](maquette-v1/preview/Sidebar.html) | S-13 (entrées ajoutées par chaque étape) | — |
