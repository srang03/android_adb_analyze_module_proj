using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Sessions;

/// <summary>
/// MediaCameraSessionSource의 CameraDeviceIds 추출 테스트
/// </summary>
public sealed class MediaCameraDeviceIdTests
{
    private readonly ITestOutputHelper _output;
    private readonly MediaCameraSessionSource _source;
    private readonly AnalysisOptions _defaultOptions;

    public MediaCameraDeviceIdTests(ITestOutputHelper output)
    {
        _output = output;
        var logger = NullLogger<MediaCameraSessionSource>.Instance;
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        _source = new MediaCameraSessionSource(logger, confidenceCalculator);
        
        _defaultOptions = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3,
            EventCorrelationWindow = TimeSpan.FromSeconds(30)
        };
    }

    [Fact]
    public void ExtractSessions_WithDeviceId_ShouldExtractCameraDeviceIds()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEventWithDeviceId(LogEventTypes.CAMERA_CONNECT, baseTime, "com.sec.android.app.camera", deviceId: 20),
            CreateEventWithDeviceId(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.sec.android.app.camera", deviceId: 20)
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        
        _output.WriteLine($"Session: {session.SessionId}");
        _output.WriteLine($"CameraDeviceIds: {(session.CameraDeviceIds != null ? string.Join(", ", session.CameraDeviceIds) : "null")}");
        _output.WriteLine($"SourceEventIds Count: {session.SourceEventIds.Count}");
        
        session.CameraDeviceIds.Should().NotBeNull("deviceId가 추출되어야 함");
        session.CameraDeviceIds.Should().ContainSingle("단일 device 사용");
        session.CameraDeviceIds![0].Should().Be(20, "device 20 (후면 카메라)");
    }

    [Fact]
    public void ExtractSessions_WithDeviceSwitching_ShouldPreserveOrder()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEventWithDeviceId(LogEventTypes.CAMERA_CONNECT, baseTime, "com.sec.android.app.camera", deviceId: 20),
            CreateEventWithDeviceId(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.sec.android.app.camera", deviceId: 20),
            CreateEventWithDeviceId(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(10), "com.sec.android.app.camera", deviceId: 21),
            CreateEventWithDeviceId(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(12), "com.sec.android.app.camera", deviceId: 21),
            CreateEventWithDeviceId(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(12), "com.sec.android.app.camera", deviceId: 20),
            CreateEventWithDeviceId(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(19), "com.sec.android.app.camera", deviceId: 20)
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        _output.WriteLine($"Total sessions: {result.Count}");
        foreach (var session in result)
        {
            _output.WriteLine($"Session: Start={session.StartTime:HH:mm:ss.fff}, End={session.EndTime:HH:mm:ss.fff}, DeviceIds={(session.CameraDeviceIds != null ? string.Join(", ", session.CameraDeviceIds) : "null")}");
        }
        
        result.Should().HaveCount(3, "3개의 별도 세션");
        
        // Session 1: device 20
        result[0].CameraDeviceIds.Should().NotBeNull();
        result[0].CameraDeviceIds.Should().ContainSingle();
        result[0].CameraDeviceIds![0].Should().Be(20);
        
        // Session 2: device 21
        result[1].CameraDeviceIds.Should().NotBeNull();
        result[1].CameraDeviceIds.Should().ContainSingle();
        result[1].CameraDeviceIds![0].Should().Be(21);
        
        // Session 3: device 20
        result[2].CameraDeviceIds.Should().NotBeNull();
        result[2].CameraDeviceIds.Should().ContainSingle();
        result[2].CameraDeviceIds![0].Should().Be(20);
    }

    private static NormalizedLogEvent CreateEventWithDeviceId(
        string eventType,
        DateTime timestamp,
        string package,
        int deviceId)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            Timestamp = timestamp,
            RawLine = $"CONNECT device {deviceId} client for package {package}",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = package,
                ["deviceId"] = deviceId,
                ["pid"] = 12345
            }
        };
    }
}

