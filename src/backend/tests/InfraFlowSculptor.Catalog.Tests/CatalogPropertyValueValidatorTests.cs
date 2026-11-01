using System.Text.Json;
using InfraFlowSculptor.Catalog;

namespace InfraFlowSculptor.Catalog.Tests;

public sealed class CatalogPropertyValueValidatorTests
{
    [Fact]
    public void RejectsAFormatAppliedToANonStringValue()
    {
        using var value = JsonDocument.Parse("42");
        using var definition = JsonDocument.Parse("""
            {
              "name": "capacity",
              "kind": "integer",
              "overridable": true,
              "format": "ipv4Cidr"
            }
            """);

        var failure = CatalogPropertyValueValidator.Validate(value.RootElement, definition.RootElement);

        Assert.NotNull(failure);
        Assert.Equal(CatalogPropertyValueFailureKind.Format, failure.Kind);
    }

    [Fact]
    public void RejectsNumbersThatCannotBeRepresentedByTheCatalogNumericRange()
    {
        using var value = JsonDocument.Parse("1e300");
        using var definition = JsonDocument.Parse("""
            { "name": "capacity", "kind": "number", "overridable": true }
            """);

        var failure = CatalogPropertyValueValidator.Validate(value.RootElement, definition.RootElement);

        Assert.NotNull(failure);
        Assert.Equal(CatalogPropertyValueFailureKind.Type, failure.Kind);
    }

    [Fact]
    public void RejectsDuplicateObjectMembersInTypedValues()
    {
        using var value = JsonDocument.Parse("""
            { "name": "D4", "name": "E4" }
            """);
        using var definition = JsonDocument.Parse("""
            {
              "name": "profiles",
              "kind": "object",
              "members": [
                { "name": "name", "kind": "string", "required": true }
              ]
            }
            """);

        var failure = CatalogPropertyValueValidator.Validate(value.RootElement, definition.RootElement);

        Assert.NotNull(failure);
        Assert.Equal(CatalogPropertyValueFailureKind.DuplicateObjectMember, failure.Kind);
        Assert.Equal("name", failure.MemberName);
    }

    [Fact]
    public void RejectsNullForRequiredObjectMembers()
    {
        using var value = JsonDocument.Parse("""
            { "name": null }
            """);
        using var definition = JsonDocument.Parse("""
            {
              "name": "profile",
              "kind": "object",
              "members": [
                { "name": "name", "kind": "string", "required": true }
              ]
            }
            """);

        var failure = CatalogPropertyValueValidator.Validate(value.RootElement, definition.RootElement);

        Assert.NotNull(failure);
        Assert.Equal(CatalogPropertyValueFailureKind.RequiredObjectMember, failure.Kind);
        Assert.Equal("name", failure.MemberName);
    }

    [Fact]
    public void UniqueValueIdentityTreatsEquivalentJsonNumberFormatsAsTheSameValue()
    {
        using var integer = JsonDocument.Parse("1");
        using var decimalNumber = JsonDocument.Parse("1.0");

        Assert.Equal(
            CatalogPropertyValueValidator.GetUniqueValueIdentityKey(integer.RootElement),
            CatalogPropertyValueValidator.GetUniqueValueIdentityKey(decimalNumber.RootElement));
    }

    [Fact]
    public void RejectsDuplicateUniqueByValuesWithEquivalentJsonNumberFormats()
    {
        using var value = JsonDocument.Parse("""
            [{"id":1},{"id":1.0}]
            """);
        using var definition = JsonDocument.Parse("""
            {
              "name": "entries",
              "kind": "array",
              "items": {
                "kind": "object",
                "uniqueBy": "id",
                "members": [
                  { "name": "id", "kind": "number", "required": true }
                ]
              }
            }
            """);

        var failure = CatalogPropertyValueValidator.Validate(value.RootElement, definition.RootElement);

        Assert.NotNull(failure);
        Assert.Equal(CatalogPropertyValueFailureKind.DuplicateItemValue, failure.Kind);
    }

    [Fact]
    public void CatalogValueDisplayRemovesFormatCharactersAndTruncatesLongNames()
    {
        Assert.Equal("A�B", CatalogValueDisplay.SafeDisplay("A\u202EB"));
        Assert.Equal(new string('x', 64) + "…", CatalogValueDisplay.SafeDisplay(new string('x', 65)));
    }
}
