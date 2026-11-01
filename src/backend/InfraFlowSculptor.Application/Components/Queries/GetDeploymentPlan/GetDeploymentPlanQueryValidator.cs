using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Queries.GetDeploymentPlan;

public sealed class GetDeploymentPlanQueryValidator : AbstractValidator<GetDeploymentPlanQuery>
{
    public GetDeploymentPlanQueryValidator()
    {
        RuleFor(query => query.ProjectId).NotEmpty();
        RuleFor(query => query.Component).MaximumLength(64);
        RuleFor(query => query.Target).MaximumLength(64);
    }
}
