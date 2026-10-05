using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Infrastructure.Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Persistence;

public static class IfsDbContextRegistrationExtensions
{
    public static IHostApplicationBuilder AddIfsDbContext(this IHostApplicationBuilder builder, string name)
    {
        builder.Services.AddSingleton(serviceProvider => PostgresDataSourceFactory.Create(
            builder.Configuration,
            name,
            serviceProvider.GetRequiredService<IfsAzureCredential>()));
        builder.Services.AddDbContext<IfsDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>())
                .UseSnakeCaseNamingConvention());
        builder.Services.AddScoped<IIfsDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<IfsDbContext>());
        builder.EnrichNpgsqlDbContext<IfsDbContext>();

        return builder;
    }
}
