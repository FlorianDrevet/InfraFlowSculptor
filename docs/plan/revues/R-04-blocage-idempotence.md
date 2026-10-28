# Blocage R-04 — portée d’idempotence de création d’organisation

Blocage résolu : CONFIRMÉ

**Étape** : R-04 — Revue de sécurité des accès.

**Ce qui manque** : DT-31 décrit une clé d’idempotence indexée par `organization_id`, alors que `POST /v1/organizations` crée précisément la première organisation et s’exécute sans organisation active. La route ne peut donc pas utiliser le filtre actuel sans une autre portée. Ajouter simplement `.WithIdempotency()` renverrait `ORGANIZATION_CONTEXT_REQUIRED`.

**Options examinées** :

1. **Recommandée** — stocker cette commande avec une clé scoppée par tenant, utilisateur et clé d’idempotence, via une migration et un magasin dédiés ; les autres commandes gardent leur portée organisation.
2. Traiter la création d’organisation comme exception et ne pas rejouer automatiquement cette commande ; une nouvelle tentative après coupure peut créer un doublon.

**Décision de Florian (2026-10-27)** : retenir l’option 1, avec une clé propre au tenant et à l’utilisateur. La portée comprend aussi la clé d’idempotence fournie par le client. La portée par organisation reste utilisée pour les commandes exécutées dans une organisation, avec une séparation supplémentaire par utilisateur.

**Résolution** : `POST /v1/organizations` applique maintenant le filtre d’idempotence. En l’absence d’organisation active, le filtre construit une portée utilisateur à partir de `tenantId` et `objectId`. Les clés utilisateur sont persistées dans une table dédiée avec une contrainte d’unicité incluant la portée et la clé. Pour une route avec organisation active, la clé de stockage est dérivée de `(tenantId, objectId, clientKey)`, afin que deux utilisateurs ne puissent pas rejouer mutuellement une réponse stockée.

La réservation, les écritures métier du gestionnaire et la réponse capturée partagent maintenant la même transaction PostgreSQL. Le `UnitOfWorkBehavior` détecte qu'il s'agit d'une transaction détenue par le filtre et ne la valide pas prématurément. Une exception de gestionnaire ou d'enregistrement de réponse annule ensemble l'écriture métier et la réservation ; la relance peut reprendre proprement.

## Vérifications effectuées

- `OrganizationsController` conserve la route comme `OrganizationAgnostic` et applique `.WithIdempotency()` au POST de création.
- `IdempotencyEndpointFilter` utilise la portée organisationnelle lorsqu’elle existe, puis la portée tenant/utilisateur pour une commande authentifiée sans organisation active; une requête sans portée authentifiée reçoit un refus explicite.
- Les tests vérifient le rejeu de la même réponse, le rejet d’une clé réutilisée avec un autre corps et l’isolation entre utilisateurs. Les tests du magasin vérifient la persistance des clés utilisateur et la purge des deux portées.
- Migration dédiée `20261027170637_R_04_UserIdempotencyKeys` générée et modèle EF synchronisé.
- Validation finale du segment : suite .NET complète 204/204, build Release avec avertissements traités en erreurs (0 avertissement), tests frontend 63/63, lint, build Angular et contrôle OpenAPI réussis. Le test d'acceptation de deux acceptations concurrentes porte maintenant deux clés distinctes, vérifie une seule appartenance et refuse toute réponse 500.
- Recette A14 exécutée dans Scalar avec les comptes de démonstration : Alice reçoit HTTP 200 et les 15 permissions; Chloé reçoit HTTP 200 et uniquement `project.read`.
- Aucun changement Azure ou autre opération externe n’a été nécessaire pour résoudre ce point.
