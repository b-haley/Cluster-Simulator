using System.Text.Json;

namespace ClusterSim.Core.Features.Cluster;

/// <summary>
/// Файловая реализация хранилища конфигураций кластера:
/// каждое определение лежит в отдельном JSON-файле.
/// </summary>
public sealed class FileSystemClusterDefinitionStore : IClusterDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _directory;

    public FileSystemClusterDefinitionStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Каталог хранилища не может быть пустым.", nameof(directory));
        }

        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Save(ClusterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        File.WriteAllText(GetPath(definition.Id), JsonSerializer.Serialize(definition, JsonOptions));
    }

    public ClusterDefinition? Load(Guid id)
    {
        var path = GetPath(id);

        return File.Exists(path)
            ? JsonSerializer.Deserialize<ClusterDefinition>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public IReadOnlyList<ClusterDefinition> ListAll() => Directory
        .GetFiles(_directory, "*.json")
        .Select(file => JsonSerializer.Deserialize<ClusterDefinition>(File.ReadAllText(file), JsonOptions))
        .Where(definition => definition is not null)
        .Select(definition => definition!)
        .OrderBy(definition => definition.CreatedUtc)
        .ToList();

    public bool Delete(Guid id)
    {
        var path = GetPath(id);

        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public bool Exists(Guid id) => File.Exists(GetPath(id));

    private string GetPath(Guid id) => Path.Combine(_directory, $"{id:N}.json");
}