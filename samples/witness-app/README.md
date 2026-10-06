# Application témoin

Cette application ASP.NET Core minimale valide les accès configurés par une cible Azure. Elle n’appartient pas à la
solution `src/backend/InfraFlowSculptor.slnx`.

## Contrôles

`GET /health` renvoie `status: ok` et la version fournie par `IMAGE_TAG` (ou `unknown` si elle est absente).

`GET /health/dependencies` exécute les contrôles suivants. Un contrôle sans configuration est marqué `ok: null` et
`error: skipped`; toute erreur renvoie HTTP 503 avec un message générique et sans exception ni valeur de configuration.

| Contrôle | Configuration | Vérification |
|---|---|---|
| `secret` | `Payments__ApiKey` | Valeur présente et non vide, résolue par Container Apps depuis Key Vault |
| `logs` | `LogAnalytics__WorkspaceId` | Requête `print 1` sur Log Analytics avec `DefaultAzureCredential` |
| `sql` | `Sql__Server`, `Sql__Database` | Écriture puis lecture dans `dbo.witness_checks`, transaction annulée ensuite |
| `sql-least-privilege` | `Sql__Server`, `Sql__Database` | Vérifie que `CREATE TABLE` est refusé à l’identité de l’application |
| `appconfig` | `AZURE_APPCONFIG_ENDPOINT` | Lecture de `orders:maxItemsPerOrder` avec `DefaultAzureCredential` |
| `servicebus` | `ServiceBus__Namespace` | Envoi d’un message de contrôle avec `DefaultAzureCredential` dans `order-created` |

La connexion Azure SQL utilise explicitement `Authentication=Active Directory Managed Identity`, sans `User Id` ni
mot de passe : SQL utilise ainsi l’identité système. Les autres SDK utilisent `DefaultAzureCredential`, qui peut
sélectionner l’identité affectée configurée par `AZURE_CLIENT_ID`.

Le script [`sql/witness-schema.sql`](sql/witness-schema.sql) crée la table une fois par base. Il doit être exécuté par
un administrateur SQL avant d’accorder à l’application uniquement `db_datareader` et `db_datawriter`.

## Tests et image

```powershell
dotnet test samples/witness-app/tests
docker build -t ifs-witness:local samples/witness-app
```

Le conteneur écoute sur 8080 et s’exécute avec l’utilisateur non privilégié fourni par l’image ASP.NET. Exemple :

```powershell
docker run --rm -p 18080:8080 -e IMAGE_TAG=local ifs-witness:local
```

Puis ouvrir `http://localhost:18080/health` et `http://localhost:18080/health/dependencies`. Le port hôte 18080
évite le port 8080 déjà publié localement par Keycloak Aspire. Sans configuration de dépendances, les six contrôles
doivent être `skipped`. Pour vérifier la variable vide, relancer avec `-e Payments__ApiKey=` : le contrôle `secret`
doit échouer avec `variable vide`, sans afficher sa valeur.
