# InfraFlowSculptor

[![CI](https://github.com/FlorianDrevet/InfraFlowSculptor/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/FlorianDrevet/InfraFlowSculptor/actions/workflows/ci.yml)

Décrivez une fois votre infrastructure Azure et vos conventions. InfraFlowSculptor produit et tient à
jour, dans vos dépôts git, le Bicep, les pipelines Azure DevOps et le kit d'installation qui la
déploient, avec le câblage de sécurité (identités, rôles, secrets) déduit automatiquement.

## État du dépôt

Le socle technique S-01 à S-17 est implémenté ; le verrou R-01 — revue du socle — est la prochaine étape. La recette clone neuf reste à exécuter lorsque les ports locaux seront libres. **Où on en est : [`NEXT.md`](NEXT.md).**

| Dossier | Contenu |
|---|---|
| [`docs/specs/`](docs/specs/README.md) | Spécifications fonctionnelles v1 — quoi |
| [`docs/technique/`](docs/technique/README.md) | Conception technique et décisions `DT-nn` — comment |
| [`docs/plan/`](docs/plan/README.md) | Plan d'implémentation : phases, jalons, verrous de revue, recettes — dans quel ordre |
| [`docs/design/`](docs/design/README.md) | Maquette v1 (72 écrans) et design system Strata, en HTML statique et en zip |

Pour la lecture, commencer par :
1. [Vision](docs/specs/00-vision.md)
2. [Principes](docs/specs/01-principes.md)
3. [Périmètre et lots](docs/specs/04-perimetre-et-lots.md)
4. [Projet de référence](docs/specs/90-projet-de-reference.md)
5. [Plan d'implémentation](docs/plan/README.md)

## Qui fait quoi

Claude (Opus) conçoit et relit à chaque verrou ; Codex (modèle Luna) implémente étape par étape. Consignes :
[`CLAUDE.md`](CLAUDE.md), [`AGENTS.md`](AGENTS.md). Mémoire du projet : [`MEMORY.md`](MEMORY.md).

## Démarrer (Windows)

Prérequis : Docker Desktop démarré, PowerShell 7, et les outils aux versions minimales de
[`tools/versions.json`](tools/versions.json). Vérifier la machine depuis la racine du dépôt :

```powershell
pwsh tools/dev/check-prereqs.ps1
git config core.hooksPath .githooks
Set-Location src/backend
aspire run
```

Aspire lance l’API, le Worker, l’application Web et les dépendances locales. Il affiche l’URL du tableau de bord dans
le terminal. Ouvrir ensuite [l’application](http://localhost:4200) et se connecter avec un utilisateur de démonstration.
Le mot de passe partagé des comptes ci-dessous est `Ifs-Demo-2026!` (développement local uniquement).

| Compte | Rôle de démonstration |
|---|---|
| `alice@contoso.example` | Administratrice de Contoso ; compte à utiliser pour voir « Bonjour Alice Martin » |
| `bob@contoso.example` | Contributeur Contoso |
| `chloe@contoso.example` | Lectrice Contoso |
| `david@fabrikam.example` | Administrateur Fabrikam, pour vérifier l’isolation entre organisations |
| `emma@outlook.example` | Compte Microsoft personnel simulé |
| `nina@contoso.example` | Invitée Contoso avec adresse non vérifiée |
| `ops@ifs.example` | Opérateur IFS |

| Interface | Adresse locale |
|---|---|
| Application Web | [http://localhost:4200](http://localhost:4200) |
| API Scalar | [http://localhost:5257/scalar](http://localhost:5257/scalar) |
| Console de compte Keycloak | [https://localhost:8080/realms/ifs/account/](https://localhost:8080/realms/ifs/account/) |
| Gitea (après initialisation) | [http://localhost:3000](http://localhost:3000) |
| Tableau de bord Aspire | URL imprimée par `aspire run` |

Dans un second terminal PowerShell ouvert à la racine, initialiser Gitea une fois ; le script est idempotent et range
ses secrets dans les User Secrets locaux de l’AppHost :

```powershell
pwsh tools/dev/gitea-init.ps1
```

`Ctrl+C` arrête l’AppHost et les processus API, Worker et Web. Les conteneurs de dépendances configurés comme
persistants restent démarrés et Aspire les réutilise au prochain `aspire run` si leur configuration n’a pas changé ;
laisser Docker Desktop actif. PostgreSQL, Azurite, Keycloak et Gitea ont aussi un volume de données. Redis et MailPit
n’en ont pas : leurs données peuvent disparaître si Aspire doit recréer leur conteneur. Voir le [guide d’exécution locale](docs/technique/05-execution-locale.md#durée-de-vie-entre-les-lancements).

## Reprendre le travail

```powershell
git pull
python tools/plan/gate.py status      # étape courante, statut, prochain verrou
python tools/plan/gate.py check       # confirme que l'étape est autorisée
git config core.hooksPath .githooks   # une fois par clone
```
