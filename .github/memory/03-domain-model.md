# 03 — Modèle de domaine

> Fondations ajoutées en S-03; aucun agrégat métier n'est encore implémenté.
> Cible : `docs/specs/05-modele-de-donnees.md`, `docs/technique/02-backend.md` § 2-3. Étapes J0-01 et suivantes.

## Faits vérifiés

- `Domain/Common/Models/` contient `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `EnumValueObject<TEnum>` et `IHasVersion`.
- `Application/Common/Extensibility/` contient `IKeyed<TKey>`, `Registry<TKey,TService>` et les attributs de métadonnées du plan.
- Le seul test applicatif actuel vérifie qu'une clé absente du registre est citée dans l'exception.
