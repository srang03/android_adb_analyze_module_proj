namespace AndroidAdbAnalyze.Analysis.Services.Reports;

/// <summary>
/// 보고서 생성 시 신뢰도 계산을 위한 헬퍼 클래스
/// </summary>
/// <remarks>
/// GetConfidenceBar와 동일한 로직으로 신뢰도를 계산하여 일관성을 보장합니다.
/// </remarks>
internal static class ConfidenceCalculatorHelper
{
    /// <summary>
    /// 신뢰도 점수를 퍼센트로 변환 (최대 100%로 제한)
    /// </summary>
    /// <param name="score">신뢰도 점수 (0.0 ~ 1.0 이상 가능)</param>
    /// <returns>퍼센트 값 (0 ~ 100)</returns>
    public static int ToPercent(double score)
    {
        return Math.Min((int)(score * 100), 100);
    }

    /// <summary>
    /// 촬영 이벤트 목록의 평균 신뢰도를 계산 (각 점수를 100%로 제한한 후 평균)
    /// </summary>
    /// <param name="captureEvents">촬영 이벤트 목록</param>
    /// <returns>평균 신뢰도 퍼센트 (0 ~ 100)</returns>
    public static double CalculateAverageConfidencePercent(IEnumerable<Models.Events.CameraCaptureEvent> captureEvents)
    {
        var events = captureEvents.ToList();
        if (!events.Any())
            return 0;

        // 각 점수를 100%로 제한한 후 평균 계산
        var percentValues = events.Select(e => ToPercent(e.CaptureDetectionScore));
        return percentValues.Average();
    }
}

