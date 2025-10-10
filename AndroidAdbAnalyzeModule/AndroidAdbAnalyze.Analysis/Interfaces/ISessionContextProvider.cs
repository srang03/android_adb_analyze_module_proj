using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 세션 컨텍스트 제공자 인터페이스
/// </summary>
/// <remarks>
/// usagestats.log를 베이스로 세션 내 모든 로그의 상관관계를 구축합니다.
/// 앱별 전략이 세션 내 필요한 정보를 쉽게 조회할 수 있도록 합니다.
/// </remarks>
public interface ISessionContextProvider
{
    /// <summary>
    /// 세션 컨텍스트 생성
    /// </summary>
    /// <param name="session">카메라 세션</param>
    /// <param name="allEvents">전체 로그 이벤트</param>
    /// <returns>세션 컨텍스트</returns>
    /// <remarks>
    /// usagestats 로그를 기반으로:
    /// - ACTIVITY_RESUMED/PAUSED 시점 추출
    /// - FOREGROUND_SERVICE_START/STOP 추출
    /// - 세션 시간 범위 내 모든 이벤트 필터링
    /// - 시간대별 이벤트 그룹화
    /// </remarks>
    SessionContext CreateContext(
        CameraSession session, 
        IReadOnlyList<NormalizedLogEvent> allEvents);
}
