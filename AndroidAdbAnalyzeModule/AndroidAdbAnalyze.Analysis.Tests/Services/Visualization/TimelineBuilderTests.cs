using AndroidAdbAnalyze.Analysis.Constants;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Visualization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Visualization;

public sealed class TimelineBuilderTests
{
    private readonly ITimelineBuilder _builder;

    public TimelineBuilderTests()
    {
        _builder = new TimelineBuilder(NullLogger<TimelineBuilder>.Instance);
    }

    [Fact]
    public void BuildTimeline_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var result = CreateEmptyResult();

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().NotBeNull();
        timeline.Should().BeEmpty();
    }

    [Fact]
    public void BuildTimeline_WithNullResult_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _builder.BuildTimeline(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public void BuildTimeline_WithSessionsOnly_CreatesSessionItems()
    {
        // Arrange
        var session1 = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var session2 = CreateSession(2, new DateTime(2025, 1, 1, 12, 5, 0, DateTimeKind.Utc));
        var result = CreateResult(new[] { session1, session2 }, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(2);
        timeline[0].EventType.Should().Be(TimelineEventTypes.CAMERA_SESSION);
        timeline[0].Label.Should().Contain("카메라 세션 #1");
        timeline[1].EventType.Should().Be(TimelineEventTypes.CAMERA_SESSION);
        timeline[1].Label.Should().Contain("카메라 세션 #2");
    }

    [Fact]
    public void BuildTimeline_WithCapturesOnly_CreatesCaptureItems()
    {
        // Arrange
        var capture1 = CreateCapture(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var capture2 = CreateCapture(2, new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc));
        var result = CreateResult(Array.Empty<CameraSession>(), new[] { capture1, capture2 });

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(2);
        timeline[0].EventType.Should().Be(TimelineEventTypes.CAMERA_CAPTURE);
        timeline[0].Label.Should().Contain("촬영 #1");
        timeline[0].EndTime.Should().BeNull(); // 순간 이벤트
        timeline[1].EventType.Should().Be(TimelineEventTypes.CAMERA_CAPTURE);
        timeline[1].Label.Should().Contain("촬영 #2");
    }

    [Fact]
    public void BuildTimeline_WithMixedEvents_SortsByStartTime()
    {
        // Arrange
        var session = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var capture1 = CreateCapture(1, new DateTime(2025, 1, 1, 12, 2, 0, DateTimeKind.Utc));
        var capture2 = CreateCapture(2, new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc));
        var result = CreateResult(new[] { session }, new[] { capture1, capture2 });

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(3);
        timeline[0].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)); // session
        timeline[1].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc)); // capture2
        timeline[2].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 2, 0, DateTimeKind.Utc)); // capture1
    }

    [Fact]
    public void BuildTimeline_WithIncompleteSessions_AddsIncompleteMarkerToLabel()
    {
        // Arrange
        var incompleteSession = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), isIncomplete: true);
        var result = CreateResult(new[] { incompleteSession }, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(1);
        timeline[0].Label.Should().Contain("(불완전)");
        timeline[0].Metadata["IsIncomplete"].Should().Be("True");
    }

    [Fact]
    public void BuildTimeline_WithEstimatedCaptures_AddsEstimatedMarkerToLabel()
    {
        // Arrange
        var estimatedCapture = CreateCapture(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), isEstimated: true);
        var result = CreateResult(Array.Empty<CameraSession>(), new[] { estimatedCapture });

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(1);
        timeline[0].Label.Should().Contain("(추정)");
        timeline[0].Metadata["IsEstimated"].Should().Be("True");
    }

    [Fact]
    public void BuildTimeline_WithHighConfidence_ReturnsGreenColorHint()
    {
        // Arrange
        var session = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), confidenceScore: 0.9);
        var result = CreateResult(new[] { session }, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline[0].ColorHint.Should().Be("green");
    }

    [Fact]
    public void BuildTimeline_WithMediumConfidence_ReturnsYellowColorHint()
    {
        // Arrange
        var session = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), confidenceScore: 0.6);
        var result = CreateResult(new[] { session }, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline[0].ColorHint.Should().Be("yellow");
    }

    [Fact]
    public void BuildTimeline_WithLowConfidence_ReturnsRedColorHint()
    {
        // Arrange
        var session = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), confidenceScore: 0.3);
        var result = CreateResult(new[] { session }, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline[0].ColorHint.Should().Be("red");
    }

    [Fact]
    public void BuildTimeline_WithSessionMetadata_IncludesAllRelevantInfo()
    {
        // Arrange
        var session = CreateSession(
            1,
            new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            isIncomplete: true,
            incompleteReason: SessionIncompleteReason.MissingEnd);
        var result = CreateResult(new[] { session }, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        var item = timeline[0];
        item.Metadata["Type"].Should().Be("Session");
        item.Metadata["IsIncomplete"].Should().Be("True");
        item.Metadata["IncompleteReason"].Should().Be("MissingEnd");
        item.Metadata.Should().ContainKey("Duration");
    }

    [Fact]
    public void BuildTimeline_WithCaptureMetadata_IncludesAllRelevantInfo()
    {
        // Arrange
        var capture = CreateCapture(
            1,
            new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            filePath: "/storage/DCIM/IMG_001.jpg",
            fileUri: "content://media/external/images/123");
        var result = CreateResult(Array.Empty<CameraSession>(), new[] { capture });

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        var item = timeline[0];
        item.Metadata["Type"].Should().Be("Capture");
        item.Metadata["FilePath"].Should().Be("/storage/DCIM/IMG_001.jpg");
        item.Metadata["FileUri"].Should().Be("content://media/external/images/123");
        item.Metadata.Should().ContainKey("EvidenceCount");
    }

    [Fact]
    public void BuildTimeline_WithMultipleSessions_AssignsSequentialNumbers()
    {
        // Arrange
        var sessions = Enumerable.Range(1, 5)
            .Select(i => CreateSession(i, new DateTime(2025, 1, 1, 12, i, 0, DateTimeKind.Utc)))
            .ToArray();
        var result = CreateResult(sessions, Array.Empty<CameraCaptureEvent>());

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            timeline[i].Label.Should().Contain($"#{i + 1}");
        }
    }

    [Fact]
    public void BuildTimeline_WithComplexScenario_ProducesCorrectTimeline()
    {
        // Arrange
        var session1 = CreateSession(1, new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), confidenceScore: 0.9);
        var capture1 = CreateCapture(1, new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc), confidenceScore: 0.8);
        var capture2 = CreateCapture(2, new DateTime(2025, 1, 1, 12, 2, 0, DateTimeKind.Utc), confidenceScore: 0.7);
        var session2 = CreateSession(2, new DateTime(2025, 1, 1, 12, 5, 0, DateTimeKind.Utc), confidenceScore: 0.6, isIncomplete: true);

        var result = CreateResult(
            new[] { session1, session2 },
            new[] { capture1, capture2 });

        // Act
        var timeline = _builder.BuildTimeline(result);

        // Assert
        timeline.Should().HaveCount(4);
        
        // 시간순 정렬 확인
        timeline[0].EventType.Should().Be(TimelineEventTypes.CAMERA_SESSION);
        timeline[0].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        
        timeline[1].EventType.Should().Be(TimelineEventTypes.CAMERA_CAPTURE);
        timeline[1].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc));
        
        timeline[2].EventType.Should().Be(TimelineEventTypes.CAMERA_CAPTURE);
        timeline[2].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 2, 0, DateTimeKind.Utc));
        
        timeline[3].EventType.Should().Be(TimelineEventTypes.CAMERA_SESSION);
        timeline[3].StartTime.Should().Be(new DateTime(2025, 1, 1, 12, 5, 0, DateTimeKind.Utc));
        timeline[3].Label.Should().Contain("(불완전)");
    }

    // Helper methods
    private AnalysisResult CreateEmptyResult()
    {
        return new AnalysisResult
        {
            Success = true,
            Sessions = Array.Empty<CameraSession>(),
            CaptureEvents = Array.Empty<CameraCaptureEvent>(),
            DeduplicationDetails = Array.Empty<AndroidAdbAnalyze.Analysis.Models.Deduplication.DeduplicationInfo>(),
            Statistics = new AndroidAdbAnalyze.Analysis.Models.Results.AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.Zero
            },
            Errors = Array.Empty<string>(),
            Warnings = Array.Empty<string>()
        };
    }

    private AnalysisResult CreateResult(CameraSession[] sessions, CameraCaptureEvent[] captures)
    {
        return new AnalysisResult
        {
            Success = true,
            Sessions = sessions,
            CaptureEvents = captures,
            DeduplicationDetails = Array.Empty<AndroidAdbAnalyze.Analysis.Models.Deduplication.DeduplicationInfo>(),
            Statistics = new AndroidAdbAnalyze.Analysis.Models.Results.AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = sessions.Length,
                CompleteSessions = sessions.Count(s => !s.IsIncomplete),
                IncompleteSessions = sessions.Count(s => s.IsIncomplete),
                TotalCaptureEvents = captures.Length,
                ProcessingTime = TimeSpan.Zero
            },
            Errors = Array.Empty<string>(),
            Warnings = Array.Empty<string>()
        };
    }

    private CameraSession CreateSession(
        int index,
        DateTime startTime,
        bool isIncomplete = false,
        SessionIncompleteReason? incompleteReason = null,
        double confidenceScore = 0.8)
    {
        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            PackageName = "com.sec.android.app.camera",
            StartTime = startTime,
            EndTime = isIncomplete ? null : startTime.AddMinutes(2),
            IncompleteReason = incompleteReason,
            ConfidenceScore = confidenceScore,
            StartEventId = Guid.NewGuid(),
            EndEventId = isIncomplete ? null : Guid.NewGuid(),
            CaptureEventIds = Array.Empty<Guid>()
        };
    }

    private CameraCaptureEvent CreateCapture(
        int index,
        DateTime captureTime,
        bool isEstimated = false,
        double confidenceScore = 0.8,
        string? filePath = null,
        string? fileUri = null)
    {
        return new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            PackageName = "com.sec.android.app.camera",
            CaptureTime = captureTime,
            ConfidenceScore = confidenceScore,
            IsEstimated = isEstimated,
            PrimaryEvidenceId = Guid.NewGuid(),
            SupportingEvidenceIds = Array.Empty<Guid>(),
            FilePath = filePath,
            FileUri = fileUri
        };
    }
}
