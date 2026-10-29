namespace InfraFlowSculptor.Domain.Common.Extensibility;

public interface IKeyed<TKey> where TKey : notnull
{
    TKey Key { get; }
}
