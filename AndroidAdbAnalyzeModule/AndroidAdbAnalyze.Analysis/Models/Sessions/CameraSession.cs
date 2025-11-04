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
    /// 세션 완전성 점수 (Session Completeness Score, 0.0 ~ 1.0)
    /// </summary>
    /// <remarks>
    /// 세션을 구성하는 이벤트 타입의 가중치 합으로 계산됨.
    /// - 완전 세션 (시작+종료): usagestats 0.95, media.camera 0.85
    /// - 불완전 세션 (시작만): usagestats 0.7, media.camera 0.6
    /// 학술 논문에서는 "세션 완전성 점수 (Session Completeness Score)"로 표현.
    /// 
    /// 주의: CameraCaptureEvent.CaptureDetectionScore(촬영 탐지 점수)와 다른 개념!
    /// </remarks>
    public double SessionCompletenessScore { get; init; }
    
    /// <summary>
    /// 이 세션의 근거가 되는 원본 이벤트 ID 목록
    /// </summary>
    public IReadOnlyList<Guid> SourceEventIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 카메라 디바이스 ID 목록 (전면/후면 카메라 전환 이력)
    /// </summary>
    /// <remarks>
    /// Android 카메라 디바이스 ID 패턴 (기기 및 제조사마다 다를 수 있음):
    /// 
    /// **Samsung Galaxy 기기 (테스트 환경 기준)**:
    /// - device 0: 후면 카메라 (메인 카메라)
    /// - device 20: 후면 카메라 (광각 또는 메인)
    /// - device 21: 전면 카메라
    /// 
    /// **일반적인 Android 기기**:
    /// - device 0: 기본 카메라 (대부분 후면, 일부 기기는 전면)
    /// - device 1: 보조 카메라 (대부분 전면, 일부 기기는 후면)
    /// 
    /// **중요**: device ID와 전면/후면 매핑은 기기마다 다르므로, 
    /// media_camera.log의 "Facing: Back/Front" 정보를 참고하여 정확히 판단해야 합니다.
    /// 
    /// 사용 예시:
    /// - null: usagestats 기반 세션 (device 정보 없음)
    /// - [0]: device 0 카메라만 사용
    /// - [20]: device 20 카메라만 사용
    /// - [20, 21, 20]: device 20 → device 21 → device 20 전환 이력
    /// 
    /// 하나의 세션 내에서 사용자가 카메라를 전환한 경우,
    /// 순서대로 기록되어 전환 이력을 제공합니다.
    /// 
    /// 이 정보는 media_camera.log의 CAMERA_CONNECT/DISCONNECT 이벤트에서 추출됩니다.
    /// </remarks>
    public IReadOnlyList<int>? CameraDeviceIds { get; init; }
}
