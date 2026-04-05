using Application.Configuration;
using Application.DTOs;
using Infrastructure.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace IntegrationTests;

[CollectionDefinition("redis")]
public class RedisCollection : ICollectionFixture<RedisFixture>
{
}

[Collection("redis")]
public class RedisStateRepositoryIntegrationTests
{
    private readonly RedisFixture _fixture;

    public RedisStateRepositoryIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveAndGetConfigurationAsync_RoundTripsThroughRedis()
    {
        if (!_fixture.Available)
            return;

        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
        var repository = new RedisStateRepository(
            multiplexer,
            Options.Create(new RedisSettings { Configuration = _fixture.ConnectionString, KeyPrefix = $"test-{Guid.NewGuid():N}" }));

        var configuration = new ServerConfigurationSnapshot
        {
            ServerId = "server-1",
            IntervalSec = 60,
            LatencyThresholdMs = 400,
            FailureThreshold = 2,
            IsActive = true
        };

        await repository.SaveConfigurationAsync(configuration, CancellationToken.None);
        var stored = await repository.GetConfigurationAsync("server-1", CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(400, stored!.LatencyThresholdMs);
        Assert.Equal(2, stored.FailureThreshold);
    }

    [Fact]
    public async Task SaveStateAsync_ExpiresAfterTtl()
    {
        if (!_fixture.Available)
            return;

        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
        var repository = new RedisStateRepository(
            multiplexer,
            Options.Create(new RedisSettings { Configuration = _fixture.ConnectionString, KeyPrefix = $"test-{Guid.NewGuid():N}" }));

        await repository.SaveStateAsync(
            "server-2",
            new ServerRuntimeState
            {
                Status = ServerStatus.UP,
                ConsecutiveFailures = 0,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None);

        Assert.NotNull(await repository.GetStateAsync("server-2", CancellationToken.None));

        await Task.Delay(750);

        Assert.Null(await repository.GetStateAsync("server-2", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteConfigurationAsync_RemovesStoredConfiguration()
    {
        if (!_fixture.Available)
            return;

        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
        var repository = new RedisStateRepository(
            multiplexer,
            Options.Create(new RedisSettings { Configuration = _fixture.ConnectionString, KeyPrefix = $"test-{Guid.NewGuid():N}" }));

        await repository.SaveConfigurationAsync(
            new ServerConfigurationSnapshot
            {
                ServerId = "server-3",
                IntervalSec = 60,
                FailureThreshold = 1
            },
            CancellationToken.None);

        await repository.DeleteConfigurationAsync("server-3", CancellationToken.None);

        Assert.Null(await repository.GetConfigurationAsync("server-3", CancellationToken.None));
    }
}
