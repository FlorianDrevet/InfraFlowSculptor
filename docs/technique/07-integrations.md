# 07 — Intégrations externes

## 1. Entra ID (connexion des utilisateurs)

| Inscription | Type | Réglages | Créée à |
|---|---|---|---|
| `IFS API (<env>)` | API | `signInAudience = AzureADMultipleOrgs` (puis `AzureADandPersonalMicrosoftAccount` à J2-10), URI `api://<clientId>`, portée `access_as_user`, revendications facultatives `email`, `verified_primary_email`, `xms_edov`, rôles d'application internes `Ifs.Support`, `Ifs.CatalogEditor`, `Ifs.PlatformAdmin` (attribuables seulement dans le tenant IFS) | J0-30 |
| `IFS Web (<env>)` | SPA | Redirections `https://<hôte web>/`, PKCE, permission déléguée `access_as_user` | J0-30 |
| `IFS Git (<env>)` | Application confidentielle multi-tenant | Principal de service ajouté par les clients à leur organisation Azure DevOps ([24 § 2](../specs/24-depots-et-publication.md)) ; informations d'identification fédérées depuis l'identité managée du worker (pas de secret) | J0-23 |

Script : `infra/entra/Configure-IfsEntraApps.ps1` (idempotent, `-WhatIf`), exécuté par l'utilisateur avec
un compte administrateur d'applications du tenant d'IFS. État consigné dans `NEXT.md` (📌).

## 2. Azure DevOps

- **Authentification** : jeton Entra du principal de service « IFS Git » pour la ressource Azure DevOps
  (`499b84ac-1321-427f-aa17-267ca6975798/.default`), obtenu par le worker avec son identité managée
  fédérée. Repli : jeton personnel (PAT) avec expiration obligatoire, stocké dans Key Vault
  ([DT-12](01-decisions.md#dt-12--secrets-propres-à-ifs--key-vault)).
- **Dépôts** (publication) : API REST Git (`_apis/git/repositories`, `pushes`, `pullrequests`), version
  d'API épinglée dans `AzureDevOpsApiVersions.cs`.
- **Pipelines** (suivi) : `_apis/build/builds` filtrés par définitions gérées, `timeline`, artefacts
  (`ifs-report`), `_apis/pipelines/approvals` pour les approbations en attente ; rafraîchissement 2 min /
  15 min ([RG-SUI-01](../specs/28-suivi-des-deploiements.md)).
- **Tests** : réponses enregistrées sous `tests/InfraFlowSculptor.Infrastructure.Tests/AzureDevOps/Recordings/`
  servies par WireMock.Net. Aucune organisation réelle dans la CI.

## 3. GitHub (jalon 1)

Application GitHub « InfraFlowSculptor » : permissions `contents: write`, `pull_requests: write`,
`workflows: write` (lot 2, GitHub Actions), `actions: read` (suivi, lot 2), `metadata: read` ; jeton
d'installation par Octokit ; clé privée de l'application dans Key Vault. Webhooks (lot 2).

## 4. E-mail

ACS Email (domaine d'envoi vérifié, adresse `no-reply@<domaine IFS>`), identité managée du worker. En local,
MailPit. Gabarits FR/EN ; aucune donnée sensible ([RG-UI-14](../specs/26-interface.md)).

## 5. DNS public (disponibilité des noms, lot 1)

[RG-NOM-09](../specs/13-nommage.md) : résolution DNS du nom de domaine du service (`<nom>.vault.azure.net`…)
par le worker, résultat en cache 24 h, constat `Info` daté. Port `INameAvailabilityProbe` ; en test, un faux.

## 6. Ce qu'IFS n'appelle jamais

Azure Resource Manager du client, ses abonnements, ses Key Vaults ([DEC-04](../specs/03-decisions.md)). Toute
lecture Azure du lot 2 passera par la connexion Azure en lecture de l'organisation, explicitement consentie.
