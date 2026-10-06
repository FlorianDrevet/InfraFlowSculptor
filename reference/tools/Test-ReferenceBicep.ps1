[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$referenceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$components = @{
    core = @('dev', 'prd')
    data = @('dev', 'prd')
    platform = @('shared')
    orders = @('dev', 'prd')
}

function Invoke-BicepCheck {
    param(
        [Parameter(Mandatory)] [string[]] $Arguments,
        [Parameter(Mandatory)] [string] $Description
    )

    $output = & az bicep @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $diagnostics = @($output | Where-Object { "$_" -notmatch '^WARNING: A new Bicep release is available:' })
    $text = $diagnostics -join [Environment]::NewLine
    if ($exitCode -ne 0) {
        throw "$Description a échoué (code $exitCode) :`n$text"
    }

    if ($text -match '(?im)(^|\s)warning\b|BCP\d{3}') {
        throw "$Description a produit un avertissement :`n$text"
    }

    return $text
}

foreach ($component in $components.Keys | Sort-Object) {
    $infraRoot = Join-Path $referenceRoot "pilot/bicep-azdo/$component/infra"
    $mainPath = Join-Path $infraRoot 'main.bicep'
    if (-not (Test-Path -LiteralPath $mainPath)) {
        throw "Fichier manquant : $mainPath"
    }

    Push-Location $infraRoot
    try {
        [void](Invoke-BicepCheck -Arguments @('build', '--file', 'main.bicep', '--stdout') -Description "$component/main.bicep build")
        [void](Invoke-BicepCheck -Arguments @('lint', '--file', 'main.bicep') -Description "$component/main.bicep lint")

        $bicepFiles = Get-ChildItem -LiteralPath $infraRoot -Filter '*.bicep' -File -Recurse | Sort-Object FullName
        foreach ($bicepFile in $bicepFiles) {
            $relativePath = [IO.Path]::GetRelativePath($infraRoot, $bicepFile.FullName)
            $relativeLabel = $relativePath.Replace('\', '/')
            $formattedPath = Join-Path ([IO.Path]::GetTempPath()) "ifs-bicep-format-$component-$($bicepFile.BaseName)-$PID.bicep"
            try {
                [void](Invoke-BicepCheck -Arguments @('format', '--file', $relativePath, '--outfile', $formattedPath, '--newline-kind', 'LF', '--insert-final-newline') -Description "$component/$relativeLabel format")
                $formattedText = [IO.File]::ReadAllText($formattedPath).Replace("`r`n", "`n")
                $sourceText = [IO.File]::ReadAllText($bicepFile.FullName).Replace("`r`n", "`n")
                if ($formattedText -cne $sourceText) {
                    throw "$component/$relativeLabel n'est pas déjà au format officiel Bicep."
                }
            }
            finally {
                if (Test-Path -LiteralPath $formattedPath) {
                    Remove-Item -LiteralPath $formattedPath -Force
                }
            }
        }

        foreach ($target in $components[$component]) {
            $parameterFile = "main.$target.bicepparam"
            if (-not (Test-Path -LiteralPath (Join-Path $infraRoot $parameterFile))) {
                throw "Fichier de paramètres manquant : $component/$parameterFile"
            }

            [void](Invoke-BicepCheck -Arguments @('build-params', '--file', $parameterFile, '--stdout') -Description "$component/$parameterFile build")
        }

        Write-Host "OK $component"
    }
    finally {
        Pop-Location
    }
}

Write-Host 'Tous les fichiers Bicep sont valides, formatés et sans avertissement.'
