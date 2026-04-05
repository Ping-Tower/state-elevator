namespace IntegrationTests;

public sealed class RedisFixture() : DockerContainerFixture("redis:7-alpine", 6379)
{
    public string ConnectionString => $"127.0.0.1:{HostPort}";
    public bool Available => IsAvailable;
}
