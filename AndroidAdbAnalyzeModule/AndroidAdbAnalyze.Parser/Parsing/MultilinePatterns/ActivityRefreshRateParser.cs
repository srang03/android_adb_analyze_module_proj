using AndroidAdbAnalyze.Parser.Core.Interfaces;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AndroidAdbAnalyze.Parser.Parsing.MultilinePatterns;

/// <summary>
/// Activity Log의 Refresh Rate 2줄 패턴 파서
/// 패턴:
///   Line 1: #15 &lt;&lt; 10-06 22:58:30.717 &gt;&gt;
///   Line 2:  [Min] Requested ( refreshRate=60.0 w=Window{...})
/// </summary>
public sealed class ActivityRefreshRateParser : IMultilinePatternParser
{
    private readonly ILogger? _logger;
    
    private static readonly Regex TimestampPattern = new(
        @"^#\d+.*<<.*(\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}).*>>",
        RegexOptions.Compiled);

    private static readonly Regex RefreshRatePattern = new(
        @"^\s+\[(Min|Max)\] Requested \( refreshRate=([\d.]+) w=Window\{([\w]+) u\d+ ([^/]+)/([^}]+)\}\)",
        RegexOptions.Compiled);

    /// <summary>
    /// <see cref="ActivityRefreshRateParser"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="logger">로깅을 위한 <see cref="ILogger"/> 인스턴스입니다. (선택 사항)</param>
    public ActivityRefreshRateParser(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string ParserId => "activity_refresh_rate_multiline";

    /// <inheritdoc />
    public string TargetSectionId => "activities";

    /// <inheritdoc />
    public int Priority => 1;

    /// <inheritdoc />
    public bool CanParse(LogSection section, int currentIndex)
    {
        if (section.Id != TargetSectionId)
            return false;

        if (currentIndex >= section.Lines.Count)
            return false;

        var currentLine = section.Lines[currentIndex];
        return TimestampPattern.IsMatch(currentLine);
    }

    /// <inheritdoc />
    public bool TryParse(
        LogSection section,
        int currentIndex,
        out ParsedLogEntry? entry,
        out int linesToSkip)
    {
        entry = null;
        linesToSkip = 0;

        // 최소 2줄 필요
        if (currentIndex + 1 >= section.Lines.Count)
            return false;

        var line1 = section.Lines[currentIndex];
        var line2 = section.Lines[currentIndex + 1];

        // Line 1: 타임스탬프 패턴 매칭
        var match1 = TimestampPattern.Match(line1);
        if (!match1.Success)
            return false;

        // Line 2: RefreshRate 패턴 매칭
        var match2 = RefreshRatePattern.Match(line2);
        if (!match2.Success)
            return false;

        // ParsedLogEntry 생성
        var timestampStr = match1.Groups[1].Value; // "10-06 22:58:30.717"
        var mode = match2.Groups[1].Value; // Min or Max
        var refreshRate = match2.Groups[2].Value;
        var windowId = match2.Groups[3].Value;
        var package = match2.Groups[4].Value;
        var activity = match2.Groups[5].Value;

        // Timestamp 파싱 (파서는 string 타임스탬프를 반환, 이후 정규화 단계에서 DeviceInfo 기준으로 변환)
        // 하지만 multiline parser는 DateTime?를 직접 생성해야 함
        // 임시로 null 처리하고, 실제 타임스탬프는 Fields에 저장
        entry = new ParsedLogEntry
        {
            Timestamp = null, // Multiline parser는 타임스탬프를 Fields에 저장하고 null로 설정
            EventType = "CAMERA_ACTIVITY_REFRESH",
            SectionId = section.Id,
            LineNumber = section.StartLine + currentIndex + 1,
            RawLine = $"{line1}\n{line2}",
            Fields = new Dictionary<string, object>
            {
                ["timestamp"] = timestampStr, // NormalizeEvents에서 DeviceInfo 기준으로 파싱
                ["mode"] = mode,
                ["refreshRate"] = double.Parse(refreshRate),
                ["package"] = package,
                ["activity"] = activity,
                ["windowId"] = windowId
            }
        };

        _logger?.LogDebug("Parsed CAMERA_ACTIVITY_REFRESH: Package={Package}, Activity={Activity}, RefreshRate={RefreshRate}, Mode={Mode}", 
            package, activity, refreshRate, mode);

        linesToSkip = 1; // 다음 1줄 스킵 (총 2줄 소비)
        return true;
    }
}

