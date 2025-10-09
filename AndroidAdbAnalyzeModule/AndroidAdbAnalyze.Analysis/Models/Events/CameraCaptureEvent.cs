namespace AndroidAdbAnalyze.Analysis.Models.Events;

/// <summary>
/// 카메라 촬영 이벤트
/// </summary>
public sealed class CameraCaptureEvent
{
    /// <summary>
    /// 촬영 이벤트 고유 ID
    /// </summary>
    public Guid CaptureId { get; init; }
    
    /// <summary>
    /// 소속 세션 ID (연결용, 순환 참조 방지)
    /// </summary>
    public Guid ParentSessionId { get; init; }
    
    /// <summary>
    /// 촬영 시각 (UTC)
    /// </summary>
    public DateTime CaptureTime { get; init; }
    
    /// <summary>
    /// 패키지명
    /// </summary>
    public string PackageName { get; init; } = string.Empty;
    
    /// <summary>
    /// 이미지 파일 경로 (있는 경우)
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// 파일 URI (있는 경우)
    /// </summary>
    public string? FileUri { get; init; }
    
    /// <summary>
    /// 가장 강력한 증거 이벤트 ID (예: DATABASE_INSERT, 순환 참조 방지)
    /// </summary>
    public Guid? PrimaryEvidenceId { get; init; }
    
    /// <summary>
    /// 보조 증거 이벤트 ID 목록 (예: SHUTTER_SOUND, VIBRATION, 순환 참조 방지)
    /// </summary>
    public IReadOnlyList<Guid> SupportingEvidenceIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 직접 증거 없이 추정된 촬영인지 여부
    /// </summary>
    public bool IsEstimated { get; init; }
    
    /// <summary>
    /// 신뢰도 점수 (0.0 ~ 1.0)
    /// </summary>
    public double ConfidenceScore { get; init; }
    
    /// <summary>
    /// 증거 타입 목록 (예: ["DATABASE_INSERT", "SHUTTER_SOUND"])
    /// </summary>
    public IReadOnlyList<string> EvidenceTypes { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 이 촬영 이벤트의 근거가 되는 원본 이벤트 ID 목록
    /// </summary>
    public IReadOnlyList<Guid> SourceEventIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 추가 메타데이터 (필요시 확장 가능)
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();
}
