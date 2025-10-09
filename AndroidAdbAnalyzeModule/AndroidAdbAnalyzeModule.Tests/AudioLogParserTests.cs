using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Audio Log 파싱 테스트
/// </summary>
public class AudioLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public AudioLogParserTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        _logger = loggerFactory.CreateLogger<AdbLogParser>();
        _configLogger = loggerFactory.CreateLogger<YamlConfigurationLoader>();
    }

    [Fact]
    public async Task ParseAudioLog_ShouldSuccessfully_ParseRealLogFile()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        _output.WriteLine($"Config path: {configPath}");
        _output.WriteLine($"Log path: {logPath}");
        _output.WriteLine($"Config exists: {File.Exists(configPath)}");
        _output.WriteLine($"Log exists: {File.Exists(logPath)}");

        // 설정 로드
        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        configuration.Should().NotBeNull();
        _output.WriteLine($"Configuration loaded: {configuration.Metadata.DisplayName}");

        // DeviceInfo 생성
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15",
            Manufacturer = "Samsung",
            Model = "SM-S928N"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true,
            Encoding = "utf-8",
            MaxFileSizeMB = 10
        };

        // Parser 생성
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue($"Parsing should succeed. Error: {result.ErrorMessage}");
        
        _output.WriteLine($"\n=== Parsing Result ===");
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Total Events: {result.Events.Count}");
        _output.WriteLine($"Total Lines: {result.Statistics.TotalLines}");
        _output.WriteLine($"Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"Skipped Lines: {result.Statistics.SkippedLines}");
        _output.WriteLine($"Error Lines: {result.Statistics.ErrorLines}");
        _output.WriteLine($"Elapsed Time: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");
        _output.WriteLine($"Success Rate: {result.Statistics.SuccessRate:P2}");

        // 이벤트가 최소한 생성되어야 함
        result.Events.Should().NotBeEmpty("Should parse at least some events from the log file");

        // EventType 통계
        _output.WriteLine($"\n=== Event Type Counts ===");
        foreach (var kvp in result.Statistics.EventTypeCounts)
        {
            _output.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        // Section 통계
        _output.WriteLine($"\n=== Section Line Counts ===");
        foreach (var kvp in result.Statistics.SectionLineCounts)
        {
            _output.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        // 에러 로깅
        if (result.Errors.Count > 0)
        {
            _output.WriteLine($"\n=== Parsing Errors (showing first 10) ===");
            foreach (var error in result.Errors.Take(10))
            {
                _output.WriteLine($"Line {error.LineNumber} [{error.Severity}]: {error.ErrorMessage}");
            }
        }

        // 샘플 이벤트 출력
        _output.WriteLine($"\n=== Sample Events (first 5) ===");
        foreach (var evt in result.Events.Take(5))
        {
            _output.WriteLine($"[{evt.EventType}] {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff} - Section: {evt.SourceSection}");
            _output.WriteLine($"  Attributes: {string.Join(", ", evt.Attributes.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldHandle_PlayerCreatedEvents()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();

        var playerCreatedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .ToList();

        _output.WriteLine($"\n=== PLAYER_CREATED Events: {playerCreatedEvents.Count} ===");

        playerCreatedEvents.Should().NotBeEmpty("Should find PLAYER_CREATED events in audio.txt");

        // Camera 관련 player 이벤트 확인
        var cameraPlayerEvents = playerCreatedEvents
            .Where(e => e.Attributes.ContainsKey("package") && 
                       e.Attributes["package"].ToString()!.Contains("camera"))
            .ToList();

        _output.WriteLine($"Camera PLAYER_CREATED Events: {cameraPlayerEvents.Count}");

        foreach (var evt in cameraPlayerEvents.Take(3))
        {
            _output.WriteLine($"  PIID: {evt.Attributes["piid"]}, Package: {evt.Attributes["package"]}, Tags: {evt.Attributes.GetValueOrDefault("tags", "N/A")}");
        }

        cameraPlayerEvents.Should().NotBeEmpty("Should find camera-related player events");
    }

    [Fact]
    public async Task ParseAudioLog_ShouldCorrectly_ParsePlayerAttributes()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions 
        { 
            DeviceInfo = deviceInfo,
            ConvertToUtc = true
        };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();

        // 첫 번째 PLAYER_CREATED 이벤트 가져오기
        var firstPlayerCreated = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        firstPlayerCreated.Should().NotBeNull("Should have at least one PLAYER_CREATED event");

        _output.WriteLine($"\n=== First PLAYER_CREATED Event Details ===");
        _output.WriteLine($"EventType: {firstPlayerCreated!.EventType}");
        _output.WriteLine($"Timestamp (UTC): {firstPlayerCreated.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"SourceSection: {firstPlayerCreated.SourceSection}");
        _output.WriteLine($"Attributes Count: {firstPlayerCreated.Attributes.Count}");
        
        foreach (var attr in firstPlayerCreated.Attributes)
        {
            _output.WriteLine($"  {attr.Key}: {attr.Value} ({attr.Value?.GetType().Name})");
        }

        // Attributes 검증
        firstPlayerCreated.Attributes.Should().ContainKey("piid");
        firstPlayerCreated.Attributes.Should().ContainKey("uid");
        firstPlayerCreated.Attributes.Should().ContainKey("pid");
        firstPlayerCreated.Attributes.Should().ContainKey("package");
        firstPlayerCreated.Attributes.Should().ContainKey("playerType");
        firstPlayerCreated.Attributes.Should().ContainKey("usage");
        firstPlayerCreated.Attributes.Should().ContainKey("contentType");
        firstPlayerCreated.Attributes.Should().ContainKey("flags");
        firstPlayerCreated.Attributes.Should().ContainKey("tags");

        // 타입 검증
        firstPlayerCreated.Attributes["piid"].Should().BeOfType<int>("piid should be parsed as int");
        firstPlayerCreated.Attributes["uid"].Should().BeOfType<int>("uid should be parsed as int");
        firstPlayerCreated.Attributes["pid"].Should().BeOfType<int>("pid should be parsed as int");
        firstPlayerCreated.Attributes["package"].Should().BeOfType<string>("package should be string");
        firstPlayerCreated.Attributes["playerType"].Should().BeOfType<string>("playerType should be string");
        firstPlayerCreated.Attributes["usage"].Should().BeOfType<string>("usage should be string");
        firstPlayerCreated.Attributes["contentType"].Should().BeOfType<string>("contentType should be string");
        firstPlayerCreated.Attributes["flags"].Should().BeOfType<int>("flags should be parsed as hex int");
        firstPlayerCreated.Attributes["tags"].Should().BeOfType<string>("tags should be string");

        // 값 검증
        ((int)firstPlayerCreated.Attributes["piid"]).Should().Be(367);
        ((int)firstPlayerCreated.Attributes["uid"]).Should().Be(10123);
        ((int)firstPlayerCreated.Attributes["pid"]).Should().Be(1902);
        ((string)firstPlayerCreated.Attributes["package"]).Should().Be("com.sec.android.app.camera");
        ((string)firstPlayerCreated.Attributes["playerType"]).Should().Contain("SoundPool");
        ((string)firstPlayerCreated.Attributes["usage"]).Should().Contain("USAGE_ASSISTANCE_SONIFICATION");
        ((string)firstPlayerCreated.Attributes["contentType"]).Should().Contain("CONTENT_TYPE_SONIFICATION");
        ((int)firstPlayerCreated.Attributes["flags"]).Should().Be(0x801);
        ((string)firstPlayerCreated.Attributes["tags"]).Should().Contain("CAMERA");

        // SourceSection 검증
        firstPlayerCreated.SourceSection.Should().Be("playback_activity");

        // EventType 검증
        firstPlayerCreated.EventType.Should().Be("PLAYER_CREATED");

        // Timestamp UTC 변환 검증
        firstPlayerCreated.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        firstPlayerCreated.Timestamp.Month.Should().Be(9);
        firstPlayerCreated.Timestamp.Day.Should().Be(4);
        firstPlayerCreated.Timestamp.Hour.Should().Be(6);
        firstPlayerCreated.Timestamp.Minute.Should().Be(8);
        firstPlayerCreated.Timestamp.Second.Should().Be(25);
        firstPlayerCreated.Timestamp.Millisecond.Should().Be(404);

        // DeviceInfo 검증
        firstPlayerCreated.DeviceInfo.Should().NotBeNull();
        firstPlayerCreated.DeviceInfo.AndroidVersion.Should().Be("15");
        firstPlayerCreated.DeviceInfo.TimeZone.Should().Be("Asia/Seoul");

        _output.WriteLine($"\n✓ All attributes validated successfully!");
    }

    [Fact]
    public async Task ParseAudioLog_ShouldCorrectly_ParseMultipleEventTypes()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();

        _output.WriteLine($"\n=== Event Type Distribution ===");
        var eventTypeGroups = result.Events.GroupBy(e => e.EventType).OrderByDescending(g => g.Count());
        
        foreach (var group in eventTypeGroups)
        {
            _output.WriteLine($"{group.Key}: {group.Count()} events");
        }

        // 다양한 EventType 검증
        var playerCreatedCount = result.Events.Count(e => e.EventType == LogEventTypes.PLAYER_CREATED);
        var playerEventCount = result.Events.Count(e => e.EventType == LogEventTypes.PLAYER_EVENT);
        var playerReleasedCount = result.Events.Count(e => e.EventType == LogEventTypes.PLAYER_RELEASED);

        _output.WriteLine($"\nPLAYER_CREATED: {playerCreatedCount}");
        _output.WriteLine($"PLAYER_EVENT: {playerEventCount}");
        _output.WriteLine($"PLAYER_RELEASED: {playerReleasedCount}");

        playerCreatedCount.Should().BeGreaterThan(0, "Should have PLAYER_CREATED events");
        playerEventCount.Should().BeGreaterThan(0, "Should have PLAYER_EVENT events");
        playerReleasedCount.Should().BeGreaterThan(0, "Should have PLAYER_RELEASED events");

        // PLAYER_EVENT 검증
        var firstPlayerEvent = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_EVENT)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        if (firstPlayerEvent != null)
        {
            _output.WriteLine($"\n=== First PLAYER_EVENT Details ===");
            firstPlayerEvent.Attributes.Should().ContainKey("piid");
            firstPlayerEvent.Attributes.Should().ContainKey("event");
            
            _output.WriteLine($"PIID: {firstPlayerEvent.Attributes["piid"]}");
            _output.WriteLine($"Event: {firstPlayerEvent.Attributes["event"]}");
            
            ((string)firstPlayerEvent.Attributes["event"]).Should().BeOneOf("started", "stopped", "paused");
        }

        // PLAYER_RELEASED 검증
        var firstPlayerReleased = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_RELEASED)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        if (firstPlayerReleased != null)
        {
            _output.WriteLine($"\n=== First PLAYER_RELEASED Details ===");
            firstPlayerReleased.Attributes.Should().ContainKey("piid");
            firstPlayerReleased.Attributes.Should().ContainKey("uid");
            
            _output.WriteLine($"PIID: {firstPlayerReleased.Attributes["piid"]}");
            _output.WriteLine($"UID: {firstPlayerReleased.Attributes["uid"]}");
        }

        _output.WriteLine($"\n✓ Multiple event types validated successfully!");
    }

    [Fact]
    public async Task ParseAudioLog_ShouldConvert_TimestampsToUtc()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();

        var firstEvent = result.Events.First();
        
        _output.WriteLine($"\n=== Timestamp Conversion Test ===");
        _output.WriteLine($"First Event Type: {firstEvent.EventType}");
        _output.WriteLine($"First Event Timestamp (UTC): {firstEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"First Event Timestamp Kind: {firstEvent.Timestamp.Kind}");

        firstEvent.Timestamp.Kind.Should().Be(DateTimeKind.Utc, "Timestamps should be converted to UTC");
    }

    [Fact]
    public async Task ParseAudioLog_WithIncompatibleAndroidVersion_ShouldFail()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "11"
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        Func<Task> act = async () => await parser.ParseAsync(logPath, options);

        // Assert
        await act.Should().ThrowAsync<Core.Exceptions.ConfigurationValidationException>()
            .WithMessage("*not supported*");

        _output.WriteLine("✓ Incompatible Android version correctly rejected");
    }

    [Fact]
    public async Task ParseAudioLog_Statistics_ShouldBeAccurate()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        result.Statistics.Should().NotBeNull();

        _output.WriteLine($"\n=== Statistics Validation ===");
        _output.WriteLine($"Total Lines: {result.Statistics.TotalLines}");
        _output.WriteLine($"Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"Skipped Lines: {result.Statistics.SkippedLines}");
        _output.WriteLine($"Error Lines: {result.Statistics.ErrorLines}");
        _output.WriteLine($"Success Rate: {result.Statistics.SuccessRate:P2}");

        // 통계 검증
        result.Statistics.TotalLines.Should().BeGreaterThan(0);
        result.Statistics.ParsedLines.Should().BeGreaterThanOrEqualTo(0);
        result.Statistics.SkippedLines.Should().BeGreaterThanOrEqualTo(0);
        result.Statistics.ElapsedTime.Should().BePositive();

        // EventTypeCounts 검증
        result.Statistics.EventTypeCounts.Should().NotBeEmpty();
        var eventTypeSum = result.Statistics.EventTypeCounts.Values.Sum();
        eventTypeSum.Should().Be(result.Events.Count, "EventTypeCounts sum should match total events");

        _output.WriteLine($"\nEvent Types:");
        foreach (var (eventType, count) in result.Statistics.EventTypeCounts)
        {
            _output.WriteLine($"  {eventType}: {count}");
            count.Should().BeGreaterThan(0, $"EventType {eventType} should have at least one event");
        }
    }
}

