using System.Diagnostics;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Orchestration;

/// <summary>
/// 전체 분석 파이프라인 오케스트레이션 서비스 구현
/// </summary>
public sealed class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly IEventDeduplicator _eventDeduplicator;
    private readonly ISessionDetector _sessionDetector;
    private readonly ICaptureDetector _captureDetector;
    private readonly ILogger<AnalysisOrchestrator> _logger;

    public AnalysisOrchestrator(
        IEventDeduplicator eventDeduplicator,
        ISessionDetector sessionDetector,
        ICaptureDetector captureDetector,
        ILogger<AnalysisOrchestrator> logger)
    {
        _eventDeduplicator = eventDeduplicator ?? throw new ArgumentNullException(nameof(eventDeduplicator));
        _sessionDetector = sessionDetector ?? throw new ArgumentNullException(nameof(sessionDetector));
        _captureDetector = captureDetector ?? throw new ArgumentNullException(nameof(captureDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions? options = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var warnings = new List<string>();

        _logger.LogInformation("분석 시작: 총 {Count}개 이벤트", events.Count);

        try
        {
            // 옵션 기본값 설정
            options ??= new AnalysisOptions();

            // Phase 1: 중복 제거 (0-20%)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0);
            _logger.LogDebug("Phase 1/4: 중복 제거 시작");

            var (uniqueEvents, deduplicationInfo) = await Task.Run(() =>
            {
                var result = _eventDeduplicator.Deduplicate(events, out var details);
                return (result, details);
            }, cancellationToken);

            _logger.LogInformation("중복 제거 완료: {Original}개 → {Unique}개 (제거: {Removed}개)",
                events.Count, uniqueEvents.Count, events.Count - uniqueEvents.Count);
            progress?.Report(20);

            // Phase 2: 세션 감지 (20-50%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 2/4: 세션 감지 시작");

            var sessions = await Task.Run(() =>
                _sessionDetector.DetectSessions(uniqueEvents, options),
                cancellationToken);

            _logger.LogInformation("세션 감지 완료: {Count}개 세션", sessions.Count);
            progress?.Report(50);

            // Phase 3: 촬영 감지 (50-80%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 3/4: 촬영 감지 시작");

            var allCaptures = new List<AndroidAdbAnalyze.Analysis.Models.Events.CameraCaptureEvent>();
            var updatedSessions = new List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>();
            var sessionCount = sessions.Count;

            for (int i = 0; i < sessionCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var session = sessions[i];
                var captures = await Task.Run(() =>
                    _captureDetector.DetectCaptures(session, uniqueEvents, options),
                    cancellationToken);

                allCaptures.AddRange(captures);
                
                // 세션에 촬영 ID 할당 (immutable이므로 새 객체 생성)
                var updatedSession = new AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession
                { 
                    SessionId = session.SessionId,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    PackageName = session.PackageName,
                    ProcessId = session.ProcessId,
                    SourceLogTypes = session.SourceLogTypes,
                    CaptureEventIds = captures.Select(c => c.CaptureId).ToList(),
                    StartEventId = session.StartEventId,
                    EndEventId = session.EndEventId,
                    IncompleteReason = session.IncompleteReason,
                    ConfidenceScore = session.ConfidenceScore,
                    SourceEventIds = session.SourceEventIds
                };
                updatedSessions.Add(updatedSession);
                
                _logger.LogDebug(
                    "세션 {SessionId} ({Package}): {CaptureCount}개 촬영 할당",
                    session.SessionId, session.PackageName, captures.Count);

                // 세션별 진행률 보고 (50% + 30% * (i+1)/sessionCount)
                var sessionProgress = 50 + (int)((30.0 * (i + 1)) / sessionCount);
                progress?.Report(sessionProgress);
            }

            _logger.LogInformation("촬영 감지 완료: {Count}개 촬영 이벤트", allCaptures.Count);
            progress?.Report(80);

            // Phase 4: 통계 계산 (80-100%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 4/4: 통계 계산 시작");

            stopwatch.Stop();
            var endTime = DateTime.UtcNow;

            var statistics = new AnalysisStatistics
            {
                TotalSourceEvents = events.Count,
                TotalSessions = updatedSessions.Count,
                CompleteSessions = updatedSessions.Count(s => !s.IsIncomplete),
                IncompleteSessions = updatedSessions.Count(s => s.IsIncomplete),
                TotalCaptureEvents = allCaptures.Count,
                DeduplicatedEvents = events.Count - uniqueEvents.Count,
                AnalysisStartTime = startTime,
                AnalysisEndTime = endTime,
                ProcessingTime = stopwatch.Elapsed
            };

            _logger.LogInformation("통계 계산 완료: 처리 시간 {Time}ms", stopwatch.ElapsedMilliseconds);
            progress?.Report(100);

            // 최종 결과 생성
            var result = new AnalysisResult
            {
                Success = true,
                Sessions = updatedSessions,
                CaptureEvents = allCaptures,
                SourceEvents = events,
                DeduplicationDetails = deduplicationInfo,
                DeviceInfo = events.FirstOrDefault()?.DeviceInfo,
                Statistics = statistics,
                Errors = errors,
                Warnings = warnings
            };

            _logger.LogInformation("분석 완료: 성공 (소요 시간: {Time}ms)", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("분석 취소됨 (소요 시간: {Time}ms)", stopwatch.ElapsedMilliseconds);

            // 취소된 경우 현재까지 결과 반환
            return new AnalysisResult
            {
                Success = false,
                SourceEvents = events,
                Statistics = new AnalysisStatistics
                {
                    TotalSourceEvents = events.Count,
                    AnalysisStartTime = startTime,
                    AnalysisEndTime = DateTime.UtcNow,
                    ProcessingTime = stopwatch.Elapsed
                },
                Errors = new[] { "분석이 취소되었습니다." },
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "분석 중 예외 발생 (소요 시간: {Time}ms)", stopwatch.ElapsedMilliseconds);

            errors.Add($"분석 중 예외 발생: {ex.Message}");

            return new AnalysisResult
            {
                Success = false,
                SourceEvents = events,
                Statistics = new AnalysisStatistics
                {
                    TotalSourceEvents = events.Count,
                    AnalysisStartTime = startTime,
                    AnalysisEndTime = DateTime.UtcNow,
                    ProcessingTime = stopwatch.Elapsed
                },
                Errors = errors,
                Warnings = warnings
            };
        }
    }
}
