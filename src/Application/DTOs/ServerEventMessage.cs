namespace Application.DTOs;

public class ServerEventMessage
{
    public ServerEventServerDto? Server { get; set; }
    public ServerEventPingSettingsDto? PingSettings { get; set; }
}

public class ServerEventServerDto
{
    public string? Id { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public class ServerEventPingSettingsDto
{
    public string? Id { get; set; }
    public string? ServerId { get; set; }
    public int? IntervalSec { get; set; }
    public int? LatencyThresholdMs { get; set; }
    public int? FailureThreshold { get; set; }
    public bool IsDeleted { get; set; }
}
