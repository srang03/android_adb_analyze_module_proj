using AndroidAdbAnalyze.Analysis.Models.Sessions;

namespace AndroidAdbAnalyze.Analysis.Models.Sessions;

/// <summary>
/// 카메라 사용 세션 (시작~종료)
/// </summary>
public sealed class CameraSession
{
    /// <summary>
    /// 세션 고유 ID
    /// </summary>
    public Guid SessionId { get; init; }
    
    /// <summary>
    /// 세션 시작 시간 (UTC)
    /// </summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>
    /// 세션 종료 시간 (UTC, null이면 불완전 세션)
    /// </summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>
    /// 패키지명 (예: com.sec.android.app.camera)
    /// </summary>
    public string PackageName { get; init; } = string.Empty;
    
    /// <summary>
    /// 프로세스 ID
    /// </summary>
    public int? ProcessId { get; init; }
    
    /// <summary>
    /// 이 세션을 감지한 로그 소스 (예: ["media.camera.worker", "usagestats"])
    /// </summary>
    public IReadOnlyList<string> SourceLogTypes { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 세션 내 촬영 이벤트 ID 목록
    /// </summary>
    public IReadOnlyList<Guid> CaptureEventIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 세션 시작을 나타내는 원본 이벤트 ID (순환 참조 방지)
    /// </summary>
    public Guid? StartEventId { get; init; }
    
    /// <summary>
    /// 세션 종료를 나타내는 원본 이벤트 ID (순환 참조 방지)
    /// </summary>
    public Guid? EndEventId { get; init; }
    
    /// <summary>
    /// 세션이 불완전한 이유 (불완전한 경우)
    /// </summary>
    public SessionIncompleteReason? IncompleteReason { get; init; }
    
    /// <summary>
    /// 세션이 불완전한지 여부 (시작 또는 종료 누락)
    /// </summary>
    public bool IsIncomplete => EndTime == null;
    
    /// <summary>
    /// 세션 지속 시간 (완전한 세션인 경우)
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    
    /// <summary>
    /// 신뢰도 점수 (0.0 ~ 1.0)
    /// </summary>
    public double ConfidenceScore { get; init; }
    
    /// <summary>
    /// 이 세션의 근거가 되는 원본 이벤트 ID 목록
    /// </summary>
    public IReadOnlyList<Guid> SourceEventIds { get; init; } = Array.Empty<Guid>();
}
