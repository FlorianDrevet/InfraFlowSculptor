using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using ErrorOr;

namespace InfraFlowSculptor.Domain.Tests.ComponentAggregate;

public sealed class AppSettingAggregateTests
{
    private static readonly ProjectId ProjectId = new(Guid.NewGuid());
    private static readonly OrganizationId OrganizationId = new(Guid.NewGuid());
    private static readonly Guid ComponentId = Guid.NewGuid();
    private static readonly Guid ApplicationResourceId = Guid.NewGuid();

    [Fact]
    public void RGPAR01VariableNameContainingColonIsRejectedWithLinuxExplanation()
    {
        var result = Create(new AppSettingDefinition(
            "ConnectionStrings:Main",
            AppSettingSourceKind.Literal,
            LiteralValue: "Host=localhost"));

        Assert.True(result.IsError);
        var error = Assert.Single(result.Errors);
        Assert.Equal("VAL-PAR-01", error.Code);
        Assert.Contains("Linux", error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void RGPAR03DuplicateNameIsRejectedAndNamesTheExistingSetting()
    {
        var existing = Create(new AppSettingDefinition(
            "Payments__ApiKey",
            AppSettingSourceKind.Literal,
            LiteralValue: "non-secret-value")).Value;

        var result = Create(
            new AppSettingDefinition("payments__apikey", AppSettingSourceKind.Literal, LiteralValue: "another-value"),
            [existing]);

        Assert.True(result.IsError);
        var error = Assert.Single(result.Errors);
        Assert.Equal("VAL-PAR-03", error.Code);
        Assert.Contains("Payments__ApiKey", error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void RGPAR05SuspiciousLiteralIsRejectedBeforeAnAggregateCanBeCreated()
    {
        var value = $"ghp_{new string('a', 36)}";

        var result = Create(new AppSettingDefinition(
            "Test__Token",
            AppSettingSourceKind.Literal,
            LiteralValue: value));

        Assert.True(result.IsError);
        Assert.Equal("VAL-PAR-SECRET-SUSPECT", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void RGPAR05ForcedSuspiciousLiteralIsDeclaredNonSecret()
    {
        var value = $"ghp_{new string('a', 36)}";

        var result = Create(new AppSettingDefinition(
            "Test__Token",
            AppSettingSourceKind.Literal,
            LiteralValue: value,
            ForceNonSecret: true));

        Assert.False(result.IsError);
        Assert.True(result.Value.SecretValueDeclaredNonSecret);
        Assert.Equal(value, result.Value.LiteralSource!.Value);
    }

    [Fact]
    public void RGPAR05ForcedSuspiciousTargetOverrideIsDeclaredNonSecret()
    {
        var targetId = Guid.NewGuid();
        var suspiciousOverride = $"ghp_{new string('a', 36)}";

        var result = Create(new AppSettingDefinition(
            "Test__Token",
            AppSettingSourceKind.Literal,
            LiteralValue: "safe-default",
            LiteralOverrides: new Dictionary<Guid, string?> { [targetId] = suspiciousOverride },
            ForceNonSecret: true));

        Assert.False(result.IsError);
        Assert.True(result.Value.SecretValueDeclaredNonSecret);
        Assert.Equal("safe-default", result.Value.LiteralSource!.Value);
        Assert.Equal(suspiciousOverride, Assert.Single(result.Value.LiteralSource.Overrides).Value);
    }

    [Fact]
    public void RGPAR08KeyVaultPipelineSecretStoresOnlyItsReference()
    {
        var vaultResourceId = Guid.NewGuid();

        var result = Create(new AppSettingDefinition(
            "Payments__ApiKey",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: vaultResourceId,
            SecretName: "payments-api-key",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: "MAIN_PAYMENTS_API_KEY"));

        Assert.False(result.IsError);
        Assert.Null(result.Value.LiteralSource);
        Assert.Equal(vaultResourceId, result.Value.KeyVaultResourceId);
        Assert.Equal("payments-api-key", result.Value.SecretName);
        Assert.Equal("MAIN_PAYMENTS_API_KEY", result.Value.PipelineVariableName);
    }

    [Fact]
    public void RGPAR12PipelineVariableNameIsUniqueAcrossProjectSettings()
    {
        var first = Create(new AppSettingDefinition(
            "Payments__ApiKey",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: Guid.NewGuid(),
            SecretName: "payments-api-key",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: "MAIN_PAYMENTS_API_KEY")).Value;

        var duplicate = Create(new AppSettingDefinition(
            "Payments__ApiKeyBackup",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: Guid.NewGuid(),
            SecretName: "payments-api-key-backup",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: "main_payments_api_key"), [first]);

        Assert.True(duplicate.IsError);
        Assert.Equal("VAL-PAR-12", Assert.Single(duplicate.Errors).Code);
    }

    [Fact]
    public void RGPAR12PipelineVariableWhitespaceCannotBypassUniqueness()
    {
        var first = Create(new AppSettingDefinition(
            "Payments__ApiKey",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: Guid.NewGuid(),
            SecretName: "payments-api-key",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: "MAIN_PAYMENTS_API_KEY")).Value;

        var duplicate = Create(new AppSettingDefinition(
            "Payments__ApiKeyBackup",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: Guid.NewGuid(),
            SecretName: "payments-api-key-backup",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: " MAIN_PAYMENTS_API_KEY "), [first]);

        Assert.True(duplicate.IsError);
        Assert.Equal("VAL-PAR-12", Assert.Single(duplicate.Errors).Code);
    }

    [Fact]
    public void DEC105PipelineSecretHasOneWriterPerVaultAndSecret()
    {
        var vaultId = Guid.NewGuid();
        var first = Create(new AppSettingDefinition(
            "Payments__ApiKey",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: vaultId,
            SecretName: "payments-api-key",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: "MAIN_PAYMENTS_API_KEY")).Value;

        var duplicateWriter = Create(new AppSettingDefinition(
            "Checkout__ApiKey",
            AppSettingSourceKind.KeyVaultSecret,
            KeyVaultResourceId: vaultId,
            SecretName: "PAYMENTS-API-KEY",
            SecretFeedKind: AppSettingSecretFeedKind.PipelineSecret,
            PipelineVariableName: "CHECKOUT_PAYMENTS_API_KEY"), [first]);

        Assert.True(duplicateWriter.IsError);
        Assert.Equal("VAL-PAR-SECRET-PROPRIETAIRE", Assert.Single(duplicateWriter.Errors).Code);
    }

    private static ErrorOr<AppSettingAggregate> Create(
        AppSettingDefinition definition,
        IReadOnlyCollection<AppSettingAggregate>? existing = null) => AppSettingAggregate.Create(
            Guid.NewGuid(),
            ProjectId,
            OrganizationId,
            ComponentId,
            ApplicationResourceId,
            definition,
            existing);
}
