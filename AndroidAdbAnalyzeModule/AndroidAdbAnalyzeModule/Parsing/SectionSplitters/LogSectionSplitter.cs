using AndroidAdbAnalyzeModule.Core.Interfaces;
using AndroidAdbAnalyzeModule.Core.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AndroidAdbAnalyzeModule.Parsing.SectionSplitters;

/// <summary>
/// 로그 섹션 분리기
/// </summary>
public sealed class LogSectionSplitter : ISectionSplitter
{
    private readonly ILogger? _logger;

    /// <summary>
    /// LogSectionSplitter 생성자
    /// </summary>
    /// <param name="logger">로거 (선택사항)</param>
    public LogSectionSplitter(ILogger? logger = null)
    {
        _logger = logger;
    }
    /// <summary>
    /// 로그 파일을 섹션별로 분리
    /// </summary>
    public async Task<IReadOnlyDictionary<string, LogSection>> SplitAsync(
        string logFilePath,
        IEnumerable<SectionDefinition> sectionDefinitions)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
            throw new ArgumentException("Log file path cannot be null or empty", nameof(logFilePath));

        if (!File.Exists(logFilePath))
            throw new FileNotFoundException($"Log file not found: {logFilePath}", logFilePath);

        if (sectionDefinitions == null)
            throw new ArgumentNullException(nameof(sectionDefinitions));

        var definitions = sectionDefinitions.Where(d => d.Enabled).ToList();
        if (definitions.Count == 0)
            return new Dictionary<string, LogSection>();

        _logger?.LogDebug("Reading log file: {FilePath}", logFilePath);
        
        try
        {
            // 파일 읽기
            var lines = await File.ReadAllLinesAsync(logFilePath);
            _logger?.LogInformation("Read {LineCount} lines from log file", lines.Length);
            
            // 섹션 분리
            _logger?.LogDebug("Splitting {LineCount} lines into {DefinitionCount} sections", 
                lines.Length, definitions.Count);
            var sections = SplitSections(lines, definitions);
            _logger?.LogInformation("Successfully split log into {SectionCount} sections", sections.Count);
            
            return sections;
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            _logger?.LogError(ex, "Failed to split log file into sections: {FilePath}", logFilePath);
            throw new ParsingException($"Failed to split log file into sections: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 라인 배열을 섹션별로 분리
    /// </summary>
    private IReadOnlyDictionary<string, LogSection> SplitSections(
        string[] lines,
        List<SectionDefinition> definitions)
    {
        var result = new Dictionary<string, LogSection>();

        foreach (var definition in definitions)
        {
            var section = FindSection(lines, definition);
            if (section != null)
            {
                _logger?.LogDebug("Found section {SectionId}: lines {StartLine}-{EndLine} ({LineCount} lines)",
                    section.Id, section.StartLine, section.EndLine, section.Lines.Count);
                result[definition.Id] = section;
            }
            else
            {
                _logger?.LogWarning("Section {SectionId} not found or is empty", definition.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// 특정 섹션 찾기
    /// </summary>
    private LogSection? FindSection(string[] lines, SectionDefinition definition)
    {
        int startLine = -1;
        int endLine = -1;

        // 시작 마커 찾기
        for (int i = 0; i < lines.Length; i++)
        {
            if (IsMarkerMatch(lines[i], definition.StartMarker, definition.MarkerType))
            {
                startLine = i;
                break;
            }
        }

        if (startLine == -1)
            return null; // 시작 마커를 찾지 못함

        // 종료 마커 찾기 (시작 마커 이후부터)
        for (int i = startLine + 1; i < lines.Length; i++)
        {
            if (IsMarkerMatch(lines[i], definition.EndMarker, definition.MarkerType))
            {
                endLine = i;
                break;
            }
        }

        if (endLine == -1)
            endLine = lines.Length - 1; // 종료 마커가 없으면 파일 끝까지

        // 섹션 라인 추출 (시작 마커는 포함, 종료 마커는 제외)
        var sectionLines = new List<string>();
        for (int i = startLine; i < endLine; i++)
        {
            sectionLines.Add(lines[i]);
        }

        return new LogSection
        {
            Id = definition.Id,
            Name = definition.Name,
            StartLine = startLine,
            EndLine = endLine,
            Lines = sectionLines
        };
    }

    /// <summary>
    /// 마커 매칭 확인
    /// </summary>
    private bool IsMarkerMatch(string line, string marker, string markerType)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        return markerType.ToLower() switch
        {
            "text" => line.Contains(marker, StringComparison.Ordinal),
            "regex" => Regex.IsMatch(line, marker, RegexOptions.None, TimeSpan.FromSeconds(1)),
            "linenumber" => false, // TODO: Phase 2에서는 지원하지 않음
            _ => false
        };
    }
}

