using AndroidAdbAnalyzeModule.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 신뢰도 점수 계산 서비스 인터페이스
/// </summary>
public interface IConfidenceCalculator
{
    /// <summary>
    /// 증거 이벤트 목록을 기반으로 신뢰도 점수를 계산합니다.
    /// </summary>
    /// <param name="evidenceEvents">증거 이벤트 목록</param>
    /// <returns>신뢰도 점수 (0.0 ~ 1.0)</returns>
    double CalculateConfidence(IReadOnlyList<NormalizedLogEvent> evidenceEvents);
    
    /// <summary>
    /// 특정 이벤트 타입의 가중치를 반환합니다.
    /// </summary>
    /// <param name="eventType">이벤트 타입</param>
    /// <returns>가중치 (0.0 ~ 1.0)</returns>
    double GetEventTypeWeight(string eventType);
}
