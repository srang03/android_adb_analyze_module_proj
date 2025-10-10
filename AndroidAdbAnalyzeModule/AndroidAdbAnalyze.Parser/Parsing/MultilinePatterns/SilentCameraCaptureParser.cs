using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Interfaces;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AndroidAdbAnalyze.Parser.Parsing.MultilinePatterns;

/// <summary>
/// 무음 카메라 촬영 패턴 파서 (5줄 패턴)
/// 
/// 패턴 구조:
/// Line 1: #N 타임스탬프 (MM-dd HH:mm:ss.fff)
/// Line 2: [Min] Requested ( refreshRate=60.0 w=Window{...SilentCamera/...CameraActivity})
/// Line 3: (빈줄)
/// Line 4: #N-1 타임스탬프 (MM-dd HH:mm:ss.fff)
/// Line 5: [Min] Requested ( refreshRate=60.0 w=Window{... Toast})
/// 
/// 이 패턴은 무음 카메라 앱의 사진 촬영을 명확하게 나타냅니다.
/// </summary>
public sealed class SilentCameraCaptureParser : IMultilinePatternParser
{
    private readonly ILogger? _logger;

    // 타임스탬프 패턴: #15 << 10-06 22:58:30.717 >>
    private static readonly Regex TimestampPattern = new(
        @"^#\d+.*<<.*(\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}).*>>",
        RegexOptions.Compiled);

    // SilentCamera RefreshRate 패턴
    private static readonly Regex SilentCameraPattern = new(
        @"^\s+\[(Min|Max)\] Requested \( refreshRate=([\d.]+) w=Window\{([\w]+) u\d+ ([^/]+)/([^}]+)\}\)",
        RegexOptions.Compiled);

    // Toast RefreshRate 패턴 (슬래시 없음)
    private static readonly Regex ToastPattern = new(
        @"^\s+\[(Min|Max)\] Requested \( refreshRate=([\d.]+) w=Window\{([\w]+) u\d+ Toast\}\)",
        RegexOptions.Compiled);

    /// <summary>
    /// <see cref="SilentCameraCaptureParser"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="logger">로깅을 위한 <see cref="ILogger"/> 인스턴스입니다. (선택 사항)</param>
    public SilentCameraCaptureParser(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string ParserId => "silent_camera_capture_parser";

    /// <inheritdoc />
    public string TargetSectionId => "activities";

    /// <inheritdoc />
    public int Priority => 0; // ActivityRefreshRateParser(1)보다 우선순위 높음

    /// <inheritdoc />
    public bool CanParse(LogSection section, int currentIndex)
    {
        if (section.Id != TargetSectionId)
            return false;

        // 최소 5줄 필요 (빈줄 포함)
        if (currentIndex + 4 >= section.Lines.Count)
            return false;

        var line1 = section.Lines[currentIndex];     // #N timestamp
        var line2 = section.Lines[currentIndex + 1]; // SilentCamera RefreshRate
        var line3 = section.Lines[currentIndex + 2]; // 빈줄
        var line4 = section.Lines[currentIndex + 3]; // #N-1 timestamp
        var line5 = section.Lines[currentIndex + 4]; // Toast RefreshRate

        // Line 3은 빈줄이어야 함
        if (!string.IsNullOrWhiteSpace(line3))
            return false;

        // Line 1: 타임스탬프 패턴 확인
        if (!TimestampPattern.IsMatch(line1))
            return false;

        // Line 2: SilentCamera 패턴 확인
        var match2 = SilentCameraPattern.Match(line2);
        if (!match2.Success)
            return false;

        // Min만 파싱 (Max는 중복이므로 스킵)
        var mode = match2.Groups[1].Value;
        if (mode != "Min")
            return false;

        // SilentCamera 패키지 확인
        var package = match2.Groups[4].Value;
        if (!package.Contains("SilentCamera", StringComparison.OrdinalIgnoreCase))
            return false;

        // Line 4: 타임스탬프 패턴 확인
        if (!TimestampPattern.IsMatch(line4))
            return false;

        // Line 5: Toast 패턴 확인
        if (!ToastPattern.IsMatch(line5))
            return false;

        return true;
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

        // 최소 5줄 필요
        if (currentIndex + 4 >= section.Lines.Count)
            return false;

        var line1 = section.Lines[currentIndex];
        var line2 = section.Lines[currentIndex + 1];
        var line3 = section.Lines[currentIndex + 2];
        var line4 = section.Lines[currentIndex + 3];
        var line5 = section.Lines[currentIndex + 4];

        // 빈줄 확인
        if (!string.IsNullOrWhiteSpace(line3))
            return false;

        // Line 1: 타임스탬프 파싱 (SilentCamera 시각)
        var match1 = TimestampPattern.Match(line1);
        if (!match1.Success)
            return false;

        var captureTimestamp = match1.Groups[1].Value; // "10-06 22:58:30.717"

        // Line 2: SilentCamera 패턴 파싱
        var match2 = SilentCameraPattern.Match(line2);
        if (!match2.Success)
            return false;

        var mode = match2.Groups[1].Value;

        // Min만 파싱 (Max는 중복이므로 스킵)
        if (mode != "Min")
            return false;

        var refreshRate = match2.Groups[2].Value;
        var windowId = match2.Groups[3].Value;
        var package = match2.Groups[4].Value;
        var activity = match2.Groups[5].Value;

        // SilentCamera 확인
        if (!package.Contains("SilentCamera", StringComparison.OrdinalIgnoreCase))
            return false;

        // Line 4: Toast 타임스탬프
        var match4 = TimestampPattern.Match(line4);
        if (!match4.Success)
            return false;

        var toastTimestamp = match4.Groups[1].Value;

        // Line 5: Toast 패턴 확인
        var match5 = ToastPattern.Match(line5);
        if (!match5.Success)
            return false;

        // ParsedLogEntry 생성 (무음 카메라 촬영 확정)
        entry = new ParsedLogEntry
        {
            Timestamp = null, // Multiline parser는 Fields에 타임스탬프 저장
            EventType = LogEventTypes.SILENT_CAMERA_CAPTURE,
            SectionId = section.Id,
            LineNumber = section.StartLine + currentIndex + 1,
            RawLine = $"{line1}\n{line2}\n{line3}\n{line4}\n{line5}",
            Fields = new Dictionary<string, object>
            {
                ["timestamp"] = captureTimestamp,
                ["mode"] = mode,
                ["refreshRate"] = double.Parse(refreshRate),
                ["package"] = package,
                ["activity"] = activity,
                ["windowId"] = windowId,
                ["toastTimestamp"] = toastTimestamp,
                ["captureType"] = "silent_camera" // 촬영 타입 명시
            }
        };

        linesToSkip = 5; // 5줄 모두 소비

        _logger?.LogDebug(
            "Parsed SILENT_CAMERA_CAPTURE: Package={Package}, Activity={Activity}, CaptureTime={CaptureTime}",
            package, activity, captureTimestamp);

        return true;
    }
}

