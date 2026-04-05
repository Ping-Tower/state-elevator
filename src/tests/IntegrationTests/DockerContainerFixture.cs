using System.Diagnostics;
using System.Net.Sockets;
using Xunit;

namespace IntegrationTests;

public abstract class DockerContainerFixture : IAsyncLifetime
{
    private readonly string _image;
    private readonly int _containerPort;
    private string? _containerId;
    private bool _isAvailable = true;

    protected DockerContainerFixture(string image, int containerPort)
    {
        _image = image;
        _containerPort = containerPort;
    }

    protected int HostPort { get; private set; }
    protected bool IsAvailable => _isAvailable;

    public async Task InitializeAsync()
    {
        HostPort = GetFreePort();
        try
        {
            _containerId = await RunDockerCommandAsync(
                $"run -d -p 127.0.0.1:{HostPort}:{_containerPort} {_image}");

            await WaitUntilReadyAsync();
        }
        catch (DockerUnavailableException)
        {
            _isAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_containerId))
            await RunDockerCommandAsync($"rm -f {_containerId}");
    }

    protected virtual async Task WaitUntilReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPortOpenAsync())
                return;

            await Task.Delay(500);
        }

        throw new TimeoutException($"Container {_image} did not become ready on port {HostPort}.");
    }

    private async Task<bool> IsPortOpenAsync()
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await client.ConnectAsync("127.0.0.1", HostPort, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected static async Task<string> RunDockerCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo("docker", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker process.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            if (stderr.Contains("failed to connect to the docker API", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("Cannot connect to the Docker daemon", StringComparison.OrdinalIgnoreCase))
            {
                throw new DockerUnavailableException();
            }

            throw new InvalidOperationException($"docker {arguments} failed with code {process.ExitCode}: {stderr}");
        }

        return stdout.Trim();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class DockerUnavailableException : Exception
    {
    }
}
