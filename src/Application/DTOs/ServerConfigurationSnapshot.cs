namespace Application.DTOs;

public class ServerConfigurationSnapshot
{
    public string ServerId { get; set; } = null!;
    public int IntervalSec { get; set; }
    public int? LatencyThresholdMs { get; set; }
    public int FailureThreshold { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public bool PingSettingsDeleted { get; set; }
}
