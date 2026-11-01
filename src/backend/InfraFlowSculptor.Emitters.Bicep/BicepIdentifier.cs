namespace InfraFlowSculptor.Emitters.Bicep;

internal static class BicepIdentifier
{
    public static string FromLogicalName(string logicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        if (logicalName.Length > 40 || logicalName[0] is < 'a' or > 'z')
        {
            throw new InvalidOperationException(
                $"Resource group logical name '{logicalName}' cannot be mapped to a Bicep identifier.");
        }

        var identifier = new char[logicalName.Length];
        for (var index = 0; index < logicalName.Length; index++)
        {
            var character = logicalName[index];
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                identifier[index] = character;
            }
            else if (character == '-')
            {
                identifier[index] = '_';
            }
            else
            {
                throw new InvalidOperationException(
                    $"Resource group logical name '{logicalName}' cannot be mapped to a Bicep identifier.");
            }
        }

        return new string(identifier);
    }
}
