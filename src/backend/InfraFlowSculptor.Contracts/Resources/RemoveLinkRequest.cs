using System.Text.Json.Serialization;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record RemoveLinkRequest(
    [property: JsonRequired] int ExpectedLinkVersion,
    [property: JsonRequired] int ExpectedModelVersion);
