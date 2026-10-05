using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public sealed class StronglyTypedIdValueConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, IStronglyTypedId
{
    public StronglyTypedIdValueConverter()
        : base(
            id => id.Value,
            value => (TId)Activator.CreateInstance(typeof(TId), value)!)
    {
    }
}
