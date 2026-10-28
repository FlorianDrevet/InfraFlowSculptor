using ErrorOr;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed class ProjectEnvironment
{
    private readonly List<ProjectTag> tags = [];
    private string[] approvers = [];
    private string[] runners = [];

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public int Position { get; private set; }

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

    public string Description { get; private set; } = string.Empty;

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public IReadOnlyCollection<ProjectTag> Tags => tags.AsReadOnly();

    private ProjectEnvironment()
    {
    }

    internal ProjectEnvironment(
        Guid id,
        string name,
        string code,
        int position,
        Guid subscriptionId,
        string region,
        string deploymentConnection,
        bool isProtected,
        IEnumerable<string> approvers,
        IEnumerable<string> runners,
        string description,
        IEnumerable<KeyValuePair<string, string>> tags)
    {
        Id = id;
        Name = name;
        Code = code;
        Position = position;
        SubscriptionId = subscriptionId;
        Region = region;
        DeploymentConnection = deploymentConnection;
        IsProtected = isProtected;
        Approvers = approvers.ToArray();
        Runners = runners.ToArray();
        Description = description;
        this.tags.AddRange(tags.Select(tag => new ProjectTag(tag.Key, tag.Value)));
    }

    internal void SetPosition(int position) => Position = position;

    internal void RecordPublication(DateTimeOffset publishedAt) => FirstPublishedAt ??= publishedAt;

    internal ErrorOr<Success> Update(
        string name,
        string code,
        Guid subscriptionId,
        string region,
        bool isProtected,
        IEnumerable<string> approvers,
        IEnumerable<string> runners,
        string description,
        IEnumerable<KeyValuePair<string, string>> tags,
        bool confirmImpact,
        string? deploymentConnection = null)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCode = code?.Trim() ?? string.Empty;
        var normalizedRegion = region?.Trim() ?? string.Empty;
        var normalizedConnection = string.IsNullOrWhiteSpace(deploymentConnection)
            ? DeploymentConnection
            : deploymentConnection.Trim();
        var normalizedDescription = description?.Trim() ?? string.Empty;
        var normalizedApprovers = NormalizeApprovers(approvers);
        var normalizedRunners = runners.Select(runner => runner.Trim()).Where(runner => runner.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalizedTags = ProjectAggregate.ValidateTags(tags);
        if (normalizedName.Length is < 1 or > 50)
        {
            return Error.Validation("ENVIRONMENT_NAME_INVALID", "An environment name must contain between 1 and 50 characters.");
        }

        if (!ProjectAggregate.EnvironmentCodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("ENVIRONMENT_CODE_INVALID", "An environment code must match ^[a-z][a-z0-9]{1,7}$.");
        }

        if (FirstPublishedAt is not null && !string.Equals(Code, normalizedCode, StringComparison.Ordinal))
        {
            return Error.Conflict("RG-ENV-03-CODE-LOCKED", "The environment code cannot change after publication.");
        }

        if (subscriptionId == Guid.Empty || normalizedRegion.Length == 0)
        {
            return Error.Validation("ENVIRONMENT_TARGET_REQUIRED", "An Azure subscription and catalog region are required.");
        }

        if (normalizedConnection.Length is < 1 or > 100)
        {
            return Error.Validation("ENVIRONMENT_CONNECTION_INVALID", "A deployment connection name must contain between 1 and 100 characters.");
        }

        if (isProtected && normalizedApprovers.Length == 0)
        {
            return Error.Validation("RG-ENV-APPROVER-REQUIRED", "A protected environment requires at least one approver.");
        }

        if (normalizedDescription.Length > 500)
        {
            return Error.Validation("ENVIRONMENT_DESCRIPTION_INVALID", "An environment description cannot exceed 500 characters.");
        }

        if (normalizedTags.IsError)
        {
            return normalizedTags.Errors;
        }

        var targetChanged = SubscriptionId != subscriptionId
            || !string.Equals(Region, normalizedRegion, StringComparison.OrdinalIgnoreCase);
        if (FirstPublishedAt is not null && targetChanged && !confirmImpact)
        {
            return Error.Conflict(
                "RG-ENV-03-CONFIRMATION-REQUIRED",
                "Changing a published environment target requires explicit confirmation. Affected resources are listed in affectedResources; this plan step has not introduced resource records yet.",
                new Dictionary<string, object> { ["affectedResources"] = Array.Empty<string>() });
        }

        Name = normalizedName;
        Code = normalizedCode;
        SubscriptionId = subscriptionId;
        Region = normalizedRegion;
        DeploymentConnection = normalizedConnection;
        IsProtected = isProtected;
        Approvers = normalizedApprovers;
        Runners = normalizedRunners;
        Description = normalizedDescription;
        ProjectAggregate.ReplaceTags(this.tags, normalizedTags.Value);
        return Result.Success;
    }

    internal static ErrorOr<ProjectEnvironment> Create(
        Guid id,
        string name,
        string code,
        int position,
        Guid subscriptionId,
        string region,
        string deploymentConnection,
        bool isProtected,
        IEnumerable<string> approvers,
        IEnumerable<string> runners,
        string description,
        IEnumerable<KeyValuePair<string, string>> tags)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCode = code?.Trim() ?? string.Empty;
        var normalizedRegion = region?.Trim() ?? string.Empty;
        var normalizedConnection = deploymentConnection?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;
        var normalizedApprovers = NormalizeApprovers(approvers);
        var normalizedRunners = runners.Select(runner => runner.Trim()).Where(runner => runner.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalizedTags = ProjectAggregate.ValidateTags(tags);

        if (id == Guid.Empty || position < 1)
        {
            return Error.Validation("ENVIRONMENT_IDENTITY_REQUIRED", "An environment identifier and a positive position are required.");
        }

        if (normalizedName.Length is < 1 or > 50)
        {
            return Error.Validation("ENVIRONMENT_NAME_INVALID", "An environment name must contain between 1 and 50 characters.");
        }

        if (!ProjectAggregate.EnvironmentCodePattern().IsMatch(normalizedCode))
        {
            return Error.Validation("ENVIRONMENT_CODE_INVALID", "An environment code must match ^[a-z][a-z0-9]{1,7}$.");
        }

        if (subscriptionId == Guid.Empty || normalizedRegion.Length == 0)
        {
            return Error.Validation("ENVIRONMENT_TARGET_REQUIRED", "An Azure subscription and catalog region are required.");
        }

        if (normalizedConnection.Length is < 1 or > 100)
        {
            return Error.Validation("ENVIRONMENT_CONNECTION_INVALID", "A deployment connection name is required.");
        }

        if (isProtected && normalizedApprovers.Length == 0)
        {
            return Error.Validation("RG-ENV-APPROVER-REQUIRED", "A protected environment requires at least one approver.");
        }

        if (normalizedDescription.Length > 500)
        {
            return Error.Validation("ENVIRONMENT_DESCRIPTION_INVALID", "An environment description cannot exceed 500 characters.");
        }

        if (normalizedTags.IsError)
        {
            return normalizedTags.Errors;
        }

        return new ProjectEnvironment(
            id,
            normalizedName,
            normalizedCode,
            position,
            subscriptionId,
            normalizedRegion,
            normalizedConnection,
            isProtected,
            normalizedApprovers,
            normalizedRunners,
            normalizedDescription,
            normalizedTags.Value);
    }

    private static string[] NormalizeApprovers(IEnumerable<string> values) => values
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
