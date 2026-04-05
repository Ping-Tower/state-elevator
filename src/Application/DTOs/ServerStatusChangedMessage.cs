using System.Text.Json.Serialization;

namespace Application.DTOs;

public class ServerStatusChangedMessage
{
    [JsonPropertyName("server_id")]
    public string ServerId { get; init; } = null!;

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServerStatus Status { get; init; }
}
