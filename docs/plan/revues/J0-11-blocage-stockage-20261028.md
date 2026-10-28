# J0-11 — blocage sur le stockage des propriétés

Blocage confirmé : CONFIRMÉ — le plan de J0-11 prescrit `jsonb` pour les propriétés des ressources, alors que Florian a explicitement demandé de ne pas utiliser de JSONB.

## Écart observé

- `docs/plan/02-jalon-0-pilote.md` décrit `Resource.Properties` comme une colonne `jsonb` nom → valeur.
- `docs/specs/14-modele-des-ressources.md` définit les valeurs effectives, les surcharges, les cas hérité/vide/absent, les enfants et les propriétés déclarées des ressources existantes, mais ne définit pas de schéma de persistance relationnel.
- `catalog/schema/resource-type.schema.json` autorise des propriétés `array` et `object`, mais ne décrit ni la forme de leurs éléments ni celle des membres d'un objet. Le descripteur `ContainerAppsEnvironment` contient déjà une propriété `workloadProfiles` de type `array` sans schéma d'élément.
- Le dépôt contient déjà d'autres colonnes JSONB : brouillon `ProjectWizardDraft` (J0-08), différences d'audit, réponses idempotentes et messages d'outbox. `docs/technique/02-backend.md` et DT-04 documentent aussi le JSONB comme convention.
- Claude CLI 2.1.312 a examiné le conflit en lecture seule. Sa proposition pour J0-11 est un modèle normalisé : colonnes typées pour les valeurs scalaires, lignes relationnelles pour les valeurs de listes/objets, et relations dédiées aux surcharges, présences, identifiants existants, noms forcés, enfants et tags. Il relève que les contrats des tableaux/objets du catalogue doivent être précisés pour valider et stocker ces formes sans JSONB.
- Aucune implémentation de J0-11 ni modification des spécifications ou conventions de stockage n'a été faite.

## Décision attendue

Confirmer la portée de l'interdiction. Recommandation : appliquer le modèle relationnel normalisé à J0-11, préciser dans le catalogue les formes `array`/`object` nécessaires, et conserver les usages JSONB déjà livrés hors de cette étape. Une interdiction générale demanderait une évolution distincte de l'architecture, des migrations et du plan.

## Vérifications effectuées

- `python tools/plan/gate.py check` autorisait J0-11 avant la consignation de ce blocage.
- Les prescriptions de J0-11, le contrat de propriété du catalogue, le descripteur `ContainerAppsEnvironment`, les usages JSONB du brouillon, de l'audit, de l'idempotence et de l'outbox, ainsi que DT-04 ont été vérifiés dans le dépôt.
- La revue de Claude CLI était en lecture seule ; aucun fichier du code n'a été modifié par Claude.
