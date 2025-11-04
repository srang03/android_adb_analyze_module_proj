using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Services.Validation;

/// <summary>
/// FOREGROUND_SERVICE 아티팩트 검증 공통 헬퍼
/// </summary>
/// <remarks>
/// <para>
/// BasePatternStrategy와 KakaoTalkStrategy에서 공통으로 사용하는 
/// FOREGROUND_SERVICE 검증 로직을 제공합니다.
/// </para>
/// <para>
/// 목적: 코드 중복 제거 및 재사용성 향상
/// </para>
/// <para>
/// 추가일: 2025-10-26 (Phase 2 리팩토링)
/// </para>
/// </remarks>
internal static class ForegroundServiceValidator
{
    /// <summary>
    /// FOREGROUND_SERVICE_START 이벤트 기본 검증
    /// </summary>
    /// <param name="artifact">검증할 FOREGROUND_SERVICE 이벤트</param>
    /// <returns>
    /// - true: serviceState가 "FOREGROUND_SERVICE_START"인 경우
    /// - false: serviceState가 null, 빈 문자열, 또는 "FOREGROUND_SERVICE_START"가 아닌 경우
    /// </returns>
    /// <remarks>
    /// FOREGROUND_SERVICE_STOP 이벤트는 제외하고 START만 허용합니다.
    /// </remarks>
    public static bool IsValidStartEvent(NormalizedLogEvent artifact)
    {
        if (artifact == null)
            return false;

        var serviceState = artifact.Attributes
            .GetValueOrDefault("serviceState")?.ToString();
        
        return serviceState == "FOREGROUND_SERVICE_START";
    }
    
    /// <summary>
    /// FOREGROUND_SERVICE 이벤트에서 className 추출
    /// </summary>
    /// <param name="artifact">FOREGROUND_SERVICE 이벤트</param>
    /// <returns>
    /// - className 문자열: 추출 성공
    /// - null: className 속성이 없거나 빈 문자열인 경우
    /// </returns>
    /// <remarks>
    /// 추출된 className은 패턴 매칭에 사용됩니다 (예: "PostProcessService", "NotificationService").
    /// </remarks>
    public static string? ExtractClassName(NormalizedLogEvent artifact)
    {
        if (artifact == null)
            return null;

        var className = artifact.Attributes
            .GetValueOrDefault("className")?.ToString();
        
        return string.IsNullOrEmpty(className) ? null : className;
    }
    
    /// <summary>
    /// className이 지정된 패턴 목록과 매칭되는지 검증
    /// </summary>
    /// <param name="className">검증할 className (추출된 서비스 클래스명)</param>
    /// <param name="patterns">매칭할 패턴 목록 (예: ["PostProcessService", "NotificationService"])</param>
    /// <param name="comparison">문자열 비교 방식 (기본값: 대소문자 무시)</param>
    /// <returns>
    /// - true: className이 patterns 중 하나라도 포함하는 경우
    /// - false: 매칭되는 패턴이 없거나 입력이 유효하지 않은 경우
    /// </returns>
    /// <remarks>
    /// <para>
    /// Contains 방식 사용:
    /// - "com.samsung.android.camera.core2.processor.PostProcessService" → "PostProcessService" 매칭됨
    /// - "com.sec.android.app.camera.service.NotificationService" → "NotificationService" 매칭됨
    /// </para>
    /// <para>
    /// null 안전성:
    /// - className이 null 또는 빈 문자열이면 false 반환
    /// - patterns가 null 또는 빈 컬렉션이면 false 반환
    /// </para>
    /// </remarks>
    public static bool MatchesAnyPattern(
        string? className,
        IEnumerable<string>? patterns,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrEmpty(className))
            return false;
        
        if (patterns == null)
            return false;
        
        return patterns.Any(pattern => 
            !string.IsNullOrEmpty(pattern) && 
            className.Contains(pattern, comparison));
    }
}

