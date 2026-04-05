using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Configuration;
using Application.DTOs;
using Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public class StatusEventPublisher : IStatusEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<StatusEventPublisher> _logger;
    private readonly RabbitMqSettings _settings;

    private IConnection? _connection;
    private IChannel? _channel;

    public StatusEventPublisher(ILogger<StatusEventPublisher> logger, IOptions<RabbitMqSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task PublishAsync(ServerStatusChangedMessage message, CancellationToken cancellationToken)
    {
        await EnsureChannelAsync(cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonSerializerOptions));
        var properties = new BasicProperties { Persistent = true };

        await _channel!.BasicPublishAsync(
            exchange: _settings.StatusEventsExchange,
            routingKey: _settings.StatusChangedRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published status change. ServerId: {ServerId}, Status: {Status}",
            message.ServerId,
            message.Status);
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsClosed: false })
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsClosed: false })
                return;

            await CleanupConnectionAsync();

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName!,
                Port = _settings.Port,
                UserName = _settings.UserName!,
                Password = _settings.Password!
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task CleanupConnectionAsync()
    {
        if (_channel is not null)
        {
            if (!_channel.IsClosed)
                await _channel.CloseAsync();
            _channel.Dispose();
            _channel = null;
        }

        if (_connection is not null)
        {
            if (_connection.IsOpen)
                await _connection.CloseAsync();
            _connection.Dispose();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupConnectionAsync();
        _lock.Dispose();
    }
}
