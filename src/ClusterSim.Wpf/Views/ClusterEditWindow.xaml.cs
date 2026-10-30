using System.Windows;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Wpf.ViewModels;

namespace ClusterSim.Wpf.Views;

/// <summary>
/// Модальное окно редактирования/создания кластера.
/// </summary>
public partial class ClusterEditWindow : Window
{
    public ClusterEditWindow(ClusterEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    /// <summary>Результат редактирования после закрытия окна с DialogResult=true.</summary>
    public ClusterDefinition? Result => ((ClusterEditorViewModel)DataContext).Result;

    private void OnCloseRequested(bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}