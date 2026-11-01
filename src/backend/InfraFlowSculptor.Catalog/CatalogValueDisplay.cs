using System.Globalization;
using System.Text;

namespace InfraFlowSculptor.Catalog;

public static class CatalogValueDisplay
{
    private const int MaximumDisplayRunes = 64;

    public static string SafeDisplay(string value)
    {
        var runes = value.EnumerateRunes().Take(MaximumDisplayRunes + 1).ToArray();
        var displayed = string.Concat(runes.Take(MaximumDisplayRunes).Select(rune =>
            Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format
                ? "�"
                : rune.ToString()));
        return runes.Length > MaximumDisplayRunes ? $"{displayed}…" : displayed;
    }
}
