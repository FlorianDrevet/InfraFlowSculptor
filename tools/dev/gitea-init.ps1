[CmdletBinding()]
param(
    [ValidateRange(15, 600)]
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$giteaUrl = "http://localhost:3000"
$containerName = "ifs-gitea"
$username = "ifs-dev"
$email = "ifs-dev@contoso.example"
$projectPath = Join-Path $PSScriptRoot "../../src/backend/InfraFlowSculptor.AppHost/InfraFlowSculptor.AppHost.csproj"
$projectPath = [IO.Path]::GetFullPath($projectPath)

function Get-UserSecret([string]$Name) {
    $lines = & dotnet user-secrets list --project $projectPath
    if ($LASTEXITCODE -ne 0) {
        throw "Impossible de lire les secrets utilisateur de l'AppHost."
    }

    $prefix = "$Name = "
    $line = $lines | Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) } | Select-Object -First 1
    if ($null -eq $line) {
        return $null
    }

    return $line.Substring($prefix.Length)
}

function Set-UserSecret([string]$Name, [string]$Value) {
    & dotnet user-secrets set $Name $Value --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Impossible d'enregistrer le secret utilisateur '$Name'."
    }
}

function New-LocalPassword {
    $bytes = New-Object byte[] 36
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "A").Replace("/", "B")
}

function New-BasicAuthHeader([string]$User, [string]$Password) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${User}:$Password"))
    return @{ Authorization = "Basic $encoded" }
}

function Invoke-GiteaApi(
    [string]$Method,
    [string]$Path,
    [hashtable]$Headers,
    [object]$Body = $null
) {
    $parameters = @{
        Method      = $Method
        Uri         = "$giteaUrl/api/v1$Path"
        Headers     = $Headers
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 6 -Compress
    }

    return Invoke-RestMethod @parameters
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$healthy = $false
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    try {
        $null = Invoke-RestMethod -Uri "$giteaUrl/api/healthz" -TimeoutSec 3
        $healthy = $true
        break
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $healthy) {
    throw "Gitea ne répond pas sur $giteaUrl/api/healthz après $TimeoutSeconds secondes."
}

$password = Get-UserSecret "Gitea:Password"
if ([string]::IsNullOrWhiteSpace($password)) {
    $password = New-LocalPassword
    Set-UserSecret "Gitea:Password" $password
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    $adminCreateOutput = & docker exec --user git $containerName gitea admin user create `
        --admin `
        --username $username `
        --password $password `
        --email $email `
        --must-change-password=false 2>&1
    $adminCreateExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}
if ($adminCreateExitCode -ne 0 -and ($adminCreateOutput -join " ") -notmatch "already exists") {
    throw "La création du compte Gitea a échoué : $($adminCreateOutput -join ' ')"
}

$basicHeaders = New-BasicAuthHeader $username $password
$organizationExists = $true
try {
    $null = Invoke-GiteaApi "Get" "/orgs/contoso" $basicHeaders
}
catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) {
        throw
    }

    $organizationExists = $false
}

if (-not $organizationExists) {
    $null = Invoke-GiteaApi "Post" "/orgs" $basicHeaders @{
        username = "contoso"
        full_name = "Contoso"
        visibility = "private"
        repo_admin_change_team_access = $false
    }
}

foreach ($repository in @("shop", "shop-infra", "shop-app")) {
    $repositoryExists = $true
    try {
        $null = Invoke-GiteaApi "Get" "/repos/contoso/$repository" $basicHeaders
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) {
            throw
        }

        $repositoryExists = $false
    }

    if (-not $repositoryExists) {
        $null = Invoke-GiteaApi "Post" "/orgs/contoso/repos" $basicHeaders @{
            name = $repository
            auto_init = $true
            default_branch = "main"
            private = $true
        }
    }
}

$token = Get-UserSecret "Gitea:Token"
$tokenIsValid = $false
if (-not [string]::IsNullOrWhiteSpace($token)) {
    try {
        $tokenHeaders = @{ Authorization = "token $token" }
        $currentUser = Invoke-GiteaApi "Get" "/user" $tokenHeaders
        $tokenIsValid = $currentUser.login -eq $username
    }
    catch {
        $tokenIsValid = $false
    }
}

if (-not $tokenIsValid) {
    try {
        $existingTokens = Invoke-GiteaApi "Get" "/users/$username/tokens" $basicHeaders
        foreach ($existingToken in $existingTokens | Where-Object { $_.name -eq "ifs-dev" }) {
            $null = Invoke-GiteaApi "Delete" "/users/$username/tokens/$($existingToken.id)" $basicHeaders
        }
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) {
            throw
        }
    }

    $createdToken = Invoke-GiteaApi "Post" "/users/$username/tokens" $basicHeaders @{
        name = "ifs-dev"
        scopes = @("all")
    }
    $token = $createdToken.sha1
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Gitea n'a pas renvoyé de valeur de jeton pour 'ifs-dev'."
    }

    Set-UserSecret "Gitea:Token" $token
}

Write-Output "3 dépôts prêts : contoso/shop, contoso/shop-infra, contoso/shop-app."
