using System.Security.Cryptography;
using System.Text;

namespace InfraFlowSculptor.Engine.Links;

internal static class LinkIdFactory
{
    public static Guid Create(string rule, Guid sourceResourceId, Guid targetResourceId)
    {
        var input = Encoding.UTF8.GetBytes($"{rule}|{sourceResourceId:N}|{targetResourceId:N}");
        return new Guid(SHA256.HashData(input)[..16]);
    }
}
