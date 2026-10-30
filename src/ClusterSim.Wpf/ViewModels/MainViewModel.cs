using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation;
using ClusterSim.Core.Simulation.Export;
using ClusterSim.Core.Simulation.Metrics;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Workload;
using ClusterSim.Wpf.Services;
using ClusterSim.Wpf.Themes;
using ClusterSim.Wpf.Views;

namespace ClusterSim.Wpf.ViewModels;

/// <summary>Сохраняемый профиль потока заданий.</summary>
public sealed class WorkloadDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Новый профиль заданий";
    public string Description { get; init; } = string.Empty;
    public WorkloadOptions Options { get; init; } = new();
}

/// <summary>Режим прогона симуляции.</summary>
public enum RunMode
{
    ClusterComparison,
    JointClusters
}

/// <summary>Карточка кластера для плитки во вкладке «Кластеры».</summary>
public sealed record ClusterCardRow(
    Guid Id,
    string Name,
    string Description,
    int NodeCount,
    int TotalCores,
    int TotalRamGb,
    int TotalDiskGb,
    string PolicyName,
    ClusterDefinition Definition);

/// <summary>Карточка профиля заданий для плитки во вкладке «Задания».</summary>
public sealed record WorkloadCardRow(
    Guid Id,
    string Name,
    string Description,
    string JobsInfo,
    string DurationInfo,
    string CpuInfo,
    string RamInfo,
    WorkloadDefinition Definition);

/// <summary>Строка таблицы загрузки кластеров в результатах.</summary>
public sealed record ClusterUtilizationRow(
    string ClusterLabel,
    int NodeCount,
    double CpuUtilization,
    double RamUtilization);

/// <summary>Внутренний результат одного прогона с меткой для интерфейса.</summary>
internal sealed record RunOutcome(
    string Label,
    RunResult Result,
    IReadOnlyList<(string Cluster, int NodeCount)>? JointRanges = null);

