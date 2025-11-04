using AndroidAdbAnalyze.Analysis.Constants;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Visualization;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Visualization;

/// <summary>
/// 타임라인 생성 서비스 구현
/// </summary>
public sealed class TimelineBuilder : ITimelineBuilder
{
    private readonly ILogger<TimelineBuilder> _logger;

    /// <summary>
    /// TimelineBuilder 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="logger">로거</param>
    public TimelineBuilder(ILogger<TimelineBuilder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IReadOnlyList<TimelineItem> BuildTimeline(AnalysisResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        _logger.LogDebug("타임라인 생성 시작: 세션 {SessionCount}개, 촬영 {CaptureCount}개",
            result.Sessions.Count, result.CaptureEvents.Count);

        var timelineItems = new List<TimelineItem>();

        // 1. 세션 → TimelineItem 변환
        var sessionItems = result.Sessions
            .Select((session, index) => CreateSessionItem(session, index + 1))
            .ToList();
        timelineItems.AddRange(sessionItems);

        // 2. 촬영 → TimelineItem 변환
        var captureItems = result.CaptureEvents
            .Select((capture, index) => CreateCaptureItem(capture, index + 1))
            .ToList();
        timelineItems.AddRange(captureItems);

        // 3. 전송 → TimelineItem 변환 (촬영 중 전송된 것만)
        var transmissionItems = result.CaptureEvents
            .Where(c => c.IsTransmitted)
            .Select((capture, index) => CreateTransmissionItem(capture, index + 1))
            .ToList();
        timelineItems.AddRange(transmissionItems);

        // 4. 시간순 정렬
        var sortedItems = timelineItems
            .OrderBy(item => item.StartTime)
            .ToList();

        _logger.LogInformation(
            "타임라인 생성 완료: 총 {Count}개 항목 (세션 {SessionCount}개, 촬영 {CaptureCount}개, 전송 {TransmissionCount}개)",
            sortedItems.Count, sessionItems.Count, captureItems.Count, transmissionItems.Count);

        return sortedItems;
    }

    /// <summary>
    /// CameraSession을 TimelineItem으로 변환
    /// </summary>
    private TimelineItem CreateSessionItem(AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession session, int index)
    {
        var label = session.IsIncomplete
            ? $"카메라 세션 #{index} (불완전)"
            : $"카메라 세션 #{index}";

        var metadata = new Dictionary<string, string>
        {
            ["Type"] = "Session",
            ["IsIncomplete"] = session.IsIncomplete.ToString(),
            ["Duration"] = session.Duration?.TotalSeconds.ToString("F1") ?? "N/A"
        };

        if (session.IncompleteReason.HasValue)
        {
            metadata["IncompleteReason"] = session.IncompleteReason.Value.ToString();
        }

        return new TimelineItem
        {
            EventId = session.SessionId,
            EventType = TimelineEventTypes.CAMERA_SESSION,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            PackageName = session.PackageName,
            Label = label,
            Score = session.SessionCompletenessScore,
            ColorHint = GetColorHint(session.SessionCompletenessScore),
            Metadata = metadata
        };
    }

    /// <summary>
    /// CameraCaptureEvent를 TimelineItem으로 변환
    /// </summary>
    private TimelineItem CreateCaptureItem(AndroidAdbAnalyze.Analysis.Models.Events.CameraCaptureEvent capture, int index)
    {
        var label = capture.IsEstimated
            ? $"촬영 #{index} (추정)"
            : $"촬영 #{index}";

        var metadata = new Dictionary<string, string>
        {
            ["Type"] = "Capture",
            ["IsEstimated"] = capture.IsEstimated.ToString(),
            ["ArtifactCount"] = (capture.SupportingArtifactIds.Count + 1).ToString() // +1 for primary
        };

        if (!string.IsNullOrEmpty(capture.FilePath))
        {
            metadata["FilePath"] = capture.FilePath;
        }

        if (!string.IsNullOrEmpty(capture.FileUri))
        {
            metadata["FileUri"] = capture.FileUri;
        }

        // 전송 관련 메타데이터 추가
        if (capture.IsTransmitted)
        {
            metadata["IsTransmitted"] = "true";
            metadata["TransmissionTime"] = capture.TransmissionTime?.ToString("HH:mm:ss.fff") ?? "N/A";
            metadata["TransmittedPackets"] = capture.TransmittedPackets?.ToString() ?? "0";
        }

        return new TimelineItem
        {
            EventId = capture.CaptureId,
            EventType = TimelineEventTypes.CAMERA_CAPTURE,
            StartTime = capture.CaptureTime,
            EndTime = null, // 촬영은 순간 이벤트
            PackageName = capture.PackageName,
            Label = label,
            Score = capture.CaptureDetectionScore,
            ColorHint = GetColorHint(capture.CaptureDetectionScore),
            Metadata = metadata
        };
    }

    /// <summary>
    /// CameraCaptureEvent (전송됨)를 전송 TimelineItem으로 변환
    /// </summary>
    /// <param name="capture">전송된 촬영 이벤트</param>
    /// <param name="index">전송 인덱스</param>
    /// <returns>전송 타임라인 아이템</returns>
    private TimelineItem CreateTransmissionItem(
        AndroidAdbAnalyze.Analysis.Models.Events.CameraCaptureEvent capture, 
        int index)
    {
        var transmissionTime = capture.TransmissionTime ?? capture.CaptureTime;
        var packets = capture.TransmittedPackets ?? 0;
        
        var label = $"전송 #{index} ({packets}개 패킷)";
        
        var metadata = new Dictionary<string, string>
        {
            ["Type"] = "Transmission",
            ["CaptureId"] = capture.CaptureId.ToString(),
            ["TransmittedPackets"] = packets.ToString(),
            ["TransmissionMethod"] = "WiFi"
        };

        return new TimelineItem
        {
            EventId = Guid.NewGuid(), // 전송 이벤트 고유 ID
            EventType = TimelineEventTypes.TRANSMISSION,
            StartTime = transmissionTime,
            EndTime = null, // 전송은 순간 이벤트
            PackageName = capture.PackageName,
            Label = label,
            Score = capture.CaptureDetectionScore,
            ColorHint = "red", // 전송은 빨간색으로 표시
            Metadata = metadata
        };
    }

    /// <summary>
    /// 탐지 점수를 기반으로 색상 힌트 생성
    /// </summary>
    /// <remarks>
    /// 촬영: 촬영 탐지 점수 (Capture Detection Score)
    /// 세션: 세션 완전성 점수 (Session Completeness Score)
    /// </remarks>
    private static string GetColorHint(double score)
    {
        return score switch
        {
            >= 0.8 => "green",
            >= 0.5 => "yellow",
            _ => "red"
        };
    }
}
