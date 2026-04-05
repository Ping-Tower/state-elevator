using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application;
using Application.Configuration;
using Application.DTOs;
using Domain;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace StateElevatorWorker;

public class StateElevatorRabbitMqWorker(
    ILogger<StateElevatorRabbitMqWorker> logger,
    IServerConfigurationStore configurationStore,
    IServerStateStore stateStore,
    IStatusEventPublisher statusEventPublisher,
    IOptions<RabbitMqSettings> rabbitMqSettings) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly RabbitMqSettings _rabbitMqSettings = rabbitMqSettings.Value;

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_channel is null || _channel.IsClosed)
                    await ConnectToRabbitMq(stoppingToken);

                await StartServerEventConsumerAsync(stoppingToken);
                await StartPingEventConsumerAsync(stoppingToken);

                while (!_channel!.IsClosed && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.Initiator == ShutdownInitiator.Peer)
            {
                logger.LogWarning("RabbitMQ connection lost. Reconnecting in 5 seconds. Reason: {Reason}", ex.ShutdownReason?.ToString());
                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in state-elevator worker. Reconnecting in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
            finally
            {
                await CloseRabbitMqAsync();
            }
        }
    }

    private async Task ConnectToRabbitMq(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqSettings.HostName!,
            Port = _rabbitMqSettings.Port,
            UserName = _rabbitMqSettings.UserName!,
            Password = _rabbitMqSettings.Password!
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Connected to RabbitMQ. ServerEventsQueue: {ServerEventsQueue}, PingEventsQueue: {PingEventsQueue}",
            _rabbitMqSettings.ServerEventsQueue,
            _rabbitMqSettings.PingEventsQueue);
    }

    private async Task StartServerEventConsumerAsync(CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += async (_, ea) => await ProcessServerEventAsync(ea, cancellationToken);

        await _channel!.BasicConsumeAsync(
            queue: _rabbitMqSettings.ServerEventsQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    private async Task StartPingEventConsumerAsync(CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += async (_, ea) => await ProcessPingEventAsync(ea, cancellationToken);

        await _channel!.BasicConsumeAsync(
            queue: _rabbitMqSettings.PingEventsQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    private async Task ProcessServerEventAsync(BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var message = DeserializeMessage<ServerEventMessage>(delivery.Body);
            if (message?.Server?.Id is null || message.PingSettings?.ServerId is null)
            {
                logger.LogWarning("Invalid server event payload. RoutingKey: {RoutingKey}", delivery.RoutingKey);
                await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            var snapshot = StateEvaluation.BuildSnapshot(message);

            switch (delivery.RoutingKey)
            {
                case "server.target.added":
                case "server.target.updated":
                    await configurationStore.SaveConfigurationAsync(snapshot, cancellationToken);

                    logger.LogInformation(
                        "Stored server configuration. RoutingKey: {RoutingKey}, ServerId: {ServerId}, IntervalSec: {IntervalSec}, LatencyThresholdMs: {LatencyThresholdMs}, FailureThreshold: {FailureThreshold}, Active: {IsActive}, Deleted: {IsDeleted}",
                        delivery.RoutingKey,
                        snapshot.ServerId,
                        snapshot.IntervalSec,
                        snapshot.LatencyThresholdMs,
                        snapshot.FailureThreshold,
                        snapshot.IsActive,
                        snapshot.IsDeleted);
                    break;
                case "server.target.deleted":
                    await configurationStore.DeleteConfigurationAsync(snapshot.ServerId, cancellationToken);
                    await stateStore.RemoveStateAsync(snapshot.ServerId, cancellationToken);

                    logger.LogInformation(
                        "Deleted server configuration and state. RoutingKey: {RoutingKey}, ServerId: {ServerId}",
                        delivery.RoutingKey,
                        snapshot.ServerId);
                    break;
                default:
                    logger.LogInformation(
                        "Ignoring unsupported routing key {RoutingKey} for server_id={ServerId}",
                        delivery.RoutingKey,
                        snapshot.ServerId);
                    break;
            }

            await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid server event JSON. Message rejected.");
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process server event. Message will be requeued.");
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task ProcessPingEventAsync(BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var ping = DeserializeMessage<PingRecordedMessage>(delivery.Body);
            if (ping?.ServerId is null)
            {
                logger.LogWarning("Invalid ping event payload. RoutingKey: {RoutingKey}", delivery.RoutingKey);
                await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            var configuration = await configurationStore.GetConfigurationAsync(ping.ServerId, cancellationToken);
            if (configuration is null)
            {
                logger.LogWarning("No configuration found for ping event. ServerId: {ServerId}", ping.ServerId);
                await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                return;
            }

            if (configuration.IsDeleted || configuration.PingSettingsDeleted || !configuration.IsActive)
            {
                await stateStore.RemoveStateAsync(ping.ServerId, cancellationToken);
                await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                return;
            }

            var currentState = await stateStore.GetStateAsync(ping.ServerId, cancellationToken)
                ?? new ServerRuntimeState
                {
                    Status = ServerStatus.UNKNOWN,
                    ConsecutiveFailures = 0,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

            var nextState = StateEvaluation.BuildNextState(currentState, configuration, ping);
            await stateStore.SaveStateAsync(
                ping.ServerId,
                nextState,
                TimeSpan.FromSeconds(configuration.IntervalSec * 3),
                cancellationToken);

            if (nextState.Status != currentState.Status)
            {
                await statusEventPublisher.PublishAsync(
                    new ServerStatusChangedMessage
                    {
                        ServerId = ping.ServerId,
                        Status = nextState.Status
                    },
                    cancellationToken);
            }

            logger.LogInformation(
                "Ping event processed. ServerId: {ServerId}, Success: {IsSuccess}, PreviousStatus: {PreviousStatus}, NewStatus: {NewStatus}, ConsecutiveFailures: {ConsecutiveFailures}",
                ping.ServerId,
                ping.IsSuccess,
                currentState.Status,
                nextState.Status,
                nextState.ConsecutiveFailures);

            await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid ping event JSON. Message rejected.");
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process ping event. Message will be requeued.");
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static T? DeserializeMessage<T>(ReadOnlyMemory<byte> body)
    {
        var payload = Encoding.UTF8.GetString(body.Span);
        return JsonSerializer.Deserialize<T>(payload, JsonSerializerOptions);
    }

    private async Task CloseRabbitMqAsync()
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
}
