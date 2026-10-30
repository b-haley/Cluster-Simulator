using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClusterSim.Core.Simulation.Workload;

namespace ClusterSim.Wpf.ViewModels;

/// <summary>ViewModel модального окна редактирования/создания профиля заданий.</summary>
public sealed partial class WorkloadEditorViewModel : ObservableObject
{
    private Guid? _existingId;

    public event Action<bool>? CloseRequested;

    public WorkloadDefinition? Result { get; private set; }

    [ObservableProperty] private string _workloadName = "Новый профиль заданий";
    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty] private int _jobCount = 100;
    [ObservableProperty] private int _seed = 42;
    [ObservableProperty] private double _arrivalRatePerHour = 60;

    [ObservableProperty] private int _minDurationSeconds = 60;
    [ObservableProperty] private int _maxDurationSeconds = 3600;
    [ObservableProperty] private int _minCpuCores = 1;
    [ObservableProperty] private int _maxCpuCores = 4;
    [ObservableProperty] private int _minRamMb = 512;
    [ObservableProperty] private int _maxRamMb = 4096;

    [ObservableProperty] private double _parallelJobProbability;
    [ObservableProperty] private double _estimationError;

    /// <summary>Заполняет редактор существующим профилем или стандартными значениями.</summary>
    public void Load(WorkloadDefinition? definition, bool standard)
    {
        if (definition is not null)
        {
            _existingId = definition.Id;
            WorkloadName = definition.Name;
            Description = definition.Description;
            JobCount = definition.Options.JobCount;
            Seed = definition.Options.Seed;
            ArrivalRatePerHour = definition.Options.ArrivalRatePerHour;
            MinDurationSeconds = (int)Math.Round(definition.Options.MinDurationSeconds);
            MaxDurationSeconds = (int)Math.Round(definition.Options.MaxDurationSeconds);
            MinCpuCores = definition.Options.MinCpuCores;
            MaxCpuCores = definition.Options.MaxCpuCores;
            MinRamMb = definition.Options.MinRamMb;
            MaxRamMb = definition.Options.MaxRamMb;
            ParallelJobProbability = definition.Options.ParallelJobProbability;
            EstimationError = definition.Options.EstimationError;
            return;
        }

        if (standard)
        {
            WorkloadName = "Стандартная нагрузка";
            Description = "100 заданий, пуассоновский поток 60/час, длительности 60–3600 сек.";
        }
        else
        {
            WorkloadName = "Новый профиль заданий";
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (MinDurationSeconds > MaxDurationSeconds)
        {
            MinDurationSeconds = MaxDurationSeconds;
        }

        if (MinCpuCores > MaxCpuCores)
        {
            MinCpuCores = MaxCpuCores;
        }

        if (MinRamMb > MaxRamMb)
        {
            MinRamMb = MaxRamMb;
        }

        Result = new WorkloadDefinition
        {
            Id = _existingId ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(WorkloadName) ? "Без названия" : WorkloadName,
            Description = Description,
            Options = new WorkloadOptions
            {
                JobCount = JobCount,
                Seed = Seed,
                ArrivalRatePerHour = ArrivalRatePerHour,
                MinDurationSeconds = MinDurationSeconds,
                MaxDurationSeconds = MaxDurationSeconds,
                MinCpuCores = MinCpuCores,
                MaxCpuCores = MaxCpuCores,
                MinRamMb = MinRamMb,
                MaxRamMb = MaxRamMb,
                ParallelJobProbability = ParallelJobProbability,
                EstimationError = EstimationError
            }
        };

        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}