using System.Text;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Emitters.InstallKit;

public static class InstallationChecklistMarkdownRenderer
{
    private const string ChecklistHeading = "## Liste de contrôle d’installation";

    public static string Append(string setup, InstallationChecklist checklist)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setup);
        ArgumentNullException.ThrowIfNull(checklist);

        var existingChecklistStart = setup.LastIndexOf(ChecklistHeading, StringComparison.Ordinal);
        var staticSetup = existingChecklistStart < 0 ? setup : setup[..existingChecklistStart];
        var markdown = new StringBuilder(staticSetup.TrimEnd());
        markdown.Append("\n\n\n").Append(ChecklistHeading).Append("\n\n");
        foreach (var section in checklist.Sections)
        {
            markdown.Append("### ").Append(Escape(section.Title)).Append('\n');
            markdown.Append("> ").Append(Escape(section.Subtitle)).Append("\n\n");
            foreach (var item in section.Items)
            {
                var check = item.State == InstallationChecklistItemState.Automatic
                    ? "[x] "
                    : item.IsUserActionable ? "[ ] " : string.Empty;
                markdown.Append("- ").Append(check)
                    .Append(StatusLabel(item.State)).Append(" — ")
                    .Append(Escape(item.Text));
                if (item.IsNewSincePrevious)
                {
                    markdown.Append(" — **Nouveau depuis la révision précédente**");
                }

                markdown.Append('\n');
                if (item.Command is not null)
                {
                    markdown.Append("\n    ```powershell\n")
                        .Append("    ").Append(item.Command).Append('\n')
                        .Append("    ```\n\n");
                }
            }

            markdown.Append('\n');
        }

        return markdown.ToString().TrimEnd() + "\n";
    }

    private static string StatusLabel(InstallationChecklistItemState state) => state switch
    {
        InstallationChecklistItemState.Automatic => "Automatique",
        InstallationChecklistItemState.ToDo => "À faire",
        InstallationChecklistItemState.Done => "Fait",
        InstallationChecklistItemState.Verified => "Vérifié",
        InstallationChecklistItemState.NeedsReverification => "À revérifier",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown installation checklist item state.")
    };

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal)
        .Replace("*", "\\*", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal);
}
