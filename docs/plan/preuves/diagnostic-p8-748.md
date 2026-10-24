# P-08 — diagnostic hors ligne du run #748

**D1a — 2026-10-17.** Diagnostic établi à partir des captures et artefacts déjà téléchargés. Le run #748 reste
**non diagnostiqué** : l’aperçu publié est disponible, mais pas le JSON What-If recalculé sous verrou. Aucun résultat
D1b sous l’identité personnelle ne pourra établir la cause de ce run.

## Sources auditées

Les téléchargements et sorties brutes restent hors du dépôt, sous
`%LOCALAPPDATA%\InfraFlowSculptor\P08\D1a\`.

| Source | Constat exploitable |
|---|---|
| `run-746-infra` — artefact `infra` de la CI #746 | Contient le module de release, son wrapper, le modèle `orders/infra/main.bicep`, les paramètres DEV et `release.dev.json`. C’est la version à employer pour D1b. |
| `run-748-preview/ifs-preview.json` — artefact `ifs-preview-dev` | `ifs-preview/v1`, généré le `2026-10-17T10:19:19.9553725Z`, empreinte `bec9d7c6492eb864e0fb7973427066f7ca2412b2a83faaf1fe3a24d8627b3460`, 5 ressources modifiées, 1 écriture de données, 1 état applicatif, aucune création/recréation/suppression/révocation/opération en attente. |
| `run-748-report/ifs-report.json` — artefact `ifs-report-dev` | `ifs-report/v2`, généré le `2026-10-17T10:24:52.9020257Z`, résultat `FailedBeforeMutation`, empreinte approuvée égale à celle de l’aperçu publié, 0 étape exécutée. |
| `run-748-preview-log-9.txt` et `run-748-deploy-log-20.txt` | Les logs de tâches téléchargés ne contiennent pas de JSON What-If recalculé identifiable. La recherche des marqueurs `what-if`, `resourceId`, `changeType`, `propertyChangeType`, `fingerprint`, `changes` et `az deployment` n’en a retrouvé aucun. |
| `src/backend/tests/.../Recordings/p8-infra-dev-failed-run-748/` | Les captures contiennent `timeline.json`, `build.json`, `artifacts.json` et `approvals.json`, sans corps de log permettant de récupérer le recalcul. |

Les fichiers bruts, identifiants Azure, noms de ressources et valeurs de propriétés ne sont pas copiés ici. L’empreinte
ci-dessus est celle du JSON initial ; elle ne démontre pas pourquoi le recalcul a divergé. Si ce recalcul original ne
peut pas être récupéré, conserver le verdict **non diagnostiqué** et ne lui attribuer aucune cause.

## Contrôle du code livré dans la CI #746

`Invoke-IfsInfraPreview.ps1` exige `servicePrincipalId` et vérifie qu’il correspond au client ID de l’identité de
déploiement. Le wrapper récupère les identités `deploy` et `app`, puis passe leurs `principalId` à
`Invoke-IfsInfraPreview`, qui appelle `Invoke-IfsPreview`. Cette fonction interne lance What-If et écrit
`ifs-preview.json` ; elle ne contient pas le contrôle de l’identité de la tâche AzureCLI.

Pour D1b, le module interne reçoit donc les mêmes identités `deploy` et `app`, obtenues par lecture de la ressource
Azure, ainsi que les mêmes fichiers de release, Bicep et paramètres. L’authentification de la CLI reste celle de
l’utilisateur. Ne pas définir ni simuler `servicePrincipalId`. Les sorties D1b portent explicitement
`identity: personal-az-cli` et `servicePrincipalId: absent` ; elles mesurent uniquement la reproductibilité sous cette
identité et ne remplacent pas le recalcul manquant du #748.

## Comparateur hors ligne

`tools/proofs/Compare-IfsPreviews.psm1` compare deux fichiers `ifs-preview/v1` de même projet, composant, cible et
unité. Il compare directement le fingerprint de production et tous les champs d’effet, sans recalculer ce fingerprint.
L’ordre des tableaux de ressources et des deltas ne change pas le résultat. `generatedAt` est conservé dans les
métadonnées de comparaison, mais exclu de l’égalité.

Verdicts : `Identique`, `Divergent` ou `NonConcluant`. Les écarts signalent les ressources, le type de changement, le
chemin de delta, l’empreinte ou un autre détail d’effet. Les identifiants de ressource sont remplacés par des clés
SHA-256 tronquées dans le diagnostic ; les valeurs `before`/`after` et les identifiants bruts ne sont jamais émis.
Une entrée absente, illisible, de schéma inconnu ou de contexte différent donne `NonConcluant`.

Les neuf tests Pester utilisent des fixtures synthétiques et couvrent les trois verdicts, l’ordre des ressources et
deltas, l’empreinte, les effets applicatifs, les différences de type/delta, les entrées invalides et les contextes
incompatibles. Exécution depuis la racine du dépôt :

```powershell
rtk pwsh -NoProfile -Command "Invoke-Pester -Path 'reference/release-module/tests/CompareIfsPreviews.Tests.ps1' -Output Detailed"
```

Pour comparer deux captures après leur arrivée, sans afficher les entrées :

```powershell
Import-Module './tools/proofs/Compare-IfsPreviews.psm1' -Force
$comparison = Compare-IfsPreviews -LeftPath $leftPreviewPath -RightPath $rightPreviewPath
$comparison | ConvertTo-Json -Depth 10
```

Ne pas utiliser ce comparateur entre l’aperçu publié #748 et une passe D1b : l’identité, le moment et potentiellement
le contexte diffèrent. Il ne faut pas non plus comparer une passe D1b au #748 comme preuve de cause.

## D1b — cinq aperçus DEV en lecture seule

L’utilisateur exécute les cinq passes dans une session PowerShell ouverte avec `rtk pwsh`. Le script ci-dessous
utilise l’artefact local de la CI #746, vérifie que l’identité CLI est un utilisateur et que l’abonnement actif est
celui du fichier de release, puis appelle directement `Invoke-IfsPreview` dans la portée du module. Il reproduit aussi
les deux lectures d’identité et transmet les `principalId` nécessaires aux paramètres Bicep. Il ne lance aucun pipeline,
stage d’approbation, déploiement ou autre écriture Azure.

Les JSON et rapports contiennent des identifiants et des valeurs Azure : ils restent dans le dossier local indiqué.
Ne pas les ajouter à Git. Au retour, communiquer le chemin de la session et les cinq `metadata.json` ; les captures
brutes resteront disponibles localement pour la comparaison hors ligne et seront vérifiées avant tout partage.

```powershell
$ErrorActionPreference = 'Stop'

