namespace AndroidAdbAnalyze.Console.Executor.Configuration;

/// <summary>
/// 로그 수집 설정 (appsettings.json의 "LogCollection" 섹션)
/// </summary>
public sealed class LogCollectionConfiguration
{
    /// <summary>
    /// 로그 출력 디렉토리
    /// </summary>
    public string OutputDirectory { get; set; } = "./logs";
    
    /// <summary>
    /// 수집할 로그 목록
    /// </summary>
    public List<LogDefinition> Logs { get; set; } = new();
}

/// <summary>
/// 개별 로그 정의
/// </summary>
public sealed class LogDefinition
{
    /// <summary>
    /// 로그 이름 (예: "activity", "audio")
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// dumpsys 서비스 이름 (예: "activity", "media.camera")
    /// </summary>
    public required string DumpsysService { get; set; }
    
    /// <summary>
    /// 출력 파일명 (예: "activity.log")
    /// </summary>
    public required string OutputFileName { get; set; }
    
    /// <summary>
    /// Parser 설정 파일 경로 (예: "Configs/Parser/adb_activity_config.yaml")
    /// </summary>
    public required string ParserConfig { get; set; }
    
    /// <summary>
    /// 로그 수집 타임아웃 (초, null이면 기본값 사용)
    /// </summary>
    public int? Timeout { get; set; }
    
    /// <summary>
    /// 필수 로그 여부 (필수 로그 수집 실패 시 전체 실패)
    /// </summary>
    public bool Required { get; set; } = true;
    
    /// <summary>
    /// 비고 (선택적)
    /// </summary>
    public string? Notes { get; set; }
}

