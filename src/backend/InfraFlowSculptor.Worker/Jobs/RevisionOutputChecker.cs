using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class RevisionOutputChecker
{
    internal const string TemporaryDirectoryCleanupFailureDataKey = "TemporaryDirectoryCleanupFailure";
    private const string PowerShellYamlModule = "powershell-yaml";
    private const string SchemaResourcePrefix = "IFS-Catalog/catalog/";
    private readonly OutputCheckerRegistry outputCheckers;
    private readonly Func<string, Task> deleteTemporaryDirectory;

    public RevisionOutputChecker(OutputCheckerRegistry outputCheckers)
        : this(outputCheckers, DeleteTemporaryDirectoryAsync)
    {
    }

    internal RevisionOutputChecker(
        OutputCheckerRegistry outputCheckers,
        Func<string, Task> deleteTemporaryDirectory)
    {
        ArgumentNullException.ThrowIfNull(outputCheckers);
        ArgumentNullException.ThrowIfNull(deleteTemporaryDirectory);
        this.outputCheckers = outputCheckers;
        this.deleteTemporaryDirectory = deleteTemporaryDirectory;
    }

    public async Task CheckAsync(
        IReadOnlyList<GeneratedFile> files,
        IacLanguage language,
        CatalogPins pins,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(pins);

        await outputCheckers.Get(language).CheckAsync(files, pins, cancellationToken);

        var hasYaml = files.Any(file => IsYaml(file.RelativePath));
        var hasPowerShell = files.Any(file => IsPowerShell(file.RelativePath));
        if (!hasYaml && !hasPowerShell)
        {
            return;
        }

        var hasYamlModulePin = pins.PowerShellModules.TryGetValue(PowerShellYamlModule, out var yamlModuleVersion);
        if (hasYaml && !hasYamlModulePin)
        {
            throw new InvalidOperationException("The catalog does not pin the powershell-yaml module.");
        }

        var temporaryDirectory = CreateTemporaryDirectory();
        Exception? validationFailure = null;
        try
        {
            var outputRoot = Path.Combine(temporaryDirectory, "output");
            Directory.CreateDirectory(outputRoot);
            WriteGeneratedFiles(outputRoot, files, cancellationToken);
            string? schemaPath = null;
            if (hasYaml)
            {
                schemaPath = Path.Combine(temporaryDirectory, "azure-pipelines.schema.json");
                var schema = ReadPinnedPipelineSchema(pins);
                await File.WriteAllBytesAsync(schemaPath, schema, cancellationToken);
            }

            var scriptPath = Path.Combine(temporaryDirectory, "check-output.ps1");
            await File.WriteAllTextAsync(scriptPath, CreateCheckScript(), new UTF8Encoding(false), cancellationToken);
            var command = await PowerShellCommandRunner.RunAsync(
                temporaryDirectory,
                [
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    scriptPath,
                    outputRoot,
                    schemaPath ?? string.Empty,
                    yamlModuleVersion ?? string.Empty,
                    WorkerToolVersions.PowerShellScriptAnalyzer,
                    hasYaml.ToString(),
                    hasPowerShell.ToString()
                ],
                cancellationToken);

            if (command.ExitCode != 0)
            {
                throw new InvalidOperationException("The YAML schema or PowerShell script check failed.");
            }
        }
        catch (Exception exception)
        {
            validationFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                await deleteTemporaryDirectory(temporaryDirectory);
            }
            catch (Exception cleanupException) when (validationFailure is not null)
            {
                validationFailure.Data[TemporaryDirectoryCleanupFailureDataKey] = cleanupException;
            }
        }
    }

    private static byte[] ReadPinnedPipelineSchema(CatalogPins pins)
    {
        var resourcePath = $"{SchemaResourcePrefix}{pins.Version}/schemas/azure-pipelines.json";
        var assembly = typeof(CatalogLoader).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => string.Equals(
                name.Replace('\\', '/'),
                resourcePath,
                StringComparison.Ordinal));
        if (resourceName is null)
        {
            throw new InvalidOperationException("The pinned Azure Pipelines schema is unavailable.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The pinned Azure Pipelines schema could not be opened.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var schema = buffer.ToArray();
        var actualHash = Convert.ToHexString(SHA256.HashData(schema)).ToLowerInvariant();
        if (!string.Equals(actualHash, pins.PipelineValidation.AzurePipelinesSchema.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The pinned Azure Pipelines schema hash does not match the catalog.");
        }

        return schema;
    }

    private static void WriteGeneratedFiles(
        string root,
        IReadOnlyList<GeneratedFile> files,
        CancellationToken cancellationToken)
    {
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = GetSafeOutputPath(root, file.RelativePath);
            if (!destinations.Add(destination))
            {
                throw new InvalidOperationException($"Generated output path '{file.RelativePath}' is duplicated.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, file.Content, new UTF8Encoding(false));
        }
    }

    private static string GetSafeOutputPath(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("A generated output path must be relative.");
        }

        if (relativePath.Contains('\\'))
        {
            throw new InvalidOperationException("A generated output path must use forward slash separators.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relativeToRoot)
            || relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A generated output path escapes its validation directory.");
        }

        return fullPath;
    }

    private static string CreateCheckScript() => """
        param(
            [Parameter(Mandatory = $true)][string]$Root,
            [Parameter(Mandatory = $true)][AllowEmptyString()][string]$SchemaPath,
            [Parameter(Mandatory = $true)][AllowEmptyString()][string]$YamlModuleVersion,
            [Parameter(Mandatory = $true)][string]$AnalyzerVersion,
            [Parameter(Mandatory = $true)][string]$CheckYaml,
            [Parameter(Mandatory = $true)][string]$CheckPowerShell
        )
        $ErrorActionPreference = 'Stop'
        if ([bool]::Parse($CheckYaml)) {
            Import-Module powershell-yaml -RequiredVersion $YamlModuleVersion -ErrorAction Stop
            $yamlFiles = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force | Where-Object {
                $_.Extension -in @('.yml', '.yaml')
            })
            foreach ($file in $yamlFiles) {
                $document = ConvertFrom-Yaml -Yaml (Get-Content -LiteralPath $file.FullName -Raw) -ErrorAction Stop
                $relativePath = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
                if (-not $relativePath.StartsWith('.ifs/templates/', [StringComparison]::OrdinalIgnoreCase)) {
                    $json = ConvertTo-Json -InputObject $document -Depth 100 -ErrorAction Stop
                    if (-not (Test-Json -Json $json -SchemaFile $SchemaPath -ErrorAction Stop)) {
                        throw 'A generated YAML pipeline does not match the pinned schema.'
                    }
                }
            }
        }
        if ([bool]::Parse($CheckPowerShell)) {
            Import-Module PSScriptAnalyzer -RequiredVersion $AnalyzerVersion -ErrorAction Stop
            $scriptFiles = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force | Where-Object {
                $_.Extension -in @('.ps1', '.psm1')
            })
            $findings = @()
            foreach ($file in $scriptFiles) {
                $tokens = $null
                $parseErrors = $null
                [System.Management.Automation.Language.Parser]::ParseFile(
                    $file.FullName,
                    [ref]$tokens,
                    [ref]$parseErrors
                ) | Out-Null
                if ($parseErrors.Count -gt 0) {
                    throw 'A generated PowerShell file contains parser errors.'
                }
                $findings += @(Invoke-ScriptAnalyzer -Path $file.FullName -Severity Error -ErrorAction Stop)
            }
            if ($findings.Count -gt 0) {
                throw 'A generated PowerShell file contains ScriptAnalyzer findings.'
            }
        }
        """;

    private static bool IsYaml(string path) =>
        string.Equals(Path.GetExtension(path), ".yml", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase);

    private static bool IsPowerShell(string path) =>
        string.Equals(Path.GetExtension(path), ".ps1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetExtension(path), ".psm1", StringComparison.OrdinalIgnoreCase);

    private static string CreateTemporaryDirectory()
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var root = Path.GetFullPath(Path.Combine(temporaryRoot, $"ifs-revision-check-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryDirectory(string root)
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(root);
        if (!fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullPath).StartsWith("ifs-revision-check-", StringComparison.Ordinal)
            || !Directory.Exists(fullPath))
        {
            return;
        }

        Directory.Delete(fullPath, recursive: true);
    }

    private static Task DeleteTemporaryDirectoryAsync(string root)
    {
        DeleteTemporaryDirectory(root);
        return Task.CompletedTask;
    }
}
