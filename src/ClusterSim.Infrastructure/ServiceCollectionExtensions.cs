using Microsoft.Extensions.DependencyInjection;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Infrastructure.Configuration;
using ClusterSim.Infrastructure.Persistence;

namespace ClusterSim.Infrastructure;

/// <summary>
/// Точка подключения PostgreSQL-инфраструктуры к приложению.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует опции, фабрику подключений, инициализатор схемы
    /// и PostgreSQL-реализацию хранилища конфигураций кластеров.
    /// </summary>
    public static IServiceCollection AddClusterSimPostgres(
        this IServiceCollection services,
        PostgresOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(options ?? new PostgresOptions());
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<PostgresSchemaInitializer>();
        services.AddSingleton<IClusterDefinitionStore, PostgresClusterDefinitionStore>();

        return services;
    }
}