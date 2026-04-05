using System.Text.Json;
using Application.Configuration;
using Application.DTOs;
using Domain;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Redis;

public class RedisStateRepository : IServerConfigurationStore, IServerStateStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new();

    private readonly IDatabase _database;
    private readonly string _keyPrefix;

    public RedisStateRepository(IConnectionMultiplexer connectionMultiplexer, IOptions<RedisSettings> settings)
    {
        _database = connectionMultiplexer.GetDatabase();
        _keyPrefix = settings.Value.KeyPrefix;
    }

    public async Task<ServerConfigurationSnapshot?> GetConfigurationAsync(string serverId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = await _database.StringGetAsync(ConfigurationKey(serverId));
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<ServerConfigurationSnapshot>(value!, JsonSerializerOptions);
    }

    public async Task SaveConfigurationAsync(ServerConfigurationSnapshot configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(configuration, JsonSerializerOptions);
        await _database.StringSetAsync(ConfigurationKey(configuration.ServerId), payload);
    }

    public async Task<ServerRuntimeState?> GetStateAsync(string serverId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = await _database.StringGetAsync(StateKey(serverId));
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<ServerRuntimeState>(value!, JsonSerializerOptions);
    }

    public async Task SaveStateAsync(string serverId, ServerRuntimeState state, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(state, JsonSerializerOptions);
        await _database.StringSetAsync(StateKey(serverId), payload, ttl);
    }

    public async Task RemoveStateAsync(string serverId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyDeleteAsync(StateKey(serverId));
    }

    public async Task DeleteConfigurationAsync(string serverId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyDeleteAsync(ConfigurationKey(serverId));
    }

    private string ConfigurationKey(string serverId) => $"{_keyPrefix}:server-config:{serverId}";

    private string StateKey(string serverId) => $"{_keyPrefix}:server-state:{serverId}";
}
