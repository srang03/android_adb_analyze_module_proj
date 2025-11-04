using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 탐지 점수 계산 서비스 인터페이스
/// </summary>
/// <remarks>
/// 촬영: 촬영 탐지 점수 (Capture Detection Score)
/// 세션: 세션 완전성 점수 (Session Completeness Score)
/// </remarks>
public interface IConfidenceCalculator
{
    /// <summary>
    /// 아티팩트 이벤트 목록을 기반으로 탐지 점수를 계산합니다.
    /// </summary>
    /// <param name="artifactEvents">아티팩트 이벤트 목록</param>
    /// <returns>탐지 점수 (0.0 ~ 1.0)</returns>
    double CalculateConfidence(IReadOnlyList<NormalizedLogEvent> artifactEvents);
    
    /// <summary>
    /// 특정 이벤트 타입의 가중치를 반환합니다.
    /// </summary>
    /// <param name="eventType">이벤트 타입</param>
    /// <returns>가중치 (0.0 ~ 1.0)</returns>
    double GetEventTypeWeight(string eventType);
}
