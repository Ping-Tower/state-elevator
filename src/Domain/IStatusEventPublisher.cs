using Application.DTOs;

namespace Domain;

public interface IStatusEventPublisher
{
    Task PublishAsync(ServerStatusChangedMessage message, CancellationToken cancellationToken);
}
