using System.Text;
using System.Text.Json;
using Application.Configuration;
using Application.DTOs;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace IntegrationTests;

[CollectionDefinition("rabbitmq")]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
}

[Collection("rabbitmq")]
public class StatusEventPublisherIntegrationTests
{
    private readonly RabbitMqFixture _fixture;

    public StatusEventPublisherIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsync_PublishesMessageToConfiguredExchangeAndRoutingKey()
    {
        if (!_fixture.Available)
            return;

        var exchangeName = $"status-events-{Guid.NewGuid():N}";
        const string routingKey = "server.status.changed";
        var queueName = $"test-queue-{Guid.NewGuid():N}";

        var factory = new ConnectionFactory
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = "guest",
            Password = "guest"
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: false, autoDelete: true);
            await channel.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: true);
            await channel.QueueBindAsync(queueName, exchangeName, routingKey);

            await using var publisher = new StatusEventPublisher(
                NullLogger<StatusEventPublisher>.Instance,
                Options.Create(new RabbitMqSettings
                {
                    HostName = _fixture.HostName,
                    Port = _fixture.Port,
                    UserName = "guest",
                    Password = "guest",
                    StatusEventsExchange = exchangeName,
                    StatusChangedRoutingKey = routingKey
                }));

            await publisher.PublishAsync(
                new ServerStatusChangedMessage
                {
                    ServerId = "server-1",
                    Status = ServerStatus.DOWN
                },
                CancellationToken.None);

            var delivery = await channel.BasicGetAsync(queueName, autoAck: true);

            Assert.NotNull(delivery);
            Assert.Equal(routingKey, delivery!.RoutingKey);

            var payload = JsonSerializer.Deserialize<ServerStatusChangedMessage>(Encoding.UTF8.GetString(delivery.Body.ToArray()));
            Assert.NotNull(payload);
            Assert.Equal("server-1", payload!.ServerId);
            Assert.Equal(ServerStatus.DOWN, payload.Status);
        }
        finally
        {
            if (!channel.IsClosed)
                await channel.CloseAsync();
            channel.Dispose();

            if (connection.IsOpen)
                await connection.CloseAsync();
            connection.Dispose();
        }
    }
}
