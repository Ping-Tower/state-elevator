using Application.DTOs;

namespace Domain;

public interface IServerConfigurationStore
{
    Task<ServerConfigurationSnapshot?> GetConfigurationAsync(string serverId, CancellationToken cancellationToken);
    Task SaveConfigurationAsync(ServerConfigurationSnapshot configuration, CancellationToken cancellationToken);
    Task DeleteConfigurationAsync(string serverId, CancellationToken cancellationToken);
}
