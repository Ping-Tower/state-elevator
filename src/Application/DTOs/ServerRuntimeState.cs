namespace Application.DTOs;

public class ServerRuntimeState
{
    public ServerStatus Status { get; set; } = ServerStatus.UNKNOWN;
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
