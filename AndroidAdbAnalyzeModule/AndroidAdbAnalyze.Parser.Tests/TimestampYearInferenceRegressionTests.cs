using System.Globalization;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Interfaces;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using AndroidAdbAnalyze.Parser.Parsing.MultilinePatterns;
using AndroidAdbAnalyze.Parser.Preprocessing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AndroidAdbAnalyze.Parser.Tests;

/// <summary>
/// 연도 없는 타임스탬프의 연도 추론 회귀 테스트
/// </summary>
/// <remarks>
/// RegexLineParser로 파싱된 연도 없는 타임스탬프(audio, vibrator, media.camera 등)가
/// 분석 실행 시점의 연도가 아니라 DeviceInfo.CurrentTime / StartTime~EndTime 기준으로 연도를 추론하는지 검증합니다.
/// </remarks>
public class TimestampYearInferenceRegressionTests
{
    private const string NoYearFormat = "MM-dd HH:mm:ss.fff";

    private static string TestDataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    private static async Task<ParsingResult> ParseAsync(string configFile, string logFile, LogParsingOptions options)
    {
        var configPath = TestDataPath(configFile);
        var configuration = await new YamlConfigurationLoader(configPath, NullLogger<YamlConfigurationLoader>.Instance)
            .LoadAsync(configPath);
        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);
        return await parser.ParseAsync(TestDataPath(logFile), options);
    }

    private static ParsedLogEntry NoYearEntry(DateTime parsedTime) => new()
    {
        EventType = "TEST",
        Timestamp = parsedTime,
        TimestampFormat = NoYearFormat
    };

    [Fact]
    public async Task LineParser_NoYearTimestamp_ShouldUseDeviceCurrentTimeYear_NotExecutionYear()
    {
        // Arrange: 2025년에 수집된 로그 (실행 연도와 무관해야 함)
        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = new DateTime(2025, 9, 9, 16, 0, 0) },
            ConvertToUtc = false
        };

        // Act
        var result = await ParseAsync("adb_vibrator_config.yaml", "vibrator_manager.txt", options);

        // Assert
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();
        result.Events.Should().OnlyContain(e => e.Timestamp.Year == 2025);
        result.Events.Should().OnlyContain(e =>
            !(e.Attributes["timestamp"] is DateTime) || ((DateTime)e.Attributes["timestamp"]).Year == 2025,
            "Attributes의 원본 타임스탬프도 이벤트 Timestamp와 같은 연도여야 함");
    }

    [Fact]
    public async Task LineParser_NoYearTimestamp_WithTimeRange_ShouldKeepEventsInRange()
    {
        // Arrange: 2025년 범위로 분석 (실행 연도가 달라도 범위 내 이벤트가 필터링되면 안 됨)
        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = DateTime.Now },
            StartTime = new DateTime(2025, 9, 7, 18, 0, 0),
            EndTime = new DateTime(2025, 9, 7, 19, 0, 0),
            ConvertToUtc = false
        };

        // Act
        var result = await ParseAsync("adb_vibrator_config.yaml", "vibrator_manager.txt", options);

        // Assert
        result.Events.Should().NotBeEmpty();
        result.Events.Should().OnlyContain(e =>
            e.Timestamp >= options.StartTime && e.Timestamp <= options.EndTime);
    }

    [Fact]
    public async Task MultilineParser_StartTimeOnly_ShouldNotDropEventsAfterStartTime()
    {
        // Arrange: StartTime만 지정 (하한값) → StartTime 이후 이벤트는 유지되어야 함
        var startTime = new DateTime(2025, 9, 5);
        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = new DateTime(2025, 9, 9, 16, 0, 0) },
            StartTime = startTime,
            ConvertToUtc = false
        };

        // Act
        var result = await ParseAsync("adb_activity_config.yaml", "activity.txt", options);

        // Assert
        var refreshEvents = result.Events.Where(e => e.EventType == "CAMERA_ACTIVITY_REFRESH").ToList();
        refreshEvents.Should().NotBeEmpty();
        refreshEvents.Should().OnlyContain(e => e.Timestamp >= startTime && e.Timestamp.Year == 2025);
    }

    [Theory]
    [InlineData(12, 31, 2025)]
    [InlineData(1, 1, 2026)]
    public void NormalizeLogEntry_TimeRangeAcrossYearBoundary_ShouldInferYearWithinRange(int month, int day, int expectedYear)
    {
        // Arrange: 파싱 시점 연도(임의)가 채워진 연도 없는 타임스탬프
        var deviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = new DateTime(2026, 1, 2) };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false,
            new DateTime(2025, 12, 30), new DateTime(2026, 1, 2));
        var entry = NoYearEntry(new DateTime(2031, month, day, 12, 0, 0));

        // Act
        var result = normalizer.NormalizeLogEntry(entry);

        // Assert
        result.Should().Be(new DateTime(expectedYear, month, day, 12, 0, 0));
    }

    [Fact]
    public void NormalizeLogEntry_UtcTimeRange_ShouldInferYearInDeviceLocalTime()
    {
        // Arrange: UTC 범위 2025-12-31 16:00Z ~ 2026-01-01 16:00Z = KST 2026-01-01 01:00 ~ 2026-01-02 01:00
        var deviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = new DateTime(2026, 1, 2) };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true,
            new DateTime(2025, 12, 31, 16, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc));
        var entry = NoYearEntry(new DateTime(2031, 1, 1, 9, 0, 0)); // KST 01-01 09:00

        // Act
        var result = normalizer.NormalizeLogEntry(entry);

        // Assert: 2026-01-01 09:00 KST = 2026-01-01 00:00 UTC
        result.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0));
    }

    [Fact]
    public void NormalizeLogEntry_FormatWithYear_ShouldKeepParsedYear()
    {
        // Arrange
        var deviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = new DateTime(2026, 1, 2) };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);
        var entry = new ParsedLogEntry
        {
            EventType = "TEST",
            Timestamp = new DateTime(2024, 5, 1, 10, 0, 0),
            TimestampFormat = "yyyy-MM-dd HH:mm:ss"
        };

        // Act
        var result = normalizer.NormalizeLogEntry(entry);

        // Assert
        result.Should().Be(new DateTime(2024, 5, 1, 10, 0, 0));
    }

    [Fact]
    public async Task ParseAsync_MaxFileSizeOver2GB_ShouldNotOverflow()
    {
        // Arrange: int 곱셈 오버플로 시 음수 크기 제한으로 모든 파일이 거부되었음
        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = new DateTime(2025, 9, 9, 16, 0, 0) },
            MaxFileSizeMB = 4096
        };

        // Act
        var result = await ParseAsync("adb_vibrator_config.yaml", "vibrator_manager.txt", options);

        // Assert
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public void ActivityRefreshRateParser_ShouldParseRefreshRate_IndependentOfCurrentCulture()
    {
        // Arrange: 소수점 구분자가 ','인 문화권
        var section = new LogSection
        {
            Id = "activities",
            Lines = new List<string>
            {
                "#15 << 09-09 15:07:21.822 >>",
                " [Min] Requested ( refreshRate=60.0 w=Window{b1ec4dd u0 com.sec.android.app.camera/com.sec.android.app.camera.Camera})"
            }
        };
        var parser = new ActivityRefreshRateParser();
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act
            var parsed = parser.TryParse(section, 0, out var entry, out _);

            // Assert
            parsed.Should().BeTrue();
            entry!.Fields["refreshRate"].Should().Be(60.0);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
