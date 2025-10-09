using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyze.Analysis.Interfaces;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Confidence;

/// <summary>
/// 신뢰도 점수 계산 서비스 구현
/// </summary>
public sealed class ConfidenceCalculator : IConfidenceCalculator
{
    private readonly ILogger<ConfidenceCalculator> _logger;
    
    // 이벤트 타입별 가중치 (0.0 ~ 1.0)
    private static readonly Dictionary<string, double> EventTypeWeights = new()
    {
        // 가장 강력한 증거 (직접 증거 - 미디어 저장)
        { LogEventTypes.DATABASE_INSERT, 0.5 },
        { LogEventTypes.DATABASE_EVENT, 0.5 },
        { LogEventTypes.MEDIA_INSERT_END, 0.5 }, // 미디어 삽입 완료 (촬영 완료 확정)
        
        // 강력한 증거 (세션 마커)
        { LogEventTypes.CAMERA_CONNECT, 0.4 },
        { LogEventTypes.CAMERA_DISCONNECT, 0.4 },
        
        // 중간 증거 (행위 마커 및 오디오 이벤트)
        { LogEventTypes.SILENT_CAMERA_CAPTURE, 0.9 }, // 무음 카메라 촬영 확정 (SilentCamera + Toast 패턴)
        { LogEventTypes.PLAYER_EVENT, 0.35 },         // 오디오 플레이어 이벤트 (셔터 음 재생)
        { LogEventTypes.URI_PERMISSION_GRANT, 0.3 },  // URI 권한 부여
        { LogEventTypes.URI_PERMISSION_REVOKE, 0.3 },
        { LogEventTypes.ACTIVITY_LIFECYCLE, 0.25 },
        { LogEventTypes.PLAYER_CREATED, 0.25 },       // 오디오 플레이어 생성 (셔터 음 준비)
        
        // 보조 증거 (부가 정보)
        { LogEventTypes.SHUTTER_SOUND, 0.2 },
        { LogEventTypes.MEDIA_EXTRACTOR, 0.2 },
        { LogEventTypes.PLAYER_RELEASED, 0.15 },           // 오디오 플레이어 해제
        { LogEventTypes.VIBRATION, 0.15 },
        { LogEventTypes.VIBRATION_EVENT, 0.4 },            // 촬영 버튼 터치 진동 (hapticType=50061)
        { LogEventTypes.CAMERA_ACTIVITY_REFRESH, 0.15 }    // 카메라 Activity Refresh Rate (화면 갱신 - 무음 카메라 탐지용)
    };
    
    private const double DefaultWeight = 0.1; // 알 수 없는 타입의 기본 가중치
    private const double MaxConfidence = 1.0;

    public ConfidenceCalculator(ILogger<ConfidenceCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public double CalculateConfidence(IReadOnlyList<NormalizedLogEvent> evidenceEvents)
    {
        if (evidenceEvents == null || evidenceEvents.Count == 0)
        {
            _logger.LogDebug("증거 이벤트가 없으므로 신뢰도 0.0 반환");
            return 0.0;
        }

        // 이벤트 타입별 가중치 합산
        double totalWeight = 0.0;
        var uniqueTypes = new HashSet<string>();

        foreach (var evt in evidenceEvents)
        {
            // 동일 타입은 한 번만 계산 (중복 방지)
            if (!uniqueTypes.Add(evt.EventType))
                continue;

            var weight = GetEventTypeWeight(evt.EventType);
            totalWeight += weight;

            _logger.LogTrace(
                "EventType '{EventType}' 가중치 추가: {Weight} (누적: {Total})",
                evt.EventType, weight, totalWeight);
        }

        // 최대값 제한
        var confidence = Math.Min(totalWeight, MaxConfidence);

        _logger.LogDebug(
            "신뢰도 계산 완료: {Confidence:F2} (증거 {Count}개, 고유 타입 {UniqueCount}개)",
            confidence, evidenceEvents.Count, uniqueTypes.Count);

        return confidence;
    }

    /// <inheritdoc/>
    public double GetEventTypeWeight(string eventType)
    {
        if (EventTypeWeights.TryGetValue(eventType, out var weight))
            return weight;

        _logger.LogTrace("알 수 없는 EventType '{EventType}', 기본 가중치 {Weight} 반환",
            eventType, DefaultWeight);
        
        return DefaultWeight;
    }
}
