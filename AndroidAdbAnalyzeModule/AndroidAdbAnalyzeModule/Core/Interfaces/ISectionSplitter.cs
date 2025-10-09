namespace AndroidAdbAnalyzeModule.Core.Interfaces;

/// <summary>
/// 로그 섹션 분리기 인터페이스
/// </summary>
public interface ISectionSplitter
{
    /// <summary>
    /// 로그 파일을 섹션별로 분리
    /// </summary>
    /// <param name="logFilePath">로그 파일 경로</param>
    /// <param name="sectionDefinitions">섹션 정의 목록</param>
    /// <returns>섹션별 로그 내용</returns>
    Task<IReadOnlyDictionary<string, LogSection>> SplitAsync(
        string logFilePath,
        IEnumerable<SectionDefinition> sectionDefinitions);
}

/// <summary>
/// 섹션 정의
/// </summary>
public sealed class SectionDefinition
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
/// 로그 섹션
/// </summary>
public sealed class LogSection
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
    /// 시작 라인 번호
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// 종료 라인 번호
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// 섹션 라인 목록
    /// </summary>
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
}

