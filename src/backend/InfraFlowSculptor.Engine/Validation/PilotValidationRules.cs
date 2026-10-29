using InfraFlowSculptor.Engine.Validation.Rules;

namespace InfraFlowSculptor.Engine.Validation;

public static class PilotValidationRules
{
    public static IReadOnlyList<IValidationRule> Create() =>
    [
        new ProjectEnvironmentRule(),
        new ProtectedEnvironmentApproverRule(),
        new ComponentWithoutEnvironmentRule(),
        new EmptyComponentRule(),
        new ComponentCycleRule(),
        new ComponentDirectionRule(),
        new ComponentEnvironmentRule(),
        new ProjectTagsRule(),
        new SystemTagsDisabledRule(),
        new NamingLengthRule(),
        new NamingEmptyRule(),
        new NamingCollisionRule(),
        new ResourceRequiredPropertyRule(),
        new ResourceConstraintRule(),
        new ResourcePresenceRule(),
        new ExistingResourceIdRule(),
        new ResourceAbsentEverywhereRule(),
        new CatalogRegionRule(),
        new LinkPlacementRule(),
        new RequiredLinkRule(),
        new SharedIdentityRule(),
        new ExternalLinkScopeRule(),
        new SqlAdministratorMembershipRule(),
        new AppSettingSecretSuspectRule(),
        new AppSettingConflictRule(),
        new AppSettingSecretOwnerRule(),
        new AppSettingManagedOutsideIfsRule(),
        new ApplicationCodeSourceRule(),
        new ApplicationStrategyRule(),
        new ApplicationStepRule(),
        new RestrictedExposureRule()
    ];
}
