using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Models.Context;

/// <summary>
/// 세션 기반 로그 상관관계 분석을 위한 컨텍스트
/// </summary>
/// <remarks>
/// usagestats.log를 베이스로 하여 세션 내 모든 로그의 상관관계를 제공합니다.
/// 이를 통해 앱별 촬영 탐지 전략이 필요한 정보를 쉽게 조회할 수 있습니다.
/// </remarks>
public sealed class SessionContext
{
    /// <summary>
    /// 카메라 세션 정보
    /// </summary>
    public CameraSession Session { get; init; } = null!;
    
    /// <summary>
    /// 세션 범위 내 모든 로그 이벤트
    /// </summary>
    /// <remarks>
    /// 세션 시작 ~ 종료(+확장 시간) 사이의 모든 이벤트가 포함됩니다.
    /// </remarks>
    public IReadOnlyList<NormalizedLogEvent> AllEvents { get; init; } = Array.Empty<NormalizedLogEvent>();
    
    /// <summary>
    /// Activity Resume 시간 (usagestats 베이스)
    /// </summary>
    /// <remarks>
    /// ACTIVITY_RESUMED 이벤트의 타임스탬프.
    /// 앱이 포그라운드로 전환된 시점을 나타냅니다.
    /// </remarks>
    public DateTime? ActivityResumedTime { get; init; }
    
    /// <summary>
    /// Activity Pause 시간 (usagestats 베이스)
    /// </summary>
    /// <remarks>
    /// ACTIVITY_PAUSED 이벤트의 타임스탬프.
    /// 앱이 백그라운드로 전환된 시점을 나타냅니다.
    /// </remarks>
    public DateTime? ActivityPausedTime { get; init; }
    
    /// <summary>
    /// Foreground Service 목록 (usagestats 베이스)
    /// </summary>
    /// <remarks>
    /// 세션 내 실행된 모든 Foreground Service 정보.
    /// PostProcessService 등으로 실제 촬영 처리 시점을 식별할 수 있습니다.
    /// </remarks>
    public IReadOnlyList<ForegroundServiceInfo> ForegroundServices { get; init; } = Array.Empty<ForegroundServiceInfo>();
    
    /// <summary>
    /// 시간대별 이벤트 그룹화 (1초 단위)
    /// </summary>
    /// <remarks>
    /// Key: 시간 (초 단위로 버림)
    /// Value: 해당 시간대(±500ms)의 모든 이벤트
    /// 빠른 시간대 기반 조회를 위해 사용됩니다.
    /// </remarks>
    public IReadOnlyDictionary<DateTime, List<NormalizedLogEvent>> TimelineEvents { get; init; } 
        = new Dictionary<DateTime, List<NormalizedLogEvent>>();
}
