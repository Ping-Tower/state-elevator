namespace IntegrationTests;

public sealed class RabbitMqFixture() : DockerContainerFixture("rabbitmq:3.13-alpine", 5672)
{
    public string HostName => "127.0.0.1";
    public int Port => HostPort;
    public bool Available => IsAvailable;
}
