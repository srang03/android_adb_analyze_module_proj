namespace AndroidAdbAnalyze.Console.Executor.Core.Exceptions;

/// <summary>
/// 로그 수집 실패 시 발생하는 예외
/// </summary>
public class LogCollectionException : AndroidAdbAnalyzeException
{
    /// <summary>
    /// 수집 실패한 로그 이름
    /// </summary>
    public string LogName { get; }
    
    /// <summary>
    /// 필수 로그 여부
    /// </summary>
    public bool IsRequired { get; }
    
    /// <summary>
    /// ADB 명령 stderr 출력
    /// </summary>
    public string? StdError { get; }

    public LogCollectionException(
        string logName, 
        bool isRequired, 
        string? stdError = null,
        Exception? innerException = null)
        : base(
            $"{logName} 로그 수집 실패" + (isRequired ? " (필수)" : " (선택)"),
            isRequired ? ExitCode.LogCollectionFailed : ExitCode.Success,
            innerException)
    {
        LogName = logName;
        IsRequired = isRequired;
        StdError = stdError;
        
        UserFriendlyHelp = $@"
로그: {logName}
필수 여부: {(isRequired ? "필수" : "선택")}

";

        if (!string.IsNullOrEmpty(stdError))
        {
            UserFriendlyHelp += $"오류 내용:\n{stdError}\n\n";
        }

        UserFriendlyHelp += isRequired
            ? @"필수 로그 수집 실패로 분석을 계속할 수 없습니다.

확인 사항:
1. dumpsys 서비스가 존재하는지 확인:
   adb shell dumpsys -l | grep <service_name>

2. 디바이스 권한 확인
3. Android 버전 호환성 확인 (현재 Android 15 지원)
"
            : @"선택적 로그 수집 실패 (분석 계속 가능)

영향:
- 일부 이벤트 탐지 정확도 저하 가능
- 주요 분석 기능은 정상 동작

참고:
- 이 로그는 특정 제조사 전용일 수 있습니다
- 다른 로그로도 충분한 분석이 가능합니다
";
    }
}

