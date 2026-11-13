# J0-23 — décision et déblocage du périmètre — 2026-11-11

Blocage résolu : CONFIRMÉ

## Décision de Florian

Le 2026-11-11, Florian a choisi l’élargissement du périmètre J0-23 en répondant « Augmente le périmètre et continue » à la question proposant de produire une sortie Azure DevOps exécutable et complète.

J0-23 et sa référence couvriront les fichiers nécessaires au fonctionnement autonome des pipelines et du kit d’installation. Les étapes catalogue `DependencyCache` et `ImageScan` auront des modèles exécutables présents dans la sortie et des points d’insertion explicites. Les paramètres, gardes et ressources de reprise propres à l’essai P-08 ne feront pas partie des pipelines générés. La référence et le texte de J0-23 peuvent être mis à jour pour refléter ce périmètre.

La recommandation Claude du 2026-11-01 est une aide technique, pas le fondement de cette autorisation. Les détails d’implémentation restent contraints par les spécifications et le plan déjà approuvés.

## Vérifications effectuées

- `git pull` a confirmé que `impl/j0-generation` était à jour avec `origin/impl/j0-generation`.
- `git status --short --branch` était propre avant la consignation de cette décision.
- Le texte de J0-23 demande la parité de l’arborescence complète de `reference-pilote.md` § 3 et cite les spécifications 22 et 23, DT-17 et RG-GEN-08.
- La référence contient des scripts, schémas, ressources d’installation et modèles appelés par ses pipelines, mais absents du périmètre de sortie actuellement émis.
- La référence contient aussi le pipeline `infra-data-access-recovery.yml`, son script associé et des paramètres figés pour `shop42/orders` et l’opération P-08 #762. Ces éléments sont hors du périmètre neutre de génération décrit par J0-23.
- Le choix reçu correspond à l’option 1 déjà consignée dans `J0-23-blocage-sortie-reference-20261101.md`. Aucune écriture Azure ou Azure DevOps n’est nécessaire pour résoudre ce blocage.

## Portée d’exécution

- Ajouter à la sortie et à sa vérification tous les assets requis par les pipelines et le kit, en conservant une source de référence unique.
- Ajouter les modèles et l’insertion des étapes `DependencyCache` et `ImageScan` à partir du plan neutre et du catalogue épinglé.
- Retirer du dépôt de sortie les paramètres et éléments de reprise spécifiques à P-08, tout en conservant séparément les preuves historiques nécessaires.
- Mettre à jour la section 3 de `docs/plan/reference-pilote.md`, le détail J0-23 et la mémoire après les vérifications TDD de l’étape.
