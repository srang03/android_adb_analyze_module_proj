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
    /// Foreground Service 목록 (usagestats 기반)
    /// </summary>
    /// <remarks>
    /// <para>세션 내 실행된 모든 Foreground Service 정보 (START ~ STOP).</para>
    /// <para>
    /// <b>사용처:</b> BasePatternStrategy.ValidatePlayerEvent()
    /// - PLAYER_EVENT가 실제 촬영 관련인지 검증
    /// - PostProcessService 실행 중인지 확인
    /// - PostProcessService는 기본 카메라에서 촬영 후처리 시 실행됨
    /// </para>
    /// <para>
    /// <b>데이터 소스:</b> usagestats.log의 FOREGROUND_SERVICE 이벤트
    /// </para>
    /// </remarks>
    public IReadOnlyList<ForegroundServiceInfo> ForegroundServices { get; init; } = Array.Empty<ForegroundServiceInfo>();
}
