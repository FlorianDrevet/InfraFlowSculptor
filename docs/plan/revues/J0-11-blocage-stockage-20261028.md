# J0-11 — décision sur l’interdiction globale de JSONB

Blocage résolu : CONFIRMÉ — le 2026-10-28, Florian confirme que l’interdiction porte sur tous les usages JSONB, y compris ceux déjà livrés.

## Écart observé

- `docs/plan/02-jalon-0-pilote.md` décrit `Resource.Properties` comme une colonne `jsonb` nom → valeur.
- `docs/specs/14-modele-des-ressources.md` définit les valeurs effectives, les surcharges, les cas hérité/vide/absent, les enfants et les propriétés déclarées des ressources existantes, mais ne définit pas de schéma de persistance relationnel.
- `catalog/schema/resource-type.schema.json` autorise des propriétés `array` et `object`, mais ne décrit ni la forme de leurs éléments ni celle des membres d'un objet. Le descripteur `ContainerAppsEnvironment` contient déjà une propriété `workloadProfiles` de type `array` sans schéma d'élément.
- Le dépôt contient déjà d'autres colonnes JSONB : brouillon `ProjectWizardDraft` (J0-08), différences d'audit, réponses idempotentes et messages d'outbox. `docs/technique/02-backend.md` et DT-04 documentent aussi le JSONB comme convention.
- Claude CLI 2.1.312 a examiné le conflit en lecture seule. Sa proposition pour J0-11 est un modèle normalisé : colonnes typées pour les valeurs scalaires, lignes relationnelles pour les valeurs de listes/objets, et relations dédiées aux surcharges, présences, identifiants existants, noms forcés, enfants et tags. Il relève que les contrats des tableaux/objets du catalogue doivent être précisés pour valider et stocker ces formes sans JSONB.
- L'implémentation J0-11 est maintenant en place : propriétés scalaires typées et arbres relationnels bornés pour les
  conteneurs, avec des tables dédiées pour ressources, enfants, overrides, présences, identifiants, noms et tags.
- La migration convertit les cinq colonnes JSONB existantes en `text`, conserve leurs valeurs JSON, ajoute les tables de
  ressources et refuse un `Down` qui rétablirait JSONB. Le test de migration applique J0-11 sur PostgreSQL après avoir
  écrit des données représentatives dans le schéma J0-10, puis vérifie les valeurs et l'absence de toute colonne JSONB.
- Le modèle de catalogue garde `kind: array|object` comme contrat de type du conteneur racine. Les éléments et membres
  imbriqués sont préservés récursivement avec leur type (`null`, booléen, nombre, texte, tableau, objet), profondeur
  maximale 32 et 4096 nœuds maximum. Claude CLI recommandait un schéma récursif `items`/`members`; nous n'avons pas
  inventé de schémas Azure propres à chaque descripteur dans J0-11, car la tranche exige que les types du catalogue
  restent génériques et ajoutables sans code. Le cas `ContainerAppsEnvironment.workloadProfiles` avec objets imbriqués
  est couvert en intégration.
- Les protocoles opaques restent JSON sérialisé en `text` (audit, idempotence, outbox, brouillon et futur instantané de
  génération) ; les données de ressources requêtables sont relationnelles. La règle interdit le type et les colonnes
  PostgreSQL `jsonb`, pas la sérialisation JSON au niveau applicatif.

## Décision reçue

- Ressources, propriétés, surcharges, présences, IDs existants, noms forcés, enfants et tags sont persistés dans des
  tables relationnelles ; les structures imbriquées sont représentées par des nœuds typés ordonnés.
- Les colonnes JSONB déjà livrées (brouillon, audit, réponses idempotentes, outbox) sont migrées en `text`. L'inventaire
  et les décisions de représentation sont implémentés ; aucune colonne PostgreSQL `jsonb` ne doit subsister.
- Les décisions techniques, conventions backend et étapes du plan touchées sont actualisées. Le test de migration
  confirme conservation des valeurs et absence de colonnes JSONB ; l'intégration API couvre tableaux/objets imbriqués,
  enfants et cibles `Single`.

## Vérifications effectuées

- L'arbitrage utilisateur résout la seule question ouverte du blocage précédent : la règle s'applique globalement aux colonnes et types PostgreSQL JSONB, y compris aux données déjà livrées.
- La portée comprend les migrations et la mise à jour de l'architecture et du plan ; le choix des formes relationnelles doit préserver les comportements existants.
- Validation finale : build backend Release sans avertissement ; .NET 295/295 ; API ciblée 7/7 ; frontend 75 tests Angular, 3 tokens, 3 icônes et 1 normaliseur ; lint/i18n 447 clés ; OpenAPI et EF à jour. Le build Angular passe avec un avertissement de budget initial (504,80 kB / 500 kB).
- L'AppHost local et l'API sont sains. La recette Scalar manuelle reste non exécutée : le contrôle Windows a refusé de continuer car il ne pouvait pas identifier l'URL courante avec suffisamment de certitude. Les opérations de ressources sont couvertes par les tests d'intégration API, mais cela ne remplace pas la preuve manuelle Scalar.
- Aucun service Azure ni coût Azure n'a été créé. `python -m graphify update .` a reconstruit le graphe après les modifications.
