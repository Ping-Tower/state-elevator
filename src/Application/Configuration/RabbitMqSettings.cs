namespace Application.Configuration;

public class RabbitMqSettings
{
    public string? HostName { get; set; }
    public int Port { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string ServerEventsQueue { get; set; } = null!;
    public string PingEventsQueue { get; set; } = null!;
    public string StatusEventsExchange { get; set; } = null!;
    public string StatusChangedRoutingKey { get; set; } = null!;
}
