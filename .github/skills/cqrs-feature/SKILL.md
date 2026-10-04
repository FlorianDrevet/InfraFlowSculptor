---
name: cqrs-feature
description: "Use when adding a backend use case (command or query) in src/backend: anatomy of a CQRS slice following the template-CQRS conventions modernised from Vole-Papillon-Damour (docs/technique/02-backend.md)."
---

# Une tranche CQRS

Référence complète : `docs/technique/02-backend.md` § 2-5 et `DT-02`, `DT-05`, `DT-06`, `DT-23`, `DT-24`, `DT-31`.

| Fichier | Contenu |
|---|---|
| `Application/<Domaine>/Commands/<Cas>/<Cas>Command.cs` | `record … : IRequest<ErrorOr<TResult>>`, `[RequiresPermission(...)]`, `[Audited("...")]`, `[ModelChange]` si le modèle change, `ExpectedVersion` si modification |
| `…/<Cas>CommandValidator.cs` | FluentValidation : contrôle **à la saisie** ; code d'erreur = code de règle de la spec |
| `…/<Cas>CommandHandler.cs` | Charge via `IIfsDbContext`, appelle le domaine, **n'enregistre pas** (le `UnitOfWorkBehavior` le fait) |
| `Application/<Domaine>/Queries/<Cas>/…` | Requête + gestionnaire, projections directes (`AsNoTracking`), pagination serveur |
| `Contracts/<Domaine>/…Request.cs`, `…Response.cs` | DTO publics, jamais les types du domaine |
| `Api/Controllers/<Domaine>Controller.cs` | `Map<Domaine>Endpoints(this RouteGroupBuilder v1)` ; `mediator.Send` ; `result.Match(ok, errors => errors.ToProblem())` ; `.WithName(EndpointNames.X)`, `.WithSummary("… (UC-…)")`, `.Produces…`, `.RequireAuthorization(...)`, `.RequireRateLimiting(...)`, `.WithIdempotency()` en écriture |
| `Api/Common/Mapping/<Domaine>MappingConfig.cs` | Mapster |
| `tests/…` | Domaine, application, API (contrat + **scénario d'isolation** dans `IsolationScenarios`) |

Règles : un type public par fichier ; pas de chaîne magique (constantes) ; aucune règle métier calculée hors du domaine
ou du moteur ; `TimeProvider` pour l'heure ; identifiants fortement typés ; après ajout de route : `dotnet build`
(OpenAPI) puis `npm run api:generate`.
