using AndroidAdbAnalyzeModule.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 이벤트 타입별 중복 판정 전략 인터페이스
/// </summary>
/// <remarks>
/// 각 이벤트 타입의 특성에 맞는 중복 판정 로직을 제공합니다.
/// 예: 카메라 이벤트(배타적), 오디오 이벤트(연속 가능), URI 권한(속성 기반)
/// </remarks>
public interface IDeduplicationStrategy
{
    /// <summary>
    /// 두 이벤트가 중복인지 판단합니다.
    /// </summary>
    /// <param name="event1">첫 번째 이벤트</param>
    /// <param name="event2">두 번째 이벤트</param>
    /// <returns>중복이면 true, 아니면 false</returns>
    /// <remarks>
    /// 이 메서드는 순서에 무관하게 동작해야 합니다.
    /// IsDuplicate(e1, e2) == IsDuplicate(e2, e1)
    /// </remarks>
    bool IsDuplicate(NormalizedLogEvent event1, NormalizedLogEvent event2);
}

