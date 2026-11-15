namespace InfraFlowSculptor.Domain.PublicationAggregate;

public static class PublicationValidationCodes
{
    public const string InvalidRequest = "VAL-PUB-DEMANDE";
    public const string InvalidTarget = "VAL-PUB-DESTINATION";
    public const string InvalidFiles = "VAL-PUB-FICHIERS";
    public const string InvalidPullRequest = "VAL-PUB-PULL-REQUEST";
    public const string RevisionNotGenerated = "VAL-PUB-REVISION";
    public const string StaleRevision = "VAL-PUB-REVISION-PERIMEE";
    public const string ProjectBusy = "VAL-PUB-PROJET-OCCUPE";
    public const string OperationNotReady = "VAL-PUB-ETAT";
    public const string BranchChanged = "VAL-PUB-BRANCHE-AVANCEE";
    public const string PermissionRevoked = "VAL-PUB-DROIT-RETIRE";
    public const string ManifestInvalid = "VAL-PUB-MANIFESTE";
    public const string ExtensionMissing = "VAL-APP-EXTENSION";
    public const string ProviderFailure = "VAL-PUB-FOURNISSEUR";
}
