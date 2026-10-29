using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Validation;
using Microsoft.Extensions.Caching.Hybrid;

namespace InfraFlowSculptor.Application.Components.Validation;

public sealed class ProjectValidationService(HybridCache cache, ValidationEngine engine)
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15)
    };

    public ValueTask<ValidationResult> GetAsync(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        CancellationToken cancellationToken = default)
    {
        var reservations = snapshot.OrganizationGlobalNames
            .OrderBy(reservation => reservation.ResourceType, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.Name, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.ProjectId)
            .ToArray();
        var reservationFingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reservations))));
        var cacheKey = $"project-validation:{snapshot.Project.Id:D}:{snapshot.Project.ModelVersion}:{catalog.Version}:{reservationFingerprint}";
        return cache.GetOrCreateAsync(
            cacheKey,
            _ => ValueTask.FromResult(engine.Evaluate(snapshot, catalog)),
            CacheOptions,
            [$"project:{snapshot.Project.Id:D}"],
            cancellationToken);
    }
}
