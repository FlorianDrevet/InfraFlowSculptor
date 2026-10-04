# 06 — Tests et qualité

## 1. TDD obligatoire

Pour tout code exécutable : test d'abord, échec pour la bonne raison, implémentation minimale, refactorisation
([`.github/skills/tdd-workflow/SKILL.md`](../../.github/skills/tdd-workflow/SKILL.md)). Une exception temporaire
s'écrit dans `.github/test-debt.md` avec l'étape qui la soldera ; Claude la refuse ou l'accepte en revue.

## 2. Pyramide

| Niveau | Projet | Ce qu'il prouve | Outil |
|---|---|---|---|
| Domaine | `Domain.Tests` | Invariants locaux des agrégats | xUnit, AwesomeAssertions |
| Catalogue | `Catalog.Tests` | Chaque descripteur valide son schéma ; versions publiées inchangées | JsonSchema.Net |
| Moteur | `Engine.Tests` | Règles de calcul et de validation, une classe de test par règle (`RG-NOM-05`…) ; résultats du projet de référence ([90 § 2](../specs/90-projet-de-reference.md)) | xUnit, données construites par `ModelSnapshotBuilder` |
| Émetteurs | `Emitters.Tests` | Sortie identique octet pour octet à `reference/` ; déterminisme (deux générations identiques) | Verify, comparaison de dossiers |
| Application | `Application.Tests` | Gestionnaires, autorisation, effets indirects | NSubstitute ou base réelle selon le cas |
| Infrastructure | `Infrastructure.Tests` | EF (filtres, concurrence, déclencheurs d'audit), blob, Service Bus, fournisseurs git contre WireMock | Testcontainers, WireMock.Net |
| API | `Api.Tests` | Contrats HTTP, `problem+json`, isolation de **chaque** route | `WebApplicationFactory`, Testcontainers |
| Architecture | `Architecture.Tests` | Dépendances interdites ([00 § 2](00-vue-d-ensemble.md)), un type public par fichier, pas d'`IgnoreQueryFilters` hors `Admin/` | NetArchTest |
| Acceptation | `Acceptance.Tests` | Projet de référence de bout en bout dans l'AppHost (création → génération → publication Gitea) | `Aspire.Hosting.Testing` |
| Module de release | `reference/release-module/tests` | Journal, reprise, empreinte, révocations (simulées) | Pester 5 |
| Frontend | `ifs-web` (`*.spec.ts`) | Composants DS, services, gardes | Vitest |
| Bout en bout | `ifs-web/e2e` | Parcours utilisateur ([06](../specs/06-parcours-utilisateur.md)), captures 1440/390, accessibilité | Playwright, axe |

## 3. Tests d'architecture (extraits obligatoires)

- `Engine` ne référence ni `Microsoft.EntityFrameworkCore`, ni `Microsoft.AspNetCore`, ni `Azure.*`, ni
  `System.Net.Http`.
- `Emitters.*` ne référencent que `Engine` (types du plan) et `Catalog`.
- `Domain` ne référence que `ErrorOr`.
- `Application` ne référence pas `Infrastructure`.
- Les classes `*Controller` de l'API sont statiques et ne référencent pas `Infrastructure`.

## 4. Isolation (EXG-01)

`Api.Tests/Isolation/EndpointIsolationTests` :
1. énumère tous les `RouteEndpoint` de l'application ;
2. pour chacun, cherche un scénario dans `IsolationScenarios` (route → requête construite avec les objets
   de l'organisation Contoso) ;
3. exécute la requête en tant que `david@fabrikam.example` (autre organisation) → attend **404** ; en tant
   qu'un membre de Contoso sans accès au projet → **404** ; en tant que lectrice sur une commande → **403**
   nommant la permission ;
4. **échoue** si une route n'a pas de scénario (liste des routes manquantes dans le message).

Une route sans scénario ne passe donc pas la CI : c'est la traduction littérale d'EXG-01.

## 5. Performance

`Engine.Tests/Performance` génère un modèle synthétique de 300 ressources (puis 1 000 au jalon 1) et vérifie
validation < 2 s et plan + émission < 10 s sur l'agent de CI ([EXG-07](../specs/27-exigences-non-fonctionnelles.md),
[EXG-08](../specs/27-exigences-non-fonctionnelles.md)). Ce test est marqué `[Trait("Category","Performance")]` et
s'exécute dans la CI principale.

## 6. CI (`.github/workflows/ci.yml`)

| Job | Contenu | Bloquant |
|---|---|---|
| `plan` | `python tools/plan/gate.py lint` et `check` | Oui |
| `backend` | `dotnet build` (avertissements = erreurs), `dotnet test` hors acceptation, couverture | Oui |
| `acceptance` | `Acceptance.Tests` (Docker) | Oui à partir de J0 |
| `release-module` | Pester | Oui à partir de P |
| `frontend` | `npm ci`, `npm run lint`, `npm test`, `npm run build`, client API à jour | Oui |
| `e2e` | Playwright contre l'AppHost | Oui à partir de J0 |
| `supply-chain` | `dotnet list package --vulnerable --include-transitive`, `npm audit --audit-level=critical` ([EXG-05](../specs/27-exigences-non-fonctionnelles.md)) | Oui (critique) |
| `images` | build des images `api`, `worker`, `web` | Oui à partir de J0-30 |

## 7. Définition de « terminé » d'une étape

1. Le test écrit d'abord existe et passe ; la suite complète de la surface touchée passe.
2. `dotnet build` sans avertissement ; `npm run lint` propre.
3. Pour une étape UI : libellés de la maquette, captures 1440 et 390 jointes à la prochaine demande de revue,
   pas de défilement horizontal, `axe` sans violation sérieuse.
4. La mémoire (`.github/memory/`) reflète ce qui a été créé.
5. Le test manuel 🧪 de l'étape est **préparé** (données, commandes) ; son exécution par l'utilisateur est
   consignée dans `docs/plan/recettes/suivi.md`.
6. Un commit avec le message du plan et le pied `Étape: <ID>`.
7. `python tools/plan/gate.py done <ID>`.
