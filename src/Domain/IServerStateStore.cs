using Application.DTOs;

namespace Domain;

public interface IServerStateStore
{
    Task<ServerRuntimeState?> GetStateAsync(string serverId, CancellationToken cancellationToken);
    Task SaveStateAsync(string serverId, ServerRuntimeState state, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveStateAsync(string serverId, CancellationToken cancellationToken);
}
