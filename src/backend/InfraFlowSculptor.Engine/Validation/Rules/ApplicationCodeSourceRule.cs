using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ApplicationCodeSourceRule : IValidationRule
{
    public string Code => "VAL-APP-CODE-SOURCE";

    public FindingSeverity Severity => FindingSeverity.ErrorAtPublish;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Applications
        .Where(application => string.IsNullOrWhiteSpace(application.CodeSourcePath))
        .Select(application => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Application, application.Id),
            $"L’application « {application.Name} » n’a pas de chemin de code source pour la publication. La génération utilisera le chemin par défaut.",
            $"Application '{application.Name}' has no code source path for publishing. Generation will use the default path."))
        .ToArray();
}
