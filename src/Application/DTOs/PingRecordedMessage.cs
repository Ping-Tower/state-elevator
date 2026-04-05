namespace Application.DTOs;

public class PingRecordedMessage
{
    public string? Id { get; set; }
    public string? ServerId { get; set; }
    public string? Protocol { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool IsSuccess { get; set; }
    public double? LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }
    public DateTimeOffset? CertExpiresAt { get; set; }
    public string? TlsVersion { get; set; }
    public double? DnsLookupMs { get; set; }
    public long? SentBytes { get; set; }
    public long? ReceivedBytes { get; set; }
    public double? PacketLossPercent { get; set; }
    public double? RttMinMs { get; set; }
    public double? RttMaxMs { get; set; }
    public int? Ttl { get; set; }
}
