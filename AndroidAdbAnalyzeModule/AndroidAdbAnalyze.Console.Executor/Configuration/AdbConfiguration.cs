namespace AndroidAdbAnalyze.Console.Executor.Configuration;

/// <summary>
/// ADB 실행 설정 (appsettings.json의 "Adb" 섹션)
/// </summary>
public sealed class AdbConfiguration
{
    /// <summary>
    /// ADB 실행 파일 경로 (null이면 PATH에서 찾기)
    /// </summary>
    public string? ExecutablePath { get; set; }
    
    /// <summary>
    /// 연결 타임아웃 (초)
    /// </summary>
    public int ConnectionTimeout { get; set; } = 10;
    
    /// <summary>
    /// 명령 타임아웃 (초)
    /// </summary>
    public int CommandTimeout { get; set; } = 60;
    
    /// <summary>
    /// 재시도 횟수
    /// </summary>
    public int RetryCount { get; set; } = 3;
    
    /// <summary>
    /// 재시도 간격 (밀리초)
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}

