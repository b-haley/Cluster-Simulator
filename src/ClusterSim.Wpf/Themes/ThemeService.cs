using System.IO;
using System.Linq;
using System.Windows;

namespace ClusterSim.Wpf.Themes;

/// <summary>Доступные темы оформления.</summary>
public enum ThemeKind
{
    FluentDark,
    NeonDashboard,
    CleanLight
}

/// <summary>Управление темой приложения: применение и восстановление выбора.</summary>
public interface IThemeService
{
    ThemeKind Current { get; }
    IReadOnlyList<ThemeKind> Available { get; }
    void Apply(ThemeKind kind);
    void Restore();
}

/// <summary>
/// Переключает словари тем в Application.Resources и сохраняет выбор на диск.
/// Тема ищется по маркеру ThemeName, поэтому словари подменяются без перезапуска.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClusterSim");

    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "theme.txt");

    private static readonly Dictionary<ThemeKind, Uri> Sources = new()
    {
        [ThemeKind.FluentDark] = new Uri("pack://application:,,,/Themes/FluentDark.xaml", UriKind.Absolute),
        [ThemeKind.NeonDashboard] = new Uri("pack://application:,,,/Themes/NeonDashboard.xaml", UriKind.Absolute),
        [ThemeKind.CleanLight] = new Uri("pack://application:,,,/Themes/CleanLight.xaml", UriKind.Absolute)
    };

    public ThemeKind Current { get; private set; } = ThemeKind.FluentDark;

    public IReadOnlyList<ThemeKind> Available { get; } = Enum.GetValues<ThemeKind>();

    public void Apply(ThemeKind kind)
    {
        var resources = Application.Current.Resources;
        var currentTheme = resources.MergedDictionaries.FirstOrDefault(d => d.Contains("ThemeName"));

        if (currentTheme is not null)
        {
            resources.MergedDictionaries.Remove(currentTheme);
        }

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = Sources[kind] });
        Current = kind;

        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsFile, kind.ToString());
        }
        catch (IOException)
        {
            // Нет доступа к диску: тема применится только до выхода из приложения.
        }
    }

    public void Restore()
    {
        var kind = ThemeKind.FluentDark;

        try
        {
            if (File.Exists(SettingsFile))
            {
                Enum.TryParse(File.ReadAllText(SettingsFile).Trim(), out kind);
            }
        }
        catch (IOException)
        {
            // Файл поврежден: используем тему по умолчанию.
        }

        Apply(kind);
    }
}