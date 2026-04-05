using Application.DTOs;

namespace Application;

public static class StateEvaluation
{
    public static ServerRuntimeState BuildNextState(
        ServerRuntimeState currentState,
        ServerConfigurationSnapshot configuration,
        PingRecordedMessage ping)
    {
        var now = ping.Timestamp == default ? DateTimeOffset.UtcNow : ping.Timestamp;
        var isFailure = !ping.IsSuccess;

        if (!isFailure &&
            configuration.LatencyThresholdMs.HasValue &&
            ping.LatencyMs is double latencyMs &&
            latencyMs > configuration.LatencyThresholdMs.Value)
        {
            isFailure = true;
        }

        if (!isFailure)
        {
            return new ServerRuntimeState
            {
                Status = ServerStatus.UP,
                ConsecutiveFailures = 0,
                UpdatedAt = now
            };
        }

        var failureCount = currentState.ConsecutiveFailures + 1;
        var nextStatus = failureCount >= configuration.FailureThreshold
            ? ServerStatus.DOWN
            : currentState.Status;

        return new ServerRuntimeState
        {
            Status = nextStatus,
            ConsecutiveFailures = failureCount,
            UpdatedAt = now
        };
    }

    public static ServerConfigurationSnapshot BuildSnapshot(ServerEventMessage message)
    {
        var pingSettings = message.PingSettings!;

        return new ServerConfigurationSnapshot
        {
            ServerId = message.Server!.Id!,
            IntervalSec = NormalizeInterval(pingSettings.IntervalSec),
            LatencyThresholdMs = pingSettings.LatencyThresholdMs,
            FailureThreshold = NormalizeFailureThreshold(pingSettings.FailureThreshold),
            IsActive = message.Server.IsActive,
            IsDeleted = message.Server.IsDeleted,
            PingSettingsDeleted = pingSettings.IsDeleted
        };
    }

    private static int NormalizeInterval(int? intervalSec) => intervalSec is > 0 ? intervalSec.Value : 60;

    private static int NormalizeFailureThreshold(int? failureThreshold) => failureThreshold is > 0 ? failureThreshold.Value : 1;
}
