using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public sealed class LinkKindRegistry
{
    private readonly ILinkKindHandler[] handlerValues;

    private readonly Registry<LinkKind, ILinkKindHandler> registry;

    public LinkKindRegistry(IEnumerable<ILinkKindHandler> handlers)
    {
        handlerValues = handlers.OrderBy(handler => handler.Key).ToArray();
        registry = new Registry<LinkKind, ILinkKindHandler>(handlerValues);
    }

    public IReadOnlyCollection<LinkKind> Keys => registry.Keys;

    public IReadOnlyList<ILinkKindHandler> Values => handlerValues;

    public bool Supports(LinkKind kind) => registry.Supports(kind);

    public ILinkKindHandler Get(LinkKind kind) => registry.Get(kind);

    public IReadOnlyList<LinkSnapshot> GetFromSource(ModelSnapshot snapshot, Guid sourceResourceId, LinkKind kind)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (sourceResourceId == Guid.Empty)
        {
            throw new ArgumentException("A source resource id is required.", nameof(sourceResourceId));
        }

        var requestedHandler = Get(kind);
        return snapshot.Links
            .Where(link => link.SourceResourceId == sourceResourceId && Supports(link.Kind))
            .Where(link => ReferenceEquals(Get(link.Kind), requestedHandler))
            .ToArray();
    }
}
