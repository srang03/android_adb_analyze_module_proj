namespace AndroidAdbAnalyze.Console.Executor.Core;

/// <summary>
/// 애플리케이션 종료 코드
/// </summary>
public enum ExitCode
{
    /// <summary>
    /// 성공
    /// </summary>
    Success = 0,
    
    /// <summary>
    /// ADB 실행 파일을 찾을 수 없음
    /// </summary>
    AdbNotFound = 1,
    
    /// <summary>
    /// 연결된 디바이스가 없음
    /// </summary>
    NoDeviceConnected = 2,
    
    /// <summary>
    /// 다중 디바이스 연결됨 (현재 버전은 단일 디바이스만 지원)
    /// </summary>
    MultipleDevicesConnected = 3,
    
    /// <summary>
    /// 로그 파싱 실패
    /// </summary>
    ParsingFailed = 4,
    
    /// <summary>
    /// 로그 분석 실패
    /// </summary>
    AnalysisFailed = 5,
    
    /// <summary>
    /// 잘못된 명령줄 인자
    /// </summary>
    InvalidArguments = 6,
    
    /// <summary>
    /// 로그 수집 실패 (필수 로그 포함)
    /// </summary>
    LogCollectionFailed = 7,
    
    /// <summary>
    /// 알 수 없는 오류
    /// </summary>
    UnknownError = 99
}

