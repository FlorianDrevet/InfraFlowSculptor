using System.Diagnostics.CodeAnalysis;

namespace InfraFlowSculptor.Domain.Common.Security;

[SuppressMessage("Naming", "CA1711", Justification = "Le nom Permission est imposé par l'étape J0-05 du plan.")]
public static class Permission
{
    public const string ProjectRead = "project.read";
    public const string AuditRead = "audit.read";
    public const string ModelEdit = "model.edit";
    public const string ApplicationsManage = "applications.manage";
    public const string ComponentsManage = "components.manage";
    public const string ConventionsManage = "conventions.manage";
    public const string EnvironmentsManage = "environments.manage";
    public const string ProtectedEnvironmentsManage = "environments.protected.manage";
    public const string PublicationManage = "publication.manage";
    public const string Generate = "generate";
    public const string Publish = "publish";
    public const string InstallationManage = "installation.manage";
    public const string ApplyProposals = "proposals.apply";
    public const string MembersManage = "members.manage";
    public const string ProjectAdminister = "project.administer";

    public static IReadOnlyCollection<string> All { get; } = Array.AsReadOnly(
    [
        ProjectRead,
        AuditRead,
        ModelEdit,
        ApplicationsManage,
        ComponentsManage,
        ConventionsManage,
        EnvironmentsManage,
        ProtectedEnvironmentsManage,
        PublicationManage,
        Generate,
        Publish,
        InstallationManage,
        ApplyProposals,
        MembersManage,
        ProjectAdminister
    ]);
}
