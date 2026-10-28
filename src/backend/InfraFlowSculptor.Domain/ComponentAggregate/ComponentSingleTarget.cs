using ErrorOr;
using InfraFlowSculptor.Domain.ProjectAggregate;
using System.Text.RegularExpressions;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed partial class ComponentSingleTarget
{
    private string[] approvers = [];
    private string[] runners = [];

    public string Code { get; private set; } = string.Empty;

    public Guid SubscriptionId { get; private set; }

    public string Region { get; private set; } = string.Empty;

    public string DeploymentConnection { get; private set; } = string.Empty;

    public bool IsProtected { get; private set; }

    public string[] Approvers
    {
        get => approvers;
        private set => approvers = value;
    }

    public string[] Runners
    {
        get => runners;
        private set => runners = value;
    }

    private ComponentSingleTarget()
    {
    }

    private ComponentSingleTarget(
        string code,
        Guid subscriptionId,
        string region,
        string deploymentConnection,
        bool isProtected,
        IEnumerable<string> approvers,
        IEnumerable<string> runners)
    {
        Code = code;
        SubscriptionId = subscriptionId;
        Region = region;
        DeploymentConnection = deploymentConnection;
        IsProtected = isProtected;
        Approvers = approvers.ToArray();
        Runners = runners.ToArray();
    }

    public static ErrorOr<ComponentSingleTarget> Create(
        string projectCode,
        string? code,
        Guid subscriptionId,
        string region,
        string? deploymentConnection,
        bool? isProtected,
        IEnumerable<string>? approvers,
        IEnumerable<string>? runners,
        IEnumerable<ProjectEnvironment> projectEnvironments,
        IEnumerable<string>? projectDefaultRunners)
    {
        var normalizedProjectCode = projectCode?.Trim() ?? string.Empty;
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? "shared" : code.Trim();
        var normalizedRegion = region?.Trim() ?? string.Empty;
        var environments = projectEnvironments.OrderBy(environment => environment.Position).ToArray();

        if (!CodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("COMPONENT_SINGLE_TARGET_CODE_INVALID", "The Single target code must match ^[a-z][a-z0-9]{1,7}$.");
        }

        if (subscriptionId == Guid.Empty)
        {
            return Error.Validation("COMPONENT_SINGLE_TARGET_SUBSCRIPTION_REQUIRED", "The Single target requires a subscription.");
        }

        if (normalizedRegion.Length is < 1 or > 64)
        {
            return Error.Validation("COMPONENT_SINGLE_TARGET_REGION_INVALID", "The Single target region must contain between 1 and 64 characters.");
        }

        if (environments.Length == 0)
        {
            return Error.Validation("COMPONENT_SINGLE_TARGET_ENVIRONMENT_REQUIRED", "A Single target needs the final project environment for its defaults.");
        }

        if (environments.Any(environment => string.Equals(environment.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Conflict(
                "COMPONENT_SINGLE_TARGET_ENVIRONMENT_CODE_CONFLICT",
                "The Single target code must differ from every project environment code.");
        }

        var normalizedConnection = string.IsNullOrWhiteSpace(deploymentConnection)
            ? $"ifs-{normalizedProjectCode}-{normalizedCode}"
            : deploymentConnection.Trim();
        if (normalizedConnection.Length is < 1 or > 100)
        {
            return Error.Validation("COMPONENT_SINGLE_TARGET_CONNECTION_INVALID", "The deployment connection must contain between 1 and 100 characters.");
        }

        var normalizedApprovers = Normalize(approvers ?? environments[^1].Approvers);
        var protectedTarget = isProtected ?? true;
        if (protectedTarget && normalizedApprovers.Length == 0)
        {
            return Error.Validation(
                "COMPONENT_SINGLE_TARGET_APPROVER_REQUIRED",
                "A protected Single target requires at least one approver.");
        }

        return new ComponentSingleTarget(
            normalizedCode,
            subscriptionId,
            normalizedRegion,
            normalizedConnection,
            protectedTarget,
            normalizedApprovers,
            Normalize(runners ?? projectDefaultRunners ?? []));
    }

    private static string[] Normalize(IEnumerable<string> values) => values
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    [GeneratedRegex("^[a-z][a-z0-9]{1,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
