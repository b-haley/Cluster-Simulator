using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ClusterSim.Wpf.Themes;

public enum ThemeKind
{
    NeonDashboard,
    CleanLight
}

public interface IThemeService
{
    ThemeKind Current { get; }
    IReadOnlyList<ThemeKind> Available { get; }
    void Apply(ThemeKind kind);
    void Restore();
}

public sealed class ThemeService : IThemeService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClusterSim");
    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "theme.txt");

    private static readonly Dictionary<ThemeKind, Uri> Sources = new()
    {
        [ThemeKind.NeonDashboard] = new Uri("pack://application:,,,/Themes/NeonDashboard.xaml", UriKind.Absolute),
        [ThemeKind.CleanLight] = new Uri("pack://application:,,,/Themes/CleanLight.xaml", UriKind.Absolute)
    };

    public ThemeKind Current { get; private set; } = ThemeKind.NeonDashboard;
    public IReadOnlyList<ThemeKind> Available { get; } = new[] { ThemeKind.NeonDashboard, ThemeKind.CleanLight };

    public void Apply(ThemeKind kind)
    {
        var resources = Application.Current.Resources;
        var currentTheme = resources.MergedDictionaries.FirstOrDefault(d => d.Contains("ThemeName"));
        if (currentTheme is not null) resources.MergedDictionaries.Remove(currentTheme);

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = Sources[kind] });
        Current = kind;

        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsFile, kind.ToString());
        }
        catch (IOException) { }
    }

    public void Restore()
    {
        var kind = ThemeKind.NeonDashboard;
        try
        {
            if (File.Exists(SettingsFile)) Enum.TryParse(File.ReadAllText(SettingsFile).Trim(), out kind);
        }
        catch (IOException) { }
        Apply(kind);
    }
}