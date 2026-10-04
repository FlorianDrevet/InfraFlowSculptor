---
name: xunit-testing
description: "Use when writing .NET tests in src/backend/tests: xUnit, AwesomeAssertions, NSubstitute, Verify, Testcontainers conventions (DT-21)."
---

# Tests .NET

- xUnit `[Fact]`/`[Theory]` ; assertions AwesomeAssertions (`result.Should().Be(...)`) ; doubles NSubstitute ;
  `FakeTimeProvider` pour l'heure.
- Base réelle : Testcontainers.PostgreSql, une base par classe (`IAsyncLifetime`) ; jamais SQLite.
- API : `ApiFactory` (`WebApplicationFactory`, environnement `Testing`, jetons de `TestTokens`).
- Sorties générées : Verify (`*.verified.*` relus en revue) ou comparaison au dossier `reference/`.
- Un test = un comportement ; le nom cite la règle quand il y en a une ; données construites par des builders
  (`ModelSnapshotBuilder`), pas de fixtures géantes partagées.
- Tests longs : `[Trait("Category","Acceptance")]` ou `"Performance"`.
