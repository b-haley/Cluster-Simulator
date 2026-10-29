namespace ClusterSim.Infrastructure.Configuration;

/// <summary>
/// Параметры подключения к PostgreSQL из Docker-контейнера.
/// Либо задаются отдельными полями, либо одной готовой строкой подключения.
/// </summary>
public sealed record PostgresOptions
{
    /// <summary>Хост базы данных.</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>Порт на хосте (см. POSTGRES_PORT в .env).</summary>
    public int Port { get; init; } = 5433;

    /// <summary>Имя базы данных.</summary>
    public string Database { get; init; } = "clustersim";

    /// <summary>Пользователь.</summary>
    public string Username { get; init; } = "clustersim";

    /// <summary>Пароль.</summary>
    public string Password { get; init; } = "change_me";

    /// <summary>Готовая строка подключения; если задана, имеет приоритет над полями.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Собирает строку подключения Npgsql.</summary>
    public string GetConnectionString() =>
        string.IsNullOrWhiteSpace(ConnectionString)
            ? $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}"
            : ConnectionString!;

    /// <summary>Создает опции из готовой строки подключения.</summary>
    public static PostgresOptions FromConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Строка подключения не может быть пустой.", nameof(connectionString));
        }

        return new PostgresOptions { ConnectionString = connectionString };
    }
}