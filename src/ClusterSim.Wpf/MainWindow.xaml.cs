using System.Windows;

namespace ClusterSim.Wpf;

/// <summary>
/// Код главного окна минимален: вся логика находится в MainViewModel.
/// DataContext назначается в App после сборки DI-контейнера.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}