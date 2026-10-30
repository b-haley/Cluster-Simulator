using System.Windows;
using ClusterSim.Wpf.ViewModels;

namespace ClusterSim.Wpf.Views;

public partial class WorkloadEditWindow : Window
{
    public WorkloadEditWindow(WorkloadEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    public WorkloadDefinition? Result => ((WorkloadEditorViewModel)DataContext).Result;

    private void OnCloseRequested(bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}