using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ClusterSim.Core.Simulation.Export;
using ClusterSim.Infrastructure;
using ClusterSim.Infrastructure.Configuration;
using ClusterSim.Infrastructure.Persistence;
using ClusterSim.Wpf.Services;
using ClusterSim.Wpf.Themes;
using ClusterSim.Wpf.ViewModels;

namespace ClusterSim.Wpf;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        Services.GetRequiredService<IThemeService>().Restore();
        TryInitializeDatabaseSchema();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };

        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddClusterSimPostgres(LoadPostgresOptions());

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ClusterDesignerService>();
        services.AddSingleton<SimulationRunner>();
        services.AddSingleton<ReportExporter>();
        services.AddSingleton<ClusterDesignerViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    private static void TryInitializeDatabaseSchema()
    {
        try
        {
            Services.GetRequiredService<PostgresSchemaInitializer>().Initialize();
        }
        catch (Exception)
        {
        }
    }

    private static PostgresOptions LoadPostgresOptions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");

        if (!File.Exists(path))
        {
            return new PostgresOptions();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
                connectionStrings.TryGetProperty("Postgres", out var connectionString) &&
                connectionString.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(connectionString.GetString()))
            {
                return PostgresOptions.FromConnectionString(connectionString.GetString()!);
            }
        }
        catch (JsonException)
        {
        }

        return new PostgresOptions();
    }
}