/// <summary>
/// Главная модель представления: карточки кластеров и заданий, модальные редакторы,
/// запуск в двух режимах, результаты с загрузкой по кластерам, экспорт, темы.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SimulationRunner _runner;
    private readonly ReportExporter _exporter;
    private readonly IThemeService _themeService;
    private readonly ClusterDesignerService _clusterService;

    private IReadOnlyList<RunResult>? _lastResults;

    public MainViewModel(
        SimulationRunner runner,
        ReportExporter exporter,
        IThemeService themeService,
        ClusterDesignerService clusterService)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _clusterService = clusterService ?? throw new ArgumentNullException(nameof(clusterService));

        _selectedTheme = themeService.Current;

        ClusterCards = new ObservableCollection<ClusterCardRow>();
        WorkloadCards = new ObservableCollection<WorkloadCardRow>();
        AvailableClusters = new ObservableCollection<ClusterDefinition>();
        AvailableWorkloads = new ObservableCollection<WorkloadDefinition>();
        SelectedClusters = new ObservableCollection<ClusterDefinition>();
        SelectedWorkloads = new ObservableCollection<WorkloadDefinition>();
        RunResults = new ObservableCollection<RunResult>();
        Comparison = new ObservableCollection<ComparisonRow>();
        ClusterUtilization = new ObservableCollection<ClusterUtilizationRow>();
        Nodes = new ObservableCollection<NodeSnapshot>();
        AllJobs = new ObservableCollection<JobSnapshot>();
        TimeSeries = new ObservableCollection<UtilizationPoint>();

        RefreshClustersFromStore();
        LoadWorkloads();
    }

    // ---------------- Коллекции интерфейса ----------------

    public ObservableCollection<ClusterCardRow> ClusterCards { get; }
    public ObservableCollection<WorkloadCardRow> WorkloadCards { get; }
    public ObservableCollection<ClusterDefinition> AvailableClusters { get; }
    public ObservableCollection<WorkloadDefinition> AvailableWorkloads { get; }
    public ObservableCollection<ClusterDefinition> SelectedClusters { get; }
    public ObservableCollection<WorkloadDefinition> SelectedWorkloads { get; }
    public ObservableCollection<RunResult> RunResults { get; }
    public ObservableCollection<ComparisonRow> Comparison { get; }
    public ObservableCollection<ClusterUtilizationRow> ClusterUtilization { get; }
    public ObservableCollection<NodeSnapshot> Nodes { get; }
    public ObservableCollection<JobSnapshot> AllJobs { get; }
    public ObservableCollection<UtilizationPoint> TimeSeries { get; }

    public IReadOnlyList<SchedulingPolicyKind> AvailablePolicies { get; } = Enum.GetValues<SchedulingPolicyKind>();
    public IReadOnlyList<ThemeKind> AvailableThemes { get; } = Enum.GetValues<ThemeKind>();
    public IReadOnlyList<RunMode> AvailableRunModes { get; } = Enum.GetValues<RunMode>();

    // ---------------- Тема ----------------

    [ObservableProperty] private ThemeKind _selectedTheme;
    partial void OnSelectedThemeChanged(ThemeKind value) => _themeService.Apply(value);

    // ---------------- Режим прогона ----------------

    /// <summary>Флаг для RadioButton режима сравнения кластеров.</summary>
    public bool IsComparisonModeChecked
    {
        get => SelectedRunMode == RunMode.ClusterComparison;
        set => SelectedRunMode = value ? RunMode.ClusterComparison : RunMode.JointClusters;
    }

    partial void OnSelectedRunModeChanged(RunMode value) => OnPropertyChanged(nameof(IsComparisonModeChecked));

    [ObservableProperty] private RunMode _selectedRunMode = RunMode.ClusterComparison;



    // ---------------- Политика запуска ----------------

    [ObservableProperty] private SchedulingPolicyKind _selectedPolicy = SchedulingPolicyKind.Fifo;
    [ObservableProperty] private bool _useClusterPolicy = true;
    [ObservableProperty] private bool _strictFifo = true;
    [ObservableProperty] private bool _allowPreemption;
    [ObservableProperty] private bool _compareAllPolicies;
    [ObservableProperty] private double _timeSeriesStep = 60;
    [ObservableProperty]
    private string _outputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClusterSim", "reports");

    // ---------------- Состояние и результаты ----------------

    [ObservableProperty] private string _statusMessage = "Готов к работе.";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _currentTime;
    [ObservableProperty] private double _makespan;
    [ObservableProperty] private double _averageWaitTime;
    [ObservableProperty] private double _averageTurnaround;
    [ObservableProperty] private double _averageSlowdown;
    [ObservableProperty] private double _cpuUtilization;
    [ObservableProperty] private double _ramUtilization;
    [ObservableProperty] private double _throughputPerHour;
    [ObservableProperty] private int _totalJobs;
    [ObservableProperty] private int _completedJobs;
    [ObservableProperty] private int _unfinishedJobs;
    [ObservableProperty] private int _rejectedJobs;
    [ObservableProperty] private int _preemptionCount;
    [ObservableProperty] private double _averageQueueLength;
    [ObservableProperty] private int _maxQueueLength;

    public bool HasResults => RunResults.Count > 0;
    public bool HasComparison => Comparison.Count > 0;

    // ---------------- Кластеры: карточки и редактор ----------------

    [RelayCommand]
    private void CreateCluster()
    {
        OpenClusterEditor(null, standard: false);
    }

    [RelayCommand]
    private void CreateStandardCluster()
    {
        var definition = new ClusterDefinition
        {
            Name = $"Стандартный кластер {AvailableClusters.Count + 1}",
            Description = "Типовая конфигурация: 4 узла по 8 ядер, 32 ГБ RAM, 512 ГБ диска.",
            Policy = SchedulingPolicyKind.Fifo,
            Nodes = Enumerable.Range(0, 4)
                .Select(index => new ClusterNodeDefinition($"node-{index:D2}", 8, 32 * 1024, 512))
                .ToList()
        };

        PersistCluster(definition);
        StatusMessage = $"Создан стандартный кластер \"{definition.Name}\".";
    }

    [RelayCommand]
    private void EditCluster(ClusterCardRow? row)
    {
        OpenClusterEditor(row?.Definition, standard: false);
    }

    [RelayCommand]
    private void DeleteCluster(ClusterCardRow? row)
    {
        if (row is null) return;

        _clusterService.Delete(row.Id);
        RefreshClustersFromStore();
        StatusMessage = $"Кластер \"{row.Name}\" удален.";
    }

    private void OpenClusterEditor(ClusterDefinition? existing, bool standard)
    {
        var viewModel = new ClusterEditorViewModel();
        viewModel.Load(existing, standard);

        var window = new ClusterEditWindow(viewModel) { Owner = Application.Current.MainWindow };

        if (window.ShowDialog() == true && viewModel.Result is not null)
        {
            PersistCluster(viewModel.Result);
            StatusMessage = $"Кластер \"{viewModel.Result.Name}\" сохранен.";
        }
    }

    private void PersistCluster(ClusterDefinition definition)
    {
        _clusterService.Save(definition);
        RefreshClustersFromStore();
    }

    private void RefreshClustersFromStore()
    {
        AvailableClusters.Clear();
        foreach (var definition in _clusterService.ListSaved())
        {
            AvailableClusters.Add(definition);
        }

        RebuildClusterCards();
    }

    private void RebuildClusterCards()
    {
        ClusterCards.Clear();

        foreach (var definition in AvailableClusters)
        {
            ClusterCards.Add(new ClusterCardRow(
                definition.Id,
                definition.Name,
                definition.Description,
                definition.Nodes.Count,
                definition.Nodes.Sum(node => node.CpuCores),
                definition.Nodes.Sum(node => node.RamMb) / 1024,
                definition.Nodes.Sum(node => node.DiskGb),
                definition.Policy.ToString(),
                definition));
        }

        // Убираем из выбора удаленные кластеры.
        foreach (var selected in SelectedClusters.ToList())
        {
            if (AvailableClusters.All(item => item.Id != selected.Id))
            {
                SelectedClusters.Remove(selected);
            }
        }
    }

    // ---------------- Задания: карточки и редактор ----------------

    [RelayCommand]
    private void CreateWorkload()
    {
        OpenWorkloadEditor(null, standard: false);
    }

    [RelayCommand]
    private void CreateStandardWorkload()
    {
        var definition = new WorkloadDefinition
        {
            Name = $"Стандартная нагрузка {AvailableWorkloads.Count + 1}",
            Description = "100 заданий, пуассоновский поток 60/час, длительности 60–3600 сек.",
            Options = new WorkloadOptions
            {
                JobCount = 100,
                Seed = 42,
                ArrivalRatePerHour = 60,
                MinDurationSeconds = 60,
                MaxDurationSeconds = 3600,
                MinCpuCores = 1,
                MaxCpuCores = 4,
                MinRamMb = 512,
                MaxRamMb = 4096
            }
        };

        AvailableWorkloads.Add(definition);
        SaveWorkloads();
        RebuildWorkloadCards();
        StatusMessage = $"Создан стандартный профиль \"{definition.Name}\".";
    }

    [RelayCommand]
    private void EditWorkload(WorkloadCardRow? row)
    {
        OpenWorkloadEditor(row?.Definition, standard: false);
    }

    [RelayCommand]
    private void DeleteWorkload(WorkloadCardRow? row)
    {
        if (row is null) return;

        var definition = AvailableWorkloads.FirstOrDefault(item => item.Id == row.Id);
        if (definition is not null)
        {
            AvailableWorkloads.Remove(definition);
        }

        SaveWorkloads();
        RebuildWorkloadCards();
        StatusMessage = $"Профиль \"{row.Name}\" удален.";
    }

    private void OpenWorkloadEditor(WorkloadDefinition? existing, bool standard)
    {
        var viewModel = new WorkloadEditorViewModel();
        viewModel.Load(existing, standard);

        var window = new WorkloadEditWindow(viewModel) { Owner = Application.Current.MainWindow };

        if (window.ShowDialog() == true && viewModel.Result is not null)
        {
            var existingById = AvailableWorkloads.FirstOrDefault(item => item.Id == viewModel.Result.Id);
            if (existingById is not null)
            {
                AvailableWorkloads[AvailableWorkloads.IndexOf(existingById)] = viewModel.Result;
            }
            else
            {
                AvailableWorkloads.Add(viewModel.Result);
            }

            SaveWorkloads();
            RebuildWorkloadCards();
            StatusMessage = $"Профиль \"{viewModel.Result.Name}\" сохранен.";
        }
    }

    private void RebuildWorkloadCards()
    {
        WorkloadCards.Clear();

        foreach (var definition in AvailableWorkloads)
        {
            var options = definition.Options;

            WorkloadCards.Add(new WorkloadCardRow(
                definition.Id,
                definition.Name,
                definition.Description,
                $"Заданий: {options.JobCount} · Seed: {options.Seed}",
                $"Длительность: {options.MinDurationSeconds:F0}–{options.MaxDurationSeconds:F0} сек",
                $"CPU: {options.MinCpuCores}–{options.MaxCpuCores} ядер",
                $"RAM: {options.MinRamMb}–{options.MaxRamMb} МБ",
                definition));
        }

        foreach (var selected in SelectedWorkloads.ToList())
        {
            if (AvailableWorkloads.All(item => item.Id != selected.Id))
            {
                SelectedWorkloads.Remove(selected);
            }
        }
    }

    private void LoadWorkloads()
    {
        var path = WorkloadsFilePath;

        if (File.Exists(path))
        {
            try
            {
                var definitions = JsonSerializer.Deserialize<List<WorkloadDefinition>>(File.ReadAllText(path));

                if (definitions is not null)
                {
                    foreach (var definition in definitions)
                    {
                        AvailableWorkloads.Add(definition);
                    }
                }
            }
            catch (JsonException)
            {
                // Поврежденный файл игнорируем.
            }
        }

        RebuildWorkloadCards();
    }

    private void SaveWorkloads()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WorkloadsFilePath)!);
            File.WriteAllText(
                WorkloadsFilePath,
                JsonSerializer.Serialize(AvailableWorkloads.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // Ошибка диска не должна ронять приложение.
        }
    }

    private static string WorkloadsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClusterSim", "workloads.json");

    // ---------------- Запуск симуляции ----------------

    [RelayCommand]
    private async Task RunSimulationAsync()
    {
        if (IsRunning) return;

        if (SelectedClusters.Count == 0)
        {
            StatusMessage = "Выберите хотя бы один кластер.";
            return;
        }

        if (SelectedWorkloads.Count == 0)
        {
            StatusMessage = "Выберите хотя бы один профиль заданий.";
            return;
        }

        IsRunning = true;
        StatusMessage = "Симуляция выполняется...";

        try
        {
            var outcomes = new List<RunOutcome>();

            if (SelectedRunMode == RunMode.ClusterComparison)
            {
                foreach (var cluster in SelectedClusters)
                {
                    foreach (var workload in SelectedWorkloads)
                    {
                        var result = await _runner.RunSingleAsync(BuildOptions(
                            new ClusterOptions { CustomNodes = cluster.Nodes }, cluster, workload.Options));
                        outcomes.Add(new RunOutcome($"{cluster.Name} · {workload.Name}", result));
                    }
                }
            }
            else
            {
                var (mergedNodes, ranges, mergedLabel) = BuildMergedCluster();

                foreach (var workload in SelectedWorkloads)
                {
                    var result = await _runner.RunSingleAsync(BuildOptions(
                        new ClusterOptions { CustomNodes = mergedNodes }, null, workload.Options));
                    outcomes.Add(new RunOutcome($"{mergedLabel} · {workload.Name}", result, ranges));
                }
            }

            _lastResults = outcomes.Select(outcome => outcome.Result).ToList();

            RunResults.Clear();
            foreach (var outcome in outcomes)
            {
                RunResults.Add(outcome.Result);
            }

            FillUtilization(outcomes);

            if (outcomes.Count > 0)
            {
                FillDetails(outcomes[0].Result);
            }

            if (CompareAllPolicies)
            {
                await FillPolicyComparisonAsync();
            }
            else
            {
                Comparison.Clear();
            }

            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(HasComparison));
            StatusMessage = $"Симуляция завершена: прогонов — {outcomes.Count}.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка симуляции: {exception.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Склеивает выбранные кластеры в один общий для режима совместной работы.</summary>
    private (List<ClusterNodeDefinition> Nodes, List<(string Cluster, int NodeCount)> Ranges, string Label)
        BuildMergedCluster()
    {
        var nodes = new List<ClusterNodeDefinition>();
        var ranges = new List<(string, int)>();
        var names = new List<string>();

        foreach (var cluster in SelectedClusters)
        {
            nodes.AddRange(cluster.Nodes);
            ranges.Add((cluster.Name, cluster.Nodes.Count));
            names.Add(cluster.Name);
        }

        return (nodes, ranges, string.Join(" + ", names));
    }

    private SimulationOptions BuildOptions(
        ClusterOptions clusterOptions,
        ClusterDefinition? definition,
        WorkloadOptions workload,
        SchedulingPolicyKind? policyOverride = null)
    {
        var policy = ResolvePolicy(definition, policyOverride);

        return new SimulationOptions
        {
            Cluster = clusterOptions,
            Workload = workload,
            Policy = policy,
            TimeSeriesStep = TimeSeriesStep
        };
    }

    private PolicyOptions ResolvePolicy(ClusterDefinition? definition, SchedulingPolicyKind? policyOverride)
    {
        if (policyOverride is not null)
        {
            return new PolicyOptions
            {
                Kind = policyOverride.Value,
                StrictFifo = StrictFifo,
                AllowPreemption = AllowPreemption
            };
        }

        if (UseClusterPolicy && definition is not null)
        {
            return new PolicyOptions
            {
                Kind = definition.Policy,
                StrictFifo = definition.StrictFifo,
                AllowPreemption = definition.AllowPreemption,
                CheckpointOverheadSeconds = definition.CheckpointOverheadSeconds,
                MinRunTimeBeforePreemption = definition.MinRunTimeBeforePreemption
            };
        }

        return new PolicyOptions
        {
            Kind = SelectedPolicy,
            StrictFifo = StrictFifo,
            AllowPreemption = AllowPreemption
        };
    }

    /// <summary>Заполняет таблицу загрузки по кластерам, включая разбор совместного прогона по узлам.</summary>
    private void FillUtilization(List<RunOutcome> outcomes)
    {
        ClusterUtilization.Clear();

        foreach (var outcome in outcomes)
        {
            if (outcome.JointRanges is null)
            {
                ClusterUtilization.Add(new ClusterUtilizationRow(
                    outcome.Label,
                    outcome.Result.Metrics.NodeUtilizations.Count,
                    outcome.Result.Metrics.CpuUtilization,
                    outcome.Result.Metrics.RamUtilization));
                continue;
            }

            var offset = 0;

            foreach (var (clusterName, nodeCount) in outcome.JointRanges)
            {
                var slice = outcome.Result.Metrics.NodeUtilizations
                    .Skip(offset)
                    .Take(nodeCount)
                    .ToList();

                offset += nodeCount;

                if (slice.Count == 0)
                {
                    continue;
                }

                var totalCpu = slice.Sum(item => item.TotalCpuCores);
                var totalRam = slice.Sum(item => item.TotalRamMb);

                var cpuUtil = totalCpu > 0
                    ? slice.Sum(item => item.CpuUtilization * item.TotalCpuCores) / totalCpu
                    : 0.0;
                var ramUtil = totalRam > 0
                    ? slice.Sum(item => item.RamUtilization * item.TotalRamMb) / totalRam
                    : 0.0;

                ClusterUtilization.Add(new ClusterUtilizationRow(
                    $"{outcome.Label} → {clusterName}", nodeCount, cpuUtil, ramUtil));
            }
        }
    }

    /// <summary>Сравнение всех политик: в режиме сравнения — по каждому кластеру, в совместном — на объединении.</summary>
    private async Task FillPolicyComparisonAsync()
    {
        var rows = new List<ComparisonRow>();
        var firstWorkload = SelectedWorkloads[0].Options;

        if (SelectedRunMode == RunMode.ClusterComparison)
        {
            foreach (var cluster in SelectedClusters)
            {
                foreach (var policyKind in AvailablePolicies)
                {
                    var result = await _runner.RunSingleAsync(BuildOptions(
                        new ClusterOptions { CustomNodes = cluster.Nodes }, cluster, firstWorkload, policyKind));
                    rows.Add(ToComparisonRow($"{cluster.Name} — {result.Metrics.PolicyName}", result.Metrics));
                }
            }
        }
        else
        {
            var (mergedNodes, _, _) = BuildMergedCluster();

            foreach (var policyKind in AvailablePolicies)
            {
                var result = await _runner.RunSingleAsync(BuildOptions(
                    new ClusterOptions { CustomNodes = mergedNodes }, null, firstWorkload, policyKind));
                rows.Add(ToComparisonRow($"Совместно — {result.Metrics.PolicyName}", result.Metrics));
            }
        }

        Comparison.Clear();
        foreach (var row in rows)
        {
            Comparison.Add(row);
        }
    }

    private static ComparisonRow ToComparisonRow(string label, SimulationMetrics metrics) => new(
        label,
        metrics.Makespan,
        metrics.AverageWaitTime,
        metrics.AverageTurnaround,
        metrics.AverageSlowdown,
        metrics.CpuUtilization,
        metrics.RamUtilization,
        metrics.ThroughputPerHour,
        metrics.CompletedJobs,
        metrics.PreemptionCount);

    [RelayCommand]
    private void ExportResults()
    {
        if (_lastResults is null || _lastResults.Count == 0)
        {
            StatusMessage = "Экспортировать нечего: сначала выполните симуляцию.";
            return;
        }

        try
        {
            var paths = new List<string>();

            foreach (var result in _lastResults)
            {
                paths.AddRange(_exporter.ExportAll(result.Metrics, result.Options, OutputDirectory));
            }

            if (_lastResults.Count > 1)
            {
                paths.Add(_exporter.ExportComparison(
                    _lastResults.Select(result => result.Metrics).ToList(), OutputDirectory));
            }

            StatusMessage = $"Экспортировано файлов: {paths.Count} в {OutputDirectory}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка экспорта: {exception.Message}";
        }
    }

    private void FillDetails(RunResult result)
    {
        var snapshot = result.FinalSnapshot;

        Nodes.Clear();
        foreach (var node in snapshot.Nodes) Nodes.Add(node);

        AllJobs.Clear();
        foreach (var job in snapshot.PendingJobs
                     .Concat(snapshot.RunningJobs)
                     .Concat(snapshot.FinishedJobs)
                     .OrderBy(job => job.Id))
        {
            AllJobs.Add(job);
        }

        TimeSeries.Clear();
        foreach (var point in result.Metrics.TimeSeries) TimeSeries.Add(point);

        var metrics = snapshot.Metrics;
        CurrentTime = metrics.CurrentTime;
        Makespan = metrics.Makespan;
        AverageWaitTime = metrics.AverageWaitTime;
        AverageTurnaround = metrics.AverageTurnaround;
        AverageSlowdown = metrics.AverageSlowdown;
        CpuUtilization = metrics.CpuUtilization;
        RamUtilization = metrics.RamUtilization;
        ThroughputPerHour = metrics.ThroughputPerHour;
        TotalJobs = metrics.TotalJobs;
        CompletedJobs = metrics.CompletedJobs;
        UnfinishedJobs = metrics.UnfinishedJobs;
        RejectedJobs = metrics.RejectedJobs;
        PreemptionCount = metrics.PreemptionCount;
        AverageQueueLength = metrics.AverageQueueLength;
        MaxQueueLength = metrics.MaxQueueLength;
    }
}