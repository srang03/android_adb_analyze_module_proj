namespace AndroidAdbAnalyzeModule.Configuration.Models;

/// <summary>
/// 로그 파싱 설정 (YAML 파일 루트)
/// </summary>
public sealed class LogConfiguration
{
    /// <summary>
    /// 설정 스키마 버전
    /// </summary>
    public string ConfigSchemaVersion { get; init; } = string.Empty;

    /// <summary>
    /// 메타데이터
    /// </summary>
    public ConfigMetadata Metadata { get; init; } = new();

    /// <summary>
    /// 파일 매칭 패턴
    /// </summary>
    public List<string> FilePatterns { get; init; } = new();

    /// <summary>
    /// 전역 파싱 설정
    /// </summary>
    public GlobalSettings GlobalSettings { get; init; } = new();

    /// <summary>
    /// 성능 설정
    /// </summary>
    public PerformanceSettings Performance { get; init; } = new();

    /// <summary>
    /// 에러 처리 설정
    /// </summary>
    public ErrorHandlingSettings ErrorHandling { get; init; } = new();

    /// <summary>
    /// 섹션 정의 목록
    /// </summary>
    public List<SectionConfig> Sections { get; init; } = new();

    /// <summary>
    /// 파서 정의 목록
    /// </summary>
    public List<ParserConfig> Parsers { get; init; } = new();


    /// <summary>
    /// 전처리 설정 (선택사항, Phase 4에서 사용)
    /// </summary>
    public PreprocessingConfig? Preprocessing { get; init; }

    /// <summary>
    /// 로깅 설정 (선택사항)
    /// </summary>
    public LoggingConfig? Logging { get; init; }
}

/// <summary>
/// 설정 메타데이터
/// </summary>
public sealed class ConfigMetadata
{
    /// <summary>
    /// 로그 타입 (예: "adb_audio", "adb_battery")
    /// </summary>
    public string LogType { get; init; } = string.Empty;

    /// <summary>
    /// 지원하는 안드로이드 버전 목록 (예: ["11", "12", "14", "15"] 또는 ["*"] for all)
    /// </summary>
    public List<string> SupportedVersions { get; init; } = new();

    /// <summary>
    /// 표시 이름
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 설명
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 작성자 (선택사항)
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// 생성일 (선택사항)
    /// </summary>
    public string? CreatedDate { get; init; }
}

/// <summary>
/// 전역 파싱 설정
/// </summary>
public sealed class GlobalSettings
{
    /// <summary>
    /// 파일 인코딩
    /// </summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary>
    /// 빈 라인 스킵 여부
    /// </summary>
    public bool SkipEmptyLines { get; init; } = true;

    /// <summary>
    /// 주석 스킵 여부
    /// </summary>
    public bool SkipComments { get; init; } = false;

    /// <summary>
    /// 주석 접두사
    /// </summary>
    public string CommentPrefix { get; init; } = "#";

    /// <summary>
    /// 시계열 순서 (ascending, descending, none)
    /// </summary>
    public string TimeSeriesOrder { get; init; } = "ascending";

    /// <summary>
    /// 타임스탬프 포맷
    /// </summary>
    public string TimestampFormat { get; init; } = "MM-dd HH:mm:ss:fff";
}

/// <summary>
/// 성능 설정
/// </summary>
public sealed class PerformanceSettings
{
    /// <summary>
    /// 최대 파일 크기 (MB)
    /// </summary>
    public int MaxFileSizeMB { get; init; } = 500;

    /// <summary>
    /// 타임아웃 (초)
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// 버퍼 크기 (KB)
    /// </summary>
    public int BufferSizeKB { get; init; } = 64;

    /// <summary>
    /// 스트리밍 파서 사용 여부 (추후 구현)
    /// </summary>
    public bool UseStreamingParser { get; init; } = false;

    /// <summary>
    /// 병렬 처리 여부 (추후 구현)
    /// </summary>
    public bool ParallelProcessing { get; init; } = false;
}

/// <summary>
/// 에러 처리 설정
/// </summary>
public sealed class ErrorHandlingSettings
{
    /// <summary>
    /// 잘못된 라인 처리 방법 (skip, abort, log)
    /// </summary>
    public string OnInvalidLine { get; init; } = "skip";

    /// <summary>
    /// 타임스탬프 누락 시 처리 방법
    /// </summary>
    public string OnMissingTimestamp { get; init; } = "skip";

    /// <summary>
    /// 파싱 에러 시 처리 방법
    /// </summary>
    public string OnParsingError { get; init; } = "log";

    /// <summary>
    /// 스키마 검증 여부
    /// </summary>
    public bool ValidateSchema { get; init; } = true;
}

/// <summary>
/// 섹션 설정 (YAML용)
/// </summary>
public sealed class SectionConfig
{
    /// <summary>
    /// 섹션 ID
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 섹션 이름
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 활성화 여부
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 시작 마커
    /// </summary>
    public string StartMarker { get; init; } = string.Empty;

    /// <summary>
    /// 종료 마커
    /// </summary>
    public string EndMarker { get; init; } = string.Empty;

    /// <summary>
    /// 마커 타입 (text, regex, lineNumber)
    /// </summary>
    public string MarkerType { get; init; } = "text";
}

/// <summary>
/// 파서 설정
/// </summary>
public sealed class ParserConfig
{
    /// <summary>
    /// 파서 ID
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 파서 이름
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 활성화 여부
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 대상 섹션 ID 목록
    /// </summary>
    public List<string> TargetSections { get; init; } = new();

    /// <summary>
    /// 우선순위 (낮을수록 먼저 실행)
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 라인 패턴 목록
    /// </summary>
    public List<LinePatternConfig> LinePatterns { get; init; } = new();
}

/// <summary>
/// 라인 패턴 설정
/// </summary>
public sealed class LinePatternConfig
{
    /// <summary>
    /// 패턴 ID
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 패턴 이름
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 정규식 패턴
    /// </summary>
    public string Regex { get; init; } = string.Empty;

    /// <summary>
    /// 필드 정의 (그룹 번호 → 필드 정의)
    /// </summary>
    public Dictionary<string, FieldDefinition> Fields { get; init; } = new();

    /// <summary>
    /// 이벤트 타입
    /// </summary>
    public string EventType { get; init; } = string.Empty;
}

/// <summary>
/// 필드 정의
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>
    /// Regex 그룹 번호
    /// </summary>
    public int Group { get; init; }

    /// <summary>
    /// 필드 타입 (datetime, int, string, hex 등)
    /// </summary>
    public string Type { get; init; } = "string";

    /// <summary>
    /// 포맷 (datetime용)
    /// </summary>
    public string? Format { get; init; }
}

/// <summary>
/// 전처리 설정 (Phase 4에서 구현)
/// </summary>
public sealed class PreprocessingConfig
{
    // 전처리 관련 설정
}

/// <summary>
/// 로깅 설정
/// </summary>
public sealed class LoggingConfig
{
    /// <summary>
    /// 로그 레벨 (debug, info, warn, error)
    /// </summary>
    public string Level { get; init; } = "info";

    /// <summary>
    /// 파싱된 이벤트 로깅 여부
    /// </summary>
    public bool LogParsedEvents { get; init; } = false;

    /// <summary>
    /// 스킵된 라인 로깅 여부
    /// </summary>
    public bool LogSkippedLines { get; init; } = true;

    /// <summary>
    /// 성능 메트릭 로깅 여부
    /// </summary>
    public bool LogPerformanceMetrics { get; init; } = true;
}

