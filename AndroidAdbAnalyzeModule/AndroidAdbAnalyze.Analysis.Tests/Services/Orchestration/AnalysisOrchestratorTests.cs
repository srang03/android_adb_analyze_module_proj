using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Orchestration;

public sealed class AnalysisOrchestratorTests
{
    private readonly Mock<IEventDeduplicator> _mockDeduplicator;
    private readonly Mock<ISessionDetector> _mockSessionDetector;
    private readonly Mock<ICaptureDetector> _mockCaptureDetector;
    private readonly Mock<ILogger<AnalysisOrchestrator>> _mockLogger;
    private readonly AnalysisOrchestrator _orchestrator;

    public AnalysisOrchestratorTests()
    {
        _mockDeduplicator = new Mock<IEventDeduplicator>();
        _mockSessionDetector = new Mock<ISessionDetector>();
        _mockCaptureDetector = new Mock<ICaptureDetector>();
        _mockLogger = new Mock<ILogger<AnalysisOrchestrator>>();

        _orchestrator = new AnalysisOrchestrator(
            _mockDeduplicator.Object,
            _mockSessionDetector.Object,
            _mockCaptureDetector.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDeduplicator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AnalysisOrchestrator(
            null!,
            _mockSessionDetector.Object,
            _mockCaptureDetector.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("eventDeduplicator");
    }

    [Fact]
    public void Constructor_WithNullSessionDetector_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AnalysisOrchestrator(
            _mockDeduplicator.Object,
            null!,
            _mockCaptureDetector.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sessionDetector");
    }

    [Fact]
    public void Constructor_WithNullCaptureDetector_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AnalysisOrchestrator(
            _mockDeduplicator.Object,
            _mockSessionDetector.Object,
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("captureDetector");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AnalysisOrchestrator(
            _mockDeduplicator.Object,
            _mockSessionDetector.Object,
            _mockCaptureDetector.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region AnalyzeAsync - Basic Tests

    [Fact]
    public async Task AnalyzeAsync_WithNullEvents_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _orchestrator.AnalyzeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("events");
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyEvents_ReturnsSuccessResult()
    {
        // Arrange
        var emptyEvents = new List<NormalizedLogEvent>();
        SetupMocksForEmptyAnalysis();

        // Act
        var result = await _orchestrator.AnalyzeAsync(emptyEvents);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SourceEvents.Should().BeSameAs(emptyEvents);
        result.Sessions.Should().BeEmpty();
        result.CaptureEvents.Should().BeEmpty();
        result.Statistics.TotalSourceEvents.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidEvents_CallsAllPipelineStages()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var options = new AnalysisOptions();
        SetupMocksForSuccessfulAnalysis(events);

        // Act
        var result = await _orchestrator.AnalyzeAsync(events, options);

        // Assert
        _mockDeduplicator.Verify(x => x.Deduplicate(events, out It.Ref<IReadOnlyList<DeduplicationInfo>>.IsAny), Times.Once);
        _mockSessionDetector.Verify(x => x.DetectSessions(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), options), Times.Once);
        _mockCaptureDetector.Verify(x => x.DetectCaptures(It.IsAny<CameraSession>(), It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), options), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidEvents_ReturnsCorrectStatistics()
    {
        // Arrange
        var events = CreateTestEvents(10);
        var dedupEvents = CreateTestEvents(8);
        var sessions = CreateTestSessions(3);
        var captures = CreateTestCaptures(5);

        SetupMocksForSuccessfulAnalysis(events, dedupEvents, sessions, captures);

        // Act
        var result = await _orchestrator.AnalyzeAsync(events);

        // Assert
        result.Statistics.TotalSourceEvents.Should().Be(10);
        result.Statistics.TotalSessions.Should().Be(3);
        result.Statistics.CompleteSessions.Should().Be(2);
        result.Statistics.IncompleteSessions.Should().Be(1);
        result.Statistics.TotalCaptureEvents.Should().Be(15); // 3 sessions * 5 captures per session
        result.Statistics.DeduplicatedEvents.Should().Be(2);
        result.Statistics.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNullOptions_UsesDefaultOptions()
    {
        // Arrange
        var events = CreateTestEvents(1);
        SetupMocksForEmptyAnalysis();

        // Act
        var result = await _orchestrator.AnalyzeAsync(events, options: null);

        // Assert
        result.Should().NotBeNull();
        _mockSessionDetector.Verify(x => x.DetectSessions(
            It.IsAny<IReadOnlyList<NormalizedLogEvent>>(),
            It.Is<AnalysisOptions>(o => o != null)), Times.Once);
    }

    #endregion

    #region Progress Reporting Tests

    [Fact]
    public async Task AnalyzeAsync_WithProgressReporter_ReportsProgressAtEachStage()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var sessions = CreateTestSessions(2);
        SetupMocksForSuccessfulAnalysis(events, sessions: sessions);

        var progressValues = new List<int>();
        var progress = new Progress<int>(p => progressValues.Add(p));

        // Act
        await _orchestrator.AnalyzeAsync(events, progress: progress);

        // Assert
        progressValues.Should().Contain(0);   // 시작
        progressValues.Should().Contain(20);  // Deduplication 완료
        progressValues.Should().Contain(50);  // Session Detection 완료
        progressValues.Should().Contain(80);  // Capture Detection 완료
        progressValues.Should().Contain(100); // 최종 완료
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultipleSessions_ReportsProgressDuringCaptureDetection()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var sessions = CreateTestSessions(5);
        SetupMocksForSuccessfulAnalysis(events, sessions: sessions);

        var progressValues = new List<int>();
        var progress = new Progress<int>(p => progressValues.Add(p));

        // Act
        await _orchestrator.AnalyzeAsync(events, progress: progress);

        // Assert
        progressValues.Should().Contain(50);  // Session Detection 완료
        // 50% + 30% * (1/5) = 56%, 50% + 30% * (2/5) = 62%, ...
        progressValues.Should().Contain(x => x > 50 && x < 80);
        progressValues.Should().Contain(80);  // Capture Detection 완료
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoProgress_DoesNotThrow()
    {
        // Arrange
        var events = CreateTestEvents(1);
        SetupMocksForEmptyAnalysis();

        // Act & Assert
        var act = async () => await _orchestrator.AnalyzeAsync(events, progress: null);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task AnalyzeAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var result = await _orchestrator.AnalyzeAsync(events, cancellationToken: cts.Token);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("분석이 취소되었습니다.");
    }

    [Fact]
    public async Task AnalyzeAsync_WithCancellationDuringDeduplication_ReturnsPartialResult()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var cts = new CancellationTokenSource();

        _mockDeduplicator
            .Setup(x => x.Deduplicate(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), out It.Ref<IReadOnlyList<DeduplicationInfo>>.IsAny))
            .Callback(() => cts.Cancel())
            .Returns((IReadOnlyList<NormalizedLogEvent> e, IReadOnlyList<DeduplicationInfo> d) =>
            {
                d = new List<DeduplicationInfo>();
                return e;
            });

        // Act
        var result = await _orchestrator.AnalyzeAsync(events, cancellationToken: cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("분석이 취소되었습니다.");
        result.Statistics.TotalSourceEvents.Should().Be(5);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task AnalyzeAsync_WhenDeduplicatorThrows_ReturnsFailureResult()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var exceptionMessage = "Deduplication failed";

        _mockDeduplicator
            .Setup(x => x.Deduplicate(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), out It.Ref<IReadOnlyList<DeduplicationInfo>>.IsAny))
            .Throws(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await _orchestrator.AnalyzeAsync(events);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Contain(exceptionMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenSessionDetectorThrows_ReturnsFailureResult()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var exceptionMessage = "Session detection failed";

        SetupMocksForSuccessfulAnalysis(events);
        _mockSessionDetector
            .Setup(x => x.DetectSessions(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Throws(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await _orchestrator.AnalyzeAsync(events);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(exceptionMessage));
    }

    [Fact]
    public async Task AnalyzeAsync_WhenCaptureDetectorThrows_ReturnsFailureResult()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var sessions = CreateTestSessions(1);
        var exceptionMessage = "Capture detection failed";

        SetupMocksForSuccessfulAnalysis(events, sessions: sessions);
        _mockCaptureDetector
            .Setup(x => x.DetectCaptures(It.IsAny<CameraSession>(), It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Throws(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await _orchestrator.AnalyzeAsync(events);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(exceptionMessage));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task AnalyzeAsync_WithCompleteFlow_ProducesCompleteResult()
    {
        // Arrange
        var events = CreateTestEvents(20);
        var dedupEvents = CreateTestEvents(18);
        var sessions = CreateTestSessions(4);
        var captures = CreateTestCaptures(10);

        SetupMocksForSuccessfulAnalysis(events, dedupEvents, sessions, captures);

        var progressValues = new List<int>();
        var progress = new Progress<int>(p => progressValues.Add(p));

        // Act
        var result = await _orchestrator.AnalyzeAsync(events, progress: progress);

        // Assert
        result.Success.Should().BeTrue();
        result.Sessions.Should().HaveCount(4);
        result.CaptureEvents.Should().HaveCount(40); // 4 sessions * 10 captures per session
        result.DeduplicationDetails.Should().NotBeNull();
        result.Statistics.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
        progressValues.Should().Contain(100);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoSessions_StillProducesValidResult()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var dedupEvents = CreateTestEvents(5);
        var sessions = new List<CameraSession>(); // 세션 없음

        SetupMocksForSuccessfulAnalysis(events, dedupEvents, sessions);

        // Act
        var result = await _orchestrator.AnalyzeAsync(events);

        // Assert
        result.Success.Should().BeTrue();
        result.Sessions.Should().BeEmpty();
        result.CaptureEvents.Should().BeEmpty();
        result.Statistics.TotalSessions.Should().Be(0);
        result.Statistics.CompleteSessions.Should().Be(0);
    }

    #endregion

    #region Helper Methods

    private List<NormalizedLogEvent> CreateTestEvents(int count)
    {
        var events = new List<NormalizedLogEvent>();
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            events.Add(new NormalizedLogEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = baseTime.AddSeconds(i),
                EventType = LogEventTypes.CAMERA_CONNECT,
                SourceFileName = "audio.txt",
                Attributes = new Dictionary<string, object>
                {
                    ["package"] = "com.sec.android.app.camera"
                }.AsReadOnly()
            });
        }

        return events;
    }

    private List<CameraSession> CreateTestSessions(int count)
    {
        var sessions = new List<CameraSession>();
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            sessions.Add(new CameraSession
            {
                SessionId = Guid.NewGuid(),
                StartTime = baseTime.AddMinutes(i * 5),
                EndTime = (i == count - 1) ? null : baseTime.AddMinutes(i * 5 + 2), // 마지막 세션만 불완전 (EndTime = null)
                PackageName = "com.sec.android.app.camera",
                SessionCompletenessScore = 0.9,
                CaptureEventIds = new List<Guid>().AsReadOnly()
            });
        }

        return sessions;
    }

    private List<CameraCaptureEvent> CreateTestCaptures(int count)
    {
        var captures = new List<CameraCaptureEvent>();
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            captures.Add(new CameraCaptureEvent
            {
                CaptureId = Guid.NewGuid(),
                CaptureTime = baseTime.AddSeconds(i * 10),
                PackageName = "com.sec.android.app.camera",
                CaptureDetectionScore = 0.8,
                decisiveArtifact = Guid.NewGuid(),
                SupportingArtifactIds = new List<Guid>().AsReadOnly()
            });
        }

        return captures;
    }

    private void SetupMocksForEmptyAnalysis()
    {
        var emptyDedup = new List<DeduplicationInfo>();
        _mockDeduplicator
            .Setup(x => x.Deduplicate(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), out It.Ref<IReadOnlyList<DeduplicationInfo>>.IsAny))
            .Callback(new DeduplicateCallback((IReadOnlyList<NormalizedLogEvent> events, out IReadOnlyList<DeduplicationInfo> details) =>
            {
                details = emptyDedup;
            }))
            .Returns((IReadOnlyList<NormalizedLogEvent> events, IReadOnlyList<DeduplicationInfo> details) => events);

        _mockSessionDetector
            .Setup(x => x.DetectSessions(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Returns(new List<CameraSession>());

        _mockCaptureDetector
            .Setup(x => x.DetectCaptures(It.IsAny<CameraSession>(), It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Returns(new List<CameraCaptureEvent>());
    }

    private void SetupMocksForSuccessfulAnalysis(
        IReadOnlyList<NormalizedLogEvent> sourceEvents,
        IReadOnlyList<NormalizedLogEvent>? dedupEvents = null,
        IReadOnlyList<CameraSession>? sessions = null,
        IReadOnlyList<CameraCaptureEvent>? captures = null)
    {
        dedupEvents ??= sourceEvents;
        sessions ??= CreateTestSessions(2);
        captures ??= CreateTestCaptures(3);

        var emptyDedup = new List<DeduplicationInfo>();
        _mockDeduplicator
            .Setup(x => x.Deduplicate(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), out It.Ref<IReadOnlyList<DeduplicationInfo>>.IsAny))
            .Callback(new DeduplicateCallback((IReadOnlyList<NormalizedLogEvent> events, out IReadOnlyList<DeduplicationInfo> details) =>
            {
                details = emptyDedup;
            }))
            .Returns(dedupEvents);

        _mockSessionDetector
            .Setup(x => x.DetectSessions(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Returns(sessions);

        _mockCaptureDetector
            .Setup(x => x.DetectCaptures(It.IsAny<CameraSession>(), It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Returns(captures);
    }

    private delegate void DeduplicateCallback(IReadOnlyList<NormalizedLogEvent> events, out IReadOnlyList<DeduplicationInfo> details);

    #endregion
}
