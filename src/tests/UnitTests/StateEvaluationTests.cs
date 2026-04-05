using Application;
using Application.DTOs;
using Xunit;

namespace UnitTests;

public class StateEvaluationTests
{
    [Fact]
    public void BuildSnapshot_WhenSettingsAreMissing_UsesDefaultIntervalAndFailureThreshold()
    {
        var snapshot = StateEvaluation.BuildSnapshot(new ServerEventMessage
        {
            Server = new ServerEventServerDto
            {
                Id = "server-1",
                IsActive = true,
                IsDeleted = false
            },
            PingSettings = new ServerEventPingSettingsDto
            {
                ServerId = "server-1",
                IntervalSec = null,
                LatencyThresholdMs = null,
                FailureThreshold = null,
                IsDeleted = false
            }
        });

        Assert.Equal("server-1", snapshot.ServerId);
        Assert.Equal(60, snapshot.IntervalSec);
        Assert.Equal(1, snapshot.FailureThreshold);
        Assert.Null(snapshot.LatencyThresholdMs);
    }

    [Fact]
    public void BuildNextState_WhenPingSucceeds_ResetsFailuresAndMarksUp()
    {
        var currentState = new ServerRuntimeState
        {
            Status = ServerStatus.DOWN,
            ConsecutiveFailures = 3
        };

        var nextState = StateEvaluation.BuildNextState(
            currentState,
            new ServerConfigurationSnapshot
            {
                ServerId = "server-1",
                IntervalSec = 60,
                FailureThreshold = 2
            },
            new PingRecordedMessage
            {
                ServerId = "server-1",
                IsSuccess = true,
                Timestamp = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero)
            });

        Assert.Equal(ServerStatus.UP, nextState.Status);
        Assert.Equal(0, nextState.ConsecutiveFailures);
        Assert.Equal(new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero), nextState.UpdatedAt);
    }

    [Fact]
    public void BuildNextState_WhenLatencyExceedsThreshold_IncrementsFailuresWithoutSeparateRule()
    {
        var nextState = StateEvaluation.BuildNextState(
            new ServerRuntimeState
            {
                Status = ServerStatus.UP,
                ConsecutiveFailures = 0
            },
            new ServerConfigurationSnapshot
            {
                ServerId = "server-2",
                IntervalSec = 60,
                LatencyThresholdMs = 400,
                FailureThreshold = 2
            },
            new PingRecordedMessage
            {
                ServerId = "server-2",
                IsSuccess = true,
                LatencyMs = 800,
                Timestamp = DateTimeOffset.UtcNow
            });

        Assert.Equal(ServerStatus.UP, nextState.Status);
        Assert.Equal(1, nextState.ConsecutiveFailures);
    }

    [Fact]
    public void BuildNextState_WhenFailuresReachThreshold_MarksDown()
    {
        var nextState = StateEvaluation.BuildNextState(
            new ServerRuntimeState
            {
                Status = ServerStatus.UP,
                ConsecutiveFailures = 1
            },
            new ServerConfigurationSnapshot
            {
                ServerId = "server-3",
                IntervalSec = 60,
                FailureThreshold = 2
            },
            new PingRecordedMessage
            {
                ServerId = "server-3",
                IsSuccess = false,
                Timestamp = DateTimeOffset.UtcNow
            });

        Assert.Equal(ServerStatus.DOWN, nextState.Status);
        Assert.Equal(2, nextState.ConsecutiveFailures);
    }
}
