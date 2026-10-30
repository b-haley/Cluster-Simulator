using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation.Scheduling;

namespace ClusterSim.Wpf.ViewModels;

/// <summary>Редактируемая строка узла в окне редактора кластера.</summary>
public sealed partial class ClusterNodeRow : ObservableObject
{
    [ObservableProperty] private string _name = "node";
    [ObservableProperty] private int _cpuCores = 8;
    [ObservableProperty] private int _ramGb = 32;
    [ObservableProperty] private int _diskGb = 512;
}

/// <summary>
/// ViewModel модального окна редактирования/создания кластера.
/// </summary>
public sealed partial class ClusterEditorViewModel : ObservableObject
{
    private Guid? _existingId;

    public event Action<bool>? CloseRequested;

    public ClusterEditorViewModel()
    {
        Nodes = new ObservableCollection<ClusterNodeRow>();
    }

    public ObservableCollection<ClusterNodeRow> Nodes { get; }
    public IReadOnlyList<SchedulingPolicyKind> AvailablePolicies { get; } = Enum.GetValues<SchedulingPolicyKind>();

    public ClusterDefinition? Result { get; private set; }

    [ObservableProperty] private string _clusterName = "Новый кластер";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ClusterNodeRow? _selectedNode;

    [ObservableProperty] private SchedulingPolicyKind _policy = SchedulingPolicyKind.Fifo;
    [ObservableProperty] private bool _strictFifo = true;
    [ObservableProperty] private bool _allowPreemption;

    [ObservableProperty] private int _templateNodeCount = 4;
    [ObservableProperty] private int _templateCpuCores = 8;
    [ObservableProperty] private int _templateRamGb = 32;
    [ObservableProperty] private int _templateDiskGb = 512;

    public int TotalNodes => Nodes.Count;
    public int TotalCores => Nodes.Sum(node => node.CpuCores);
    public int TotalRamGb => Nodes.Sum(node => node.RamGb);
    public int TotalDiskGb => Nodes.Sum(node => node.DiskGb);

    /// <summary>Заполняет редактор: существующим кластером, стандартом или пустым новым.</summary>
    public void Load(ClusterDefinition? definition, bool standard)
    {
        if (definition is not null)
        {
            _existingId = definition.Id;
            ClusterName = definition.Name;
            Description = definition.Description;
            Policy = definition.Policy;
            StrictFifo = definition.StrictFifo;
            AllowPreemption = definition.AllowPreemption;

            foreach (var node in definition.Nodes)
            {
                var row = new ClusterNodeRow
                {
                    Name = node.Name,
                    CpuCores = node.CpuCores,
                    RamGb = Math.Max(1, node.RamMb / 1024),
                    DiskGb = node.DiskGb
                };
                Attach(row);
                Nodes.Add(row);
            }

            NotifyTotals();
            return;
        }

        if (standard)
        {
            ClusterName = "Стандартный кластер";
            Description = "Типовая конфигурация: 4 узла по 8 ядер, 32 ГБ RAM, 512 ГБ диска.";
            TemplateNodeCount = 4;
            TemplateCpuCores = 8;
            TemplateRamGb = 32;
            TemplateDiskGb = 512;
            GenerateNodesFromTemplate();
        }
        else
        {
            ClusterName = "Новый кластер";
            TemplateNodeCount = 2;
            GenerateNodesFromTemplate();
        }
    }

    [RelayCommand]
    private void AddNode()
    {
        var row = new ClusterNodeRow
        {
            Name = $"node-{Nodes.Count:D2}",
            CpuCores = TemplateCpuCores,
            RamGb = TemplateRamGb,
            DiskGb = TemplateDiskGb
        };
        Attach(row);
        Nodes.Add(row);
        NotifyTotals();
    }

    [RelayCommand]
    private void RemoveSelectedNode()
    {
        if (SelectedNode is null) return;
        Detach(SelectedNode);
        Nodes.Remove(SelectedNode);
        SelectedNode = null;
        NotifyTotals();
    }

    [RelayCommand]
    private void GenerateNodesFromTemplate()
    {
        foreach (var row in Nodes.ToList()) Detach(row);
        Nodes.Clear();

        var count = Math.Clamp(TemplateNodeCount, 1, 512);
        for (var index = 0; index < count; index++)
        {
            var row = new ClusterNodeRow
            {
                Name = $"node-{index:D2}",
                CpuCores = TemplateCpuCores,
                RamGb = TemplateRamGb,
                DiskGb = TemplateDiskGb
            };
            Attach(row);
            Nodes.Add(row);
        }

        NotifyTotals();
    }

    [RelayCommand]
    private void Save()
    {
        if (Nodes.Count == 0)
        {
            return;
        }

        Result = new ClusterDefinition
        {
            Id = _existingId ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(ClusterName) ? "Без названия" : ClusterName,
            Description = Description,
            Policy = Policy,
            StrictFifo = StrictFifo,
            AllowPreemption = AllowPreemption,
            Nodes = Nodes
                .Select(node => new ClusterNodeDefinition(node.Name, node.CpuCores, node.RamGb * 1024, node.DiskGb))
                .ToList()
        };

        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    private void Attach(ClusterNodeRow row) => row.PropertyChanged += OnRowChanged;
    private void Detach(ClusterNodeRow row) => row.PropertyChanged -= OnRowChanged;
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => NotifyTotals();

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalNodes));
        OnPropertyChanged(nameof(TotalCores));
        OnPropertyChanged(nameof(TotalRamGb));
        OnPropertyChanged(nameof(TotalDiskGb));
    }
}