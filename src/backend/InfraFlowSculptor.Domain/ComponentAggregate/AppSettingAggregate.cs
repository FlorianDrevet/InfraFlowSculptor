using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

[AuditedObject(ObjectType.AppSetting, nameof(AppSettingAggregate.Id), nameof(AppSettingAggregate.OrganizationId))]
public sealed partial class AppSettingAggregate : AggregateRoot<Guid>, IOrganizationOwned, IHasVersion
{
    private AppSettingLiteralSource? literalSource;

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public ComponentId ComponentId { get; private set; }

    public Guid ApplicationResourceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NameKey { get; private set; } = string.Empty;

    public AppSettingSourceKind SourceKind { get; private set; }

    public AppSettingLiteralSource? LiteralSource
    {
        get => literalSource;
        private set => literalSource = value;
    }

    public Guid? SourceResourceId { get; private set; }

    public string? OutputName { get; private set; }

    public Guid? KeyVaultResourceId { get; private set; }

    public string? SecretName { get; private set; }

    public string? SecretNameKey { get; private set; }

    public AppSettingSecretFeedKind? SecretFeedKind { get; private set; }

    public string? PipelineVariableName { get; private set; }

    public string? PipelineVariableNameKey { get; private set; }

    public Guid? IdentityResourceId { get; private set; }

    public bool SecretValueDeclaredNonSecret { get; private set; }

    public int Version { get; private set; } = 1;

    private AppSettingAggregate()
    {
    }

