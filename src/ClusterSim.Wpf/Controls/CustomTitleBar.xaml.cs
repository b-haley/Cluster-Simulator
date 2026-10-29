using System.Windows;
using System.Windows.Controls;

namespace ClusterSim.Wpf.Controls;

public partial class CustomTitleBar : UserControl
{
    public CustomTitleBar()
    {
        InitializeComponent();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null) return;
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }
}