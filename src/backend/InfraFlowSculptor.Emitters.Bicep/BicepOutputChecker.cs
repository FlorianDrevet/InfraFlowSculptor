using System.Text;
using System.Text.RegularExpressions;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.Bicep;

public sealed class BicepOutputChecker : IOutputChecker
{
    internal const string TemporaryDirectoryCleanupFailureDataKey = "TemporaryDirectoryCleanupFailure";
    private const int TemporaryDirectoryCleanupAttempts = 10;
    private static readonly TimeSpan TemporaryDirectoryCleanupRetryInterval = TimeSpan.FromMilliseconds(100);
    private static readonly Regex WarningPattern = new(
        @"(?im)(^|\s)warning\b|BCP\d{3}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IBicepCommandRunner runner;
    private readonly Func<string, Task> deleteTemporaryDirectory;

    public BicepOutputChecker() : this(new BicepCommandRunner(), DeleteTemporaryDirectoryAsync)
    {
    }

    internal BicepOutputChecker(IBicepCommandRunner runner)
        : this(runner, DeleteTemporaryDirectoryAsync)
    {
    }

    internal BicepOutputChecker(IBicepCommandRunner runner, Func<string, Task> deleteTemporaryDirectory)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(deleteTemporaryDirectory);
        this.runner = runner;
        this.deleteTemporaryDirectory = deleteTemporaryDirectory;
    }

    public IacLanguage Key => IacLanguage.Bicep;

