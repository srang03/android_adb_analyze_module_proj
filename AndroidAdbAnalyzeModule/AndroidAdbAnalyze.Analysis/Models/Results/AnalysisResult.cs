using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Models.Results;

/// <summary>
/// 로그 분석 최종 결과
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>
    /// 분석 성공 여부
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 감지된 카메라 세션 목록
    /// </summary>
    public IReadOnlyList<CameraSession> Sessions { get; init; } = Array.Empty<CameraSession>();
    
    /// <summary>
    /// 감지된 촬영 이벤트 목록
    /// </summary>
    public IReadOnlyList<CameraCaptureEvent> CaptureEvents { get; init; } = 
        Array.Empty<CameraCaptureEvent>();
    
    /// <summary>
    /// 원본 NormalizedLogEvent 목록 (참조용)
    /// </summary>
    public IReadOnlyList<NormalizedLogEvent> SourceEvents { get; init; } = 
        Array.Empty<NormalizedLogEvent>();
    
    /// <summary>
    /// 중복 제거 정보 (디버깅 및 검증용)
    /// </summary>
    public IReadOnlyList<DeduplicationInfo> DeduplicationDetails { get; init; } = 
        Array.Empty<DeduplicationInfo>();
    
    /// <summary>
    /// 디바이스 정보
    /// </summary>
    public DeviceInfo? DeviceInfo { get; init; }
    
    /// <summary>
    /// 분석 통계
    /// </summary>
    public AnalysisStatistics Statistics { get; init; } = new();
    
    /// <summary>
    /// 분석 에러 목록
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 분석 경고 목록
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