    private AppSettingAggregate(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid componentId,
        Guid applicationResourceId) : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        ComponentId = new ComponentId(componentId);
        ApplicationResourceId = applicationResourceId;
    }

    public static ErrorOr<AppSettingAggregate> Create(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        Guid componentId,
        Guid applicationResourceId,
        AppSettingDefinition definition,
        IReadOnlyCollection<AppSettingAggregate>? existingSettings = null)
    {
        if (id == Guid.Empty || projectId.Value == Guid.Empty || organizationId.Value == Guid.Empty
            || componentId == Guid.Empty || applicationResourceId == Guid.Empty)
        {
            return Error.Validation("APP_SETTING_IDENTITY_REQUIRED", "A setting, project, component, application, and organization are required.");
        }

        var normalizedName = definition.Name?.Trim() ?? string.Empty;
        var validated = ValidateDefinition(
            applicationResourceId,
            normalizedName,
            definition,
            existingSettings ?? []);
        if (validated.IsError)
        {
            return validated.Errors;
        }

        var setting = new AppSettingAggregate(id, projectId, organizationId, componentId, applicationResourceId);
        setting.Apply(normalizedName, definition);
        return setting;
    }

    public ErrorOr<Success> Update(
        AppSettingDefinition definition,
        IReadOnlyCollection<AppSettingAggregate>? existingSettings = null)
    {
        var normalizedName = definition.Name?.Trim() ?? string.Empty;
        var validated = ValidateDefinition(
            ApplicationResourceId,
            normalizedName,
            definition,
            (existingSettings ?? []).Where(setting => setting.Id != Id).ToArray());
        if (validated.IsError)
        {
            return validated.Errors;
        }

        Apply(normalizedName, definition);
        Touch();
        return Result.Success;
    }

    private static ErrorOr<Success> ValidateDefinition(
        Guid applicationResourceId,
        string normalizedName,
        AppSettingDefinition definition,
        IReadOnlyCollection<AppSettingAggregate> existingSettings)
    {
        if (!VariableNamePattern().IsMatch(normalizedName))
        {
            var detail = normalizedName.Contains(':', StringComparison.Ordinal)
                ? "A colon is not supported in an environment variable name on Linux. Use double underscore for hierarchy."
                : "An environment variable name must match ^[A-Za-z_][A-Za-z0-9_.-]{0,255}$.";
            return Error.Validation("VAL-PAR-01", detail);
        }

        if (!Enum.IsDefined(definition.SourceKind))
        {
            return Error.Validation("APP_SETTING_SOURCE_INVALID", "The application setting source is unsupported.");
        }

        var duplicate = existingSettings.FirstOrDefault(setting =>
            setting.ApplicationResourceId == applicationResourceId
            && string.Equals(setting.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            return Error.Conflict("VAL-PAR-03", $"A parameter named '{duplicate.Name}' already exists on this application.");
        }

        var sourceResult = ValidateSource(normalizedName, definition);
        if (sourceResult.IsError)
        {
            return sourceResult.Errors;
        }

        if (definition.SourceKind == AppSettingSourceKind.KeyVaultSecret
            && definition.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
            && existingSettings.Any(setting =>
                setting.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                && string.Equals(setting.PipelineVariableName, definition.PipelineVariableName?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict("VAL-PAR-12", "A pipeline variable with this name already exists in the project secret store.");
        }

        if (definition.SourceKind == AppSettingSourceKind.KeyVaultSecret
            && definition.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret)
        {
            var duplicateWriter = existingSettings.FirstOrDefault(setting =>
                setting.KeyVaultResourceId == definition.KeyVaultResourceId
                && string.Equals(setting.SecretName, definition.SecretName, StringComparison.OrdinalIgnoreCase)
                && setting.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret);
            if (duplicateWriter is not null)
            {
                return Error.Conflict(
                    "VAL-PAR-SECRET-PROPRIETAIRE",
                    "This physical Key Vault secret already has a pipeline writer.");
            }
        }

        return Result.Success;
    }

    private static ErrorOr<Success> ValidateSource(string name, AppSettingDefinition definition)
    {
        var hasOverrides = definition.LiteralOverrides is { Count: > 0 };
        var sourceFieldsAreClear = definition.SourceResourceId is null
            && definition.OutputName is null
            && definition.KeyVaultResourceId is null
            && definition.SecretName is null
            && definition.SecretFeedKind is null
            && definition.PipelineVariableName is null
            && definition.IdentityResourceId is null;

        switch (definition.SourceKind)
        {
            case AppSettingSourceKind.Literal:
                if (definition.LiteralValue is null || !sourceFieldsAreClear)
                {
                    return Error.Validation("APP_SETTING_SOURCE_INVALID", "A literal source requires a value and cannot contain resource or Key Vault references.");
                }

                var hasSuspiciousLiteralValue = SecretValueDetector.IsSuspicious(definition.LiteralValue)
                    || definition.LiteralOverrides?.Values.Any(value => value is not null && SecretValueDetector.IsSuspicious(value)) == true;
                if (definition.ForceNonSecret && !hasSuspiciousLiteralValue)
                {
                    return Error.Validation("APP_SETTING_SOURCE_INVALID", "A value can be declared non-secret only when the base value or a target override resembles a secret.");
                }

                if (definition.LiteralOverrides is not null
                    && definition.LiteralOverrides.Any(value => value.Key == Guid.Empty || value.Value is null))
                {
                    return Error.Validation("APP_SETTING_OVERRIDE_INVALID", "Each literal override requires a target and a value.");
                }

                if (SecretValueDetector.IsSuspicious(definition.LiteralValue) && !definition.ForceNonSecret)
                {
                    return Error.Validation(
                        "VAL-PAR-SECRET-SUSPECT",
                        "The literal value resembles a secret. Convert it to a pipeline secret, or declare it non-secret to force this value.");
                }

                if (definition.LiteralOverrides?.Values.Any(value => value is not null && SecretValueDetector.IsSuspicious(value)) == true
                    && !definition.ForceNonSecret)
                {
                    return Error.Validation(
                        "VAL-PAR-SECRET-SUSPECT",
                        "A target override resembles a secret. Convert it to a pipeline secret, or declare it non-secret to force this value.");
                }

                return Result.Success;

            case AppSettingSourceKind.ResourceOutput:
                if (definition.LiteralValue is not null || hasOverrides || definition.SourceResourceId is null
                    || definition.SourceResourceId == Guid.Empty || string.IsNullOrWhiteSpace(definition.OutputName)
                    || definition.KeyVaultResourceId is not null || definition.SecretName is not null
                    || definition.SecretFeedKind is not null || definition.PipelineVariableName is not null
                    || definition.IdentityResourceId is not null || definition.ForceNonSecret)
                {
                    return Error.Validation("APP_SETTING_SOURCE_INVALID", "A resource output source requires one resource and output name only.");
                }

                return Result.Success;

            case AppSettingSourceKind.KeyVaultSecret:
                if (definition.LiteralValue is not null || hasOverrides || definition.SourceResourceId is not null
                    || definition.OutputName is not null || definition.KeyVaultResourceId is null
                    || definition.KeyVaultResourceId == Guid.Empty || string.IsNullOrWhiteSpace(definition.SecretName)
                    || !SecretNamePattern().IsMatch(definition.SecretName)
                    || definition.SecretFeedKind is null || !Enum.IsDefined(definition.SecretFeedKind.Value)
                    || definition.ForceNonSecret || definition.IdentityResourceId == Guid.Empty)
                {
                    return Error.Validation("APP_SETTING_SOURCE_INVALID", "A Key Vault source requires a valid vault, secret name, and feed type.");
                }

                if (definition.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                    && !PipelineVariableNamePattern().IsMatch(definition.PipelineVariableName?.Trim() ?? string.Empty))
                {
                    return Error.Validation("VAL-PAR-12", "A pipeline secret requires a variable name matching ^[A-Za-z_][A-Za-z0-9_]{0,127}$.");
                }

                if (definition.SecretFeedKind == AppSettingSecretFeedKind.ManagedOutsideIfs
                    && !string.IsNullOrWhiteSpace(definition.PipelineVariableName))
                {
                    return Error.Validation("APP_SETTING_SOURCE_INVALID", "A secret managed outside IFS cannot declare a pipeline variable.");
                }

                return Result.Success;

            default:
                return Error.Validation("APP_SETTING_SOURCE_INVALID", $"The source for '{name}' is unsupported.");
        }
    }

    private void Apply(string normalizedName, AppSettingDefinition definition)
    {
        Name = normalizedName;
        NameKey = normalizedName.ToUpperInvariant();
        SourceKind = definition.SourceKind;
        LiteralSource = definition.SourceKind == AppSettingSourceKind.Literal
            ? new AppSettingLiteralSource(definition.LiteralValue!, definition.LiteralOverrides)
            : null;
        SourceResourceId = definition.SourceKind == AppSettingSourceKind.ResourceOutput ? definition.SourceResourceId : null;
        OutputName = definition.SourceKind == AppSettingSourceKind.ResourceOutput ? definition.OutputName?.Trim() : null;
        KeyVaultResourceId = definition.SourceKind == AppSettingSourceKind.KeyVaultSecret ? definition.KeyVaultResourceId : null;
        SecretName = definition.SourceKind == AppSettingSourceKind.KeyVaultSecret ? definition.SecretName?.Trim() : null;
        SecretNameKey = SecretName?.ToUpperInvariant();
        SecretFeedKind = definition.SourceKind == AppSettingSourceKind.KeyVaultSecret ? definition.SecretFeedKind : null;
        PipelineVariableName = definition.SourceKind == AppSettingSourceKind.KeyVaultSecret
            && definition.SecretFeedKind == AppSettingSecretFeedKind.PipelineSecret
                ? definition.PipelineVariableName?.Trim()
                : null;
        PipelineVariableNameKey = PipelineVariableName?.ToUpperInvariant();
        IdentityResourceId = definition.SourceKind == AppSettingSourceKind.KeyVaultSecret ? definition.IdentityResourceId : null;
        SecretValueDeclaredNonSecret = definition.SourceKind == AppSettingSourceKind.Literal
            && definition.ForceNonSecret
            && (SecretValueDetector.IsSuspicious(definition.LiteralValue)
                || definition.LiteralOverrides?.Values.Any(value => value is not null && SecretValueDetector.IsSuspicious(value)) == true);
    }

    private void Touch() => Version = checked(Version + 1);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.-]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableNamePattern();

    [GeneratedRegex("^[A-Za-z0-9-]{1,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretNamePattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex PipelineVariableNamePattern();
}
