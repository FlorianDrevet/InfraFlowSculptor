namespace InfraFlowSculptor.Api.Common;

public static class FeatureEndpointExtensions
{
    public static RouteGroupBuilder RequireFeature(this RouteGroupBuilder builder, string featureKey) =>
        builder.WithMetadata(new RequiredFeature(featureKey));

    public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string featureKey) =>
        builder.WithMetadata(new RequiredFeature(featureKey));
}
