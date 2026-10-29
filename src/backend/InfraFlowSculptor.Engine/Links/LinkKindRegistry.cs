using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;

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
}
