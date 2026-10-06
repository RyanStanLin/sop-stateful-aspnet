using System.Text.Json;
using Npgsql;
using StackExchange.Redis;

public sealed class DemoData : IAsyncDisposable
{
    private readonly NpgsqlDataSource? _postgres;
    private ConnectionMultiplexer? _redis;
    private readonly ConfigurationOptions? _redisOptions;
    private readonly string? _configPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    public DemoData(IConfiguration configuration)
    {
        if (!string.IsNullOrEmpty(configuration["DATABASE_HOST"]))
        {
            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = configuration["DATABASE_HOST"],
                Port = int.Parse(configuration["DATABASE_PORT"] ?? "5432"),
                Database = configuration["DATABASE_NAME"] ?? "app",
                Username = configuration["DATABASE_USER"] ?? "app",
                Password = configuration["DATABASE_PASSWORD"] ?? throw new InvalidOperationException("DATABASE_PASSWORD required"),
                MaxPoolSize = 8,
                Timeout = 5,
                CommandTimeout = 5
            };
            _postgres = NpgsqlDataSource.Create(connection.ConnectionString);
        }
        if (!string.IsNullOrEmpty(configuration["REDIS_HOST"]))
        {
            _redisOptions = new ConfigurationOptions
            {
                Password = configuration["REDIS_PASSWORD"],
                AbortOnConnectFail = false,
                ConnectTimeout = 5000,
                AsyncTimeout = 5000
            };
            _redisOptions.EndPoints.Add(configuration["REDIS_HOST"]!, int.Parse(configuration["REDIS_PORT"] ?? "6379"));
        }
        _configPath = configuration["CONFIG_PATH"];
    }
    public async Task InitializeAsync()
    {
        if (_postgres is not null)
        {
            await using var command = _postgres.CreateCommand("CREATE TABLE IF NOT EXISTS paas_items (key text PRIMARY KEY, value text NOT NULL)");
            await command.ExecuteNonQueryAsync();
        }
        if (_redisOptions is not null) _redis = await ConnectionMultiplexer.ConnectAsync(_redisOptions);
    }
    public async Task<bool> HealthyAsync(CancellationToken cancellation)
    {
        try
        {
            if (_postgres is not null)
            {
                await using var command = _postgres.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellation);
            }
            if (_redis is not null) await _redis.GetDatabase().PingAsync();
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or RedisException or TimeoutException) { return false; }
    }
    public async Task PutDatabaseAsync(string key, string value, CancellationToken cancellation)
    {
        if (_postgres is null) throw new DataUnavailableException();
        await using var command = _postgres.CreateCommand("INSERT INTO paas_items(key,value) VALUES($1,$2) ON CONFLICT(key) DO UPDATE SET value=EXCLUDED.value");
        command.Parameters.AddWithValue(key); command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancellation);
    }
    public async Task<string?> GetDatabaseAsync(string key, CancellationToken cancellation)
    {
        if (_postgres is null) throw new DataUnavailableException();
        await using var command = _postgres.CreateCommand("SELECT value FROM paas_items WHERE key=$1");
        command.Parameters.AddWithValue(key);
        return (string?)await command.ExecuteScalarAsync(cancellation);
    }
    public async Task PutRedisAsync(string key, string value)
    {
        if (_redis is null) throw new DataUnavailableException();
        await _redis.GetDatabase().StringSetAsync("demo:" + key, value);
    }
    public async Task<string?> GetRedisAsync(string key)
    {
        if (_redis is null) throw new DataUnavailableException();
        var value = await _redis.GetDatabase().StringGetAsync("demo:" + key);
        return value.IsNull ? null : value.ToString();
    }
    public async Task WriteConfigAsync(JsonElement value, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(_configPath)) throw new DataUnavailableException();
        await _fileLock.WaitAsync(cancellation);
        var temp = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_configPath))!);
            await File.WriteAllTextAsync(temp, value.GetRawText(), cancellation);
            File.Move(temp, _configPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); _fileLock.Release(); }
    }
    public async Task<JsonElement?> ReadConfigAsync(CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(_configPath)) throw new DataUnavailableException();
        await _fileLock.WaitAsync(cancellation);
        try
        {
            if (!File.Exists(_configPath)) return null;
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_configPath, cancellation));
            return document.RootElement.Clone();
        }
        finally { _fileLock.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_redis is not null) await _redis.DisposeAsync();
        _fileLock.Dispose();
    }
}
public sealed class DataUnavailableException : Exception;
