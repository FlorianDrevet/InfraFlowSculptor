namespace InfraFlowSculptor.Engine.Plan;

public sealed record DiagnosticSettingPlan(
    string SourceResourceType,
    string SourceLogicalName,
    string SourceName,
    string Name,
    DiagnosticDestinationPlan Destination,
    string? LogCategoryGroup,
    string? MetricCategory,
    string LogAnalyticsDestinationType,
    IReadOnlyList<string> ExcludedCategories);