    public async ValueTask CheckAsync(
        IReadOnlyList<GeneratedFile> files,
        CatalogPins pins,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(pins);

        var root = CreateTemporaryDirectory();
        Exception? validationFailure = null;
        try
        {
            var generatedPaths = WriteGeneratedFiles(root, files, cancellationToken);
            var mainFiles = generatedPaths
                .Where(path => path.Replace('\\', '/').EndsWith("/infra/main.bicep", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var parameterFiles = generatedPaths
                .Where(path => string.Equals(Path.GetExtension(path), ".bicepparam", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var bicepFiles = generatedPaths
                .Where(path => string.Equals(Path.GetExtension(path), ".bicep", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (mainFiles.Length == 0 || parameterFiles.Length == 0 || bicepFiles.Length == 0)
            {
                throw new InvalidOperationException(
                    "Bicep output validation requires at least one main.bicep, one .bicepparam, and one .bicep file.");
            }

            var version = await runner.RunAsync(root, ["--version"], cancellationToken);
            EnsureSuccess(version, "Bicep CLI version check");
            var versionOutput = version.StandardOutput + Environment.NewLine + version.StandardError;
            if (!ContainsExactVersion(versionOutput, pins.BicepCliVersion))
            {
                throw new InvalidOperationException(
                    $"Bicep CLI version '{versionOutput.Trim()}' does not match the pinned version '{pins.BicepCliVersion}'.");
            }

            foreach (var mainFile in mainFiles)
            {
                var relativePath = GetCliPath(root, mainFile);
                await RunCheckedAsync(root, ["build", relativePath, "--stdout"],
                    $"bicep build {relativePath}", cancellationToken);
                await RunCheckedAsync(root, ["lint", relativePath],
                    $"bicep lint {relativePath}", cancellationToken);
            }

            foreach (var parameterFile in parameterFiles)
            {
                var relativePath = GetCliPath(root, parameterFile);
                await RunCheckedAsync(root, ["build-params", relativePath, "--stdout"],
                    $"bicep build-params {relativePath}", cancellationToken);
            }

            for (var index = 0; index < bicepFiles.Length; index++)
            {
                var sourcePath = bicepFiles[index];
                var relativePath = GetCliPath(root, sourcePath);
                var formattedDirectory = Path.Combine(root, ".format-check");
                Directory.CreateDirectory(formattedDirectory);
                var formattedPath = Path.Combine(formattedDirectory, $"{index:D4}-{Path.GetFileName(sourcePath)}");
                var formattedRelativePath = GetCliPath(root, formattedPath);
                await RunCheckedAsync(root,
                    ["format", relativePath, "--outfile", formattedRelativePath,
                        "--newline-kind", "LF", "--insert-final-newline"],
                    $"bicep format {relativePath}", cancellationToken);

                var source = NormalizeNewlines(await File.ReadAllTextAsync(sourcePath, cancellationToken));
                var formatted = NormalizeNewlines(await File.ReadAllTextAsync(formattedPath, cancellationToken));
                if (!string.Equals(source, formatted, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Bicep format would change generated file '{relativePath}'.");
                }
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
                await deleteTemporaryDirectory(root);
            }
            catch (Exception cleanupException) when (validationFailure is not null)
            {
                validationFailure.Data[TemporaryDirectoryCleanupFailureDataKey] = cleanupException;
            }
        }
    }

    private async Task RunCheckedAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string description,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(workingDirectory, arguments, cancellationToken);
        EnsureSuccess(result, description);
        var output = result.StandardError;
        if (WarningPattern.IsMatch(output))
        {
            throw new InvalidOperationException($"{description} produced a warning: {output.Trim()}");
        }
    }

    private static bool ContainsExactVersion(string output, string pinnedVersion)
    {
        var versionPattern = $@"(?<![A-Za-z0-9_.+-]){Regex.Escape(pinnedVersion)}(?![A-Za-z0-9_.+-])";
        return Regex.IsMatch(output, versionPattern, RegexOptions.CultureInvariant);
    }

    private static void EnsureSuccess(BicepCommandResult result, string description)
    {
        if (result.ExitCode != 0)
        {
            var output = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
            throw new InvalidOperationException(
                $"{description} failed with exit code {result.ExitCode}: {output}");
        }
    }

    private static List<string> WriteGeneratedFiles(
        string root,
        IReadOnlyList<GeneratedFile> files,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>(files.Count);
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
            File.WriteAllText(destination, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            paths.Add(destination);
        }

        return paths;
    }

    private static string GetSafeOutputPath(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Generated output path '{relativePath}' must be relative.");
        }

        if (relativePath.Contains('\\'))
        {
            throw new InvalidOperationException($"Generated output path '{relativePath}' must use forward slash separators.");
        }

        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedPath));
        var relativeToRoot = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relativeToRoot)
            || relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Generated output path '{relativePath}' escapes its validation directory.");
        }

        var normalizedRelativePath = relativeToRoot.Replace('\\', '/');
        if (normalizedRelativePath == ".")
        {
            throw new InvalidOperationException($"Generated output path '{relativePath}' must identify a file.");
        }

        if (normalizedRelativePath.Equals(".format-check", StringComparison.OrdinalIgnoreCase)
            || normalizedRelativePath.StartsWith(".format-check/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Generated output path '{relativePath}' uses a reserved directory.");
        }

        return fullPath;
    }

    private static string CreateTemporaryDirectory()
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var root = Path.GetFullPath(Path.Combine(temporaryRoot, $"ifs-bicep-check-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Task DeleteTemporaryDirectoryAsync(string root) =>
        DeleteTemporaryDirectoryAsync(root, static (path, recursive) => Directory.Delete(path, recursive));

    internal static async Task DeleteTemporaryDirectoryAsync(string root, Action<string, bool> deleteDirectory)
    {
        ArgumentNullException.ThrowIfNull(deleteDirectory);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(root);
        if (!fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullPath).StartsWith("ifs-bicep-check-", StringComparison.Ordinal)
            || !Directory.Exists(fullPath))
        {
            return;
        }

        for (var attempt = 1; attempt <= TemporaryDirectoryCleanupAttempts; attempt++)
        {
            try
            {
                deleteDirectory(fullPath, true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (IOException) when (attempt < TemporaryDirectoryCleanupAttempts)
            {
                await Task.Delay(TemporaryDirectoryCleanupRetryInterval);
            }
            catch (UnauthorizedAccessException) when (attempt < TemporaryDirectoryCleanupAttempts)
            {
                await Task.Delay(TemporaryDirectoryCleanupRetryInterval);
            }
        }

    }

    private static string GetCliPath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