$artifact = Join-Path $env:LOCALAPPDATA 'InfraFlowSculptor/P08/D1a/run-746-infra'
$releasePath = Join-Path $artifact 'orders/infra/release.dev.json'
$templateFile = Join-Path $artifact 'orders/infra/main.bicep'
$parameterFile = Join-Path $artifact 'orders/infra/main.dev.bicepparam'
$modulePath = Join-Path $artifact '.ifs/templates/scripts/IfsRelease.psm1'
$requiredFiles = @($releasePath, $templateFile, $parameterFile, $modulePath)
if (@($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -gt 0) {
    throw 'Artefact infra #746 incomplet dans le dossier D1a local.'
}

if (Test-Path Env:servicePrincipalId) {
    throw 'Ouvrir une session propre : servicePrincipalId ne doit pas être défini.'
}
$identityType = (& rtk az account show --query user.type --output tsv | Out-String).Trim()
if ($identityType -cne 'user') {
    throw 'La session Azure CLI doit être authentifiée comme utilisateur personnel.'
}
$releaseDataForCheck = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
$activeSubscription = (& rtk az account show --query id --output tsv | Out-String).Trim()
if ($activeSubscription -cne [string]$releaseDataForCheck.subscriptionId) {
    throw 'L abonnement Azure CLI actif ne correspond pas au fichier de release DEV.'
}

$azVersionJson = ((rtk az version --output json) | Out-String) | ConvertFrom-Json
$azVersion = [string]$azVersionJson.'azure-cli'
$bicepVersion = (& rtk bicep --version 2>&1 | Out-String).Trim()
$inputHashes = [ordered]@{
    releaseData = (Get-FileHash -LiteralPath $releasePath -Algorithm SHA256).Hash.ToLowerInvariant()
    template = (Get-FileHash -LiteralPath $templateFile -Algorithm SHA256).Hash.ToLowerInvariant()
    parameters = (Get-FileHash -LiteralPath $parameterFile -Algorithm SHA256).Hash.ToLowerInvariant()
    module = (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash.ToLowerInvariant()
}

$sessionId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$sessionRoot = Join-Path $env:LOCALAPPDATA "InfraFlowSculptor/P08/D1b/$sessionId"
[void](New-Item -ItemType Directory -Path $sessionRoot -Force)
$releaseModule = Import-Module -Name $modulePath -Force -PassThru
$invokePreview = {
    param(
        [string] $ReleasePath,
        [string] $TemplateFile,
        [string] $ParameterFile,
        [string] $OutputDirectory
    )

    $releaseData = Read-IfsReleaseData -Path $ReleasePath
    $deploymentIdentity = Get-IfsManagedIdentity -ReleaseData $releaseData -Kind deploy
    $applicationIdentity = Get-IfsManagedIdentity -ReleaseData $releaseData -Kind app
    $preview = Invoke-IfsPreview `
        -ReleaseData $releaseData `
        -TemplateFile $TemplateFile `
        -ParameterFile $ParameterFile `
        -OutputDirectory $OutputDirectory `
        -DeploymentIdentityObjectId ([string]$deploymentIdentity.principalId) `
        -ApplicationIdentityObjectId ([string]$applicationIdentity.principalId)

    return [pscustomobject]@{
        Project = [string]$preview.project
        Component = [string]$preview.component
        Target = [string]$preview.target
        Unit = [string]$preview.unit
        GeneratedAt = [string]$preview.generatedAt
        Fingerprint = [string]$preview.fingerprint
    }
}

for ($pass = 1; $pass -le 5; $pass++) {
    $outputDirectory = Join-Path $sessionRoot ('run-{0:D2}' -f $pass)
    $startedAt = [DateTime]::UtcNow.ToString('o')
    $previewInfo = & $releaseModule $invokePreview $releasePath $templateFile $parameterFile $outputDirectory
    $finishedAt = [DateTime]::UtcNow.ToString('o')
    $previewPath = Join-Path $outputDirectory 'ifs-preview.json'
    $previewFile = Get-Content -LiteralPath $previewPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
    if ([string]$previewFile.fingerprint -cne $previewInfo.Fingerprint) {
        throw "Passe $pass : l’empreinte écrite diffère de l’objet retourné par le module."
    }

    $metadata = [ordered]@{
        pass = $pass
        identity = 'personal-az-cli'
        servicePrincipalId = 'absent'
        subscriptionMatchesReleaseData = $true
        context = [ordered]@{
            project = $previewInfo.Project
            component = $previewInfo.Component
            target = $previewInfo.Target
            unit = $previewInfo.Unit
        }
        startedAtUtc = $startedAt
        finishedAtUtc = $finishedAt
        generatedAt = $previewInfo.GeneratedAt
        fingerprint = $previewInfo.Fingerprint
        previewSha256 = (Get-FileHash -LiteralPath $previewPath -Algorithm SHA256).Hash.ToLowerInvariant()
        inputSha256 = $inputHashes
        toolVersions = [ordered]@{
            powershell = $PSVersionTable.PSVersion.ToString()
            azureCli = $azVersion
            bicep = $bicepVersion
        }
    }
    $metadataPath = Join-Path $outputDirectory 'metadata.json'
    [IO.File]::WriteAllText($metadataPath, (ConvertTo-Json -InputObject $metadata -Depth 10), [Text.UTF8Encoding]::new($false))
}

Write-Host "Cinq passes terminées. Garder les captures locales sous : $sessionRoot"
```

## État après D1a (historique)

P-08 passe à `EN_ATTENTE_DE_RECETTE`. À réception du chemin D1b et des résultats, reprendre uniquement avec
`python tools/plan/gate.py resume P-08`, comparer les cinq sorties hors ligne, puis consigner les constats D1 en
distinguant explicitement la reproductibilité personnelle du run #748 non diagnostiqué. Aucun pipeline ou release ne
doit être lancé avant les conditions D3 précisées au plan.

## Résultats D1b (2026-10-17)

Les cinq aperçus ont été produits depuis l’artefact infra de la CI #746, avec les mêmes template et paramètres, en
appelant `Invoke-IfsPreview` sous l’identité Azure CLI personnelle. `servicePrincipalId` était absent et la
souscription active correspondait aux données de release. Les cinq aperçus `ifs-preview/v1` et leurs cinq
`metadata.json` ont été conservés hors dépôt dans
`%LOCALAPPDATA%\InfraFlowSculptor\P08\D1b\20261017T215336Z\` ; le fichier de comparaisons s’y trouve également.

Les métadonnées et captures ont été validées : schéma, identité, contexte, empreintes SHA-256 des sorties et des
entrées, versions d’outils et cohérence entre chaque capture et ses métadonnées. Les quatre comparaisons de
`run-01` avec `run-02` à `run-05` donnent `Identique`, avec la même empreinte
`77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1` et aucun delta classé. Les cinq contextes
sont `shop42 / orders / dev / ifs-shop42-orders-dev`.

Pour permettre ces lectures sous l’identité personnelle, les rôles `Storage Blob Data Reader` au conteneur
`ifs-operations` du compte `stifsshop42dev` et `Key Vault Reader` au coffre `kv-shop42-main-dev` ont été attribués
sur demande de l’utilisateur. Le premier a permis de lire le journal d’opérations ; le second, les métadonnées de
référence du secret. Aucune valeur de secret n’a été lue. Les deux attributions restent en place.

L’extension Azure CLI `containerapp` écrivait un avertissement dans la sortie JSON consommée par le module. Les
passes ont utilisé `AZURE_CORE_ONLY_SHOW_ERRORS=true` dans le seul processus de capture pour supprimer cet
avertissement ; la configuration persistante de la CLI n’a pas été modifiée.

D1b démontre uniquement la répétabilité de ces aperçus sous l’identité personnelle. Elle ne diagnostique pas la
divergence du run #748 : le What-If recalculé original reste introuvable et #748 demeure non diagnostiqué. Aucun
pipeline, approbation, déploiement ou mutation n’a été lancé. P-08 reste KO ; aucun nouveau pipeline ou release ne
doit être lancé avant que Claude ait détaillé D2/D3 et que les conditions D3 du plan soient remplies. Le nettoyage
Shop42 reste suspendu.
