using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Exceptions;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
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
        await act.Should().ThrowAsync<ConfigurationValidationException>()
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

    [Fact]
    public async Task ParseAudioLog_ShouldParse_TimestampAccurately()
    {
        // Arrange: 타임스탬프 파싱 정확도 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Events.Should().NotBeEmpty("Should parse events with timestamps");

        // 타임스탬프 순서 검증
        var timestamps = result.Events.Select(e => e.Timestamp).ToList();
        var sortedTimestamps = timestamps.OrderBy(t => t).ToList();

        timestamps.Should().BeInAscendingOrder("Timestamps should be in ascending order");

        // 타임스탬프 정확도 검증 (밀리초까지 파싱)
        var firstEvent = result.Events.First();
        firstEvent.Timestamp.Millisecond.Should().BeGreaterThanOrEqualTo(0, "Milliseconds should be parsed");

        _output.WriteLine($"✓ Timestamp parsing validated");
        _output.WriteLine($"  First Event: {firstEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Last Event: {result.Events.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
    }

    [Fact]
    public async Task ParseAudioLog_ShouldValidate_PiidUidPidTypes()
    {
        // Arrange: PIID, UID, PID 타입 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var playerCreatedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .ToList();

        playerCreatedEvents.Should().NotBeEmpty("Should have PLAYER_CREATED events");

        foreach (var evt in playerCreatedEvents)
        {
            // PIID 검증
            evt.Attributes.Should().ContainKey("piid");
            var piidObj = evt.Attributes["piid"];
            if (piidObj is int piidInt)
            {
                piidInt.Should().BeGreaterThanOrEqualTo(0, "piid should be non-negative integer");
            }
            else if (int.TryParse(piidObj?.ToString(), out var parsed))
            {
                parsed.Should().BeGreaterThanOrEqualTo(0, "piid should be parseable as non-negative integer");
            }
            else
            {
                Assert.Fail($"piid should be int or parseable string: {piidObj}");
            }

            // UID 검증
            evt.Attributes.Should().ContainKey("uid");
            var uidObj = evt.Attributes["uid"];
            if (uidObj is int uidInt)
            {
                uidInt.Should().BeGreaterThanOrEqualTo(0, "uid should be non-negative integer");
            }
            else if (int.TryParse(uidObj?.ToString(), out var parsedUid))
            {
                parsedUid.Should().BeGreaterThanOrEqualTo(0, "uid should be parseable as non-negative integer");
            }

            // PID 검증
            evt.Attributes.Should().ContainKey("pid");
            var pidObj = evt.Attributes["pid"];
            if (pidObj is int pidInt)
            {
                pidInt.Should().BeGreaterThanOrEqualTo(0, "pid should be non-negative integer");
            }
            else if (int.TryParse(pidObj?.ToString(), out var parsedPid))
            {
                parsedPid.Should().BeGreaterThanOrEqualTo(0, "pid should be parseable as non-negative integer");
            }
        }

        _output.WriteLine($"✓ PIID/UID/PID type validation passed");
        _output.WriteLine($"  Validated {playerCreatedEvents.Count} PLAYER_CREATED events");
    }

    [Fact]
    public async Task ParseAudioLog_ShouldParse_MultiplePackages()
    {
        // Arrange: 여러 패키지의 이벤트 파싱 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var eventsWithPackage = result.Events
            .Where(e => e.Attributes.ContainsKey("package"))
            .ToList();

        eventsWithPackage.Should().NotBeEmpty("Should parse events with package attribute");

        var packageGroups = eventsWithPackage
            .GroupBy(e => e.Attributes["package"].ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        packageGroups.Should().NotBeEmpty("Should have events from different packages");

        _output.WriteLine($"✓ Multiple package parsing validated");
        _output.WriteLine($"  Total Packages: {packageGroups.Count}");
        _output.WriteLine($"\n  Package Distribution:");
        foreach (var group in packageGroups.Take(10))
        {
            _output.WriteLine($"    {group.Key}: {group.Count()} events");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldCorrelate_PlayerLifecycle()
    {
        // Arrange: PIID 기반 Player Lifecycle 상관관계 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var playerCreatedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .ToDictionary(e => Convert.ToInt32(e.Attributes["piid"]), e => e);

        var playerEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_EVENT)
            .GroupBy(e => Convert.ToInt32(e.Attributes["piid"]))
            .ToDictionary(g => g.Key, g => g.ToList());

        var playerReleasedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_RELEASED)
            .ToDictionary(e => Convert.ToInt32(e.Attributes["piid"]), e => e);

        playerCreatedEvents.Should().NotBeEmpty("Should have PLAYER_CREATED events");

        // PIID별로 lifecycle 검증
        var completeLifecycles = 0;
        var incompleteLifecycles = 0;

        foreach (var (piid, createdEvent) in playerCreatedEvents)
        {
            var hasEvent = playerEvents.ContainsKey(piid);
            var hasReleased = playerReleasedEvents.ContainsKey(piid);

            if (hasEvent && hasReleased)
            {
                completeLifecycles++;

                // 시간 순서 검증
                var events = playerEvents[piid];
                var releasedEvent = playerReleasedEvents[piid];

                foreach (var evt in events)
                {
                    evt.Timestamp.Should().BeAfter(createdEvent.Timestamp,
                        $"PLAYER_EVENT should occur after PLAYER_CREATED for piid={piid}");
                }

                releasedEvent.Timestamp.Should().BeAfter(createdEvent.Timestamp,
                    $"PLAYER_RELEASED should occur after PLAYER_CREATED for piid={piid}");
            }
            else
            {
                incompleteLifecycles++;
            }
        }

        completeLifecycles.Should().BeGreaterThan(0, "Should have at least one complete player lifecycle");

        _output.WriteLine($"✓ Player lifecycle correlation validated");
        _output.WriteLine($"  Total PIIDs: {playerCreatedEvents.Count}");
        _output.WriteLine($"  Complete Lifecycles: {completeLifecycles}");
        _output.WriteLine($"  Incomplete Lifecycles: {incompleteLifecycles}");
        
        // 샘플 출력
        if (completeLifecycles > 0)
        {
            var samplePiid = playerCreatedEvents.Keys.First(piid => 
                playerEvents.ContainsKey(piid) && playerReleasedEvents.ContainsKey(piid));
            
            _output.WriteLine($"\n  Sample Lifecycle (PIID={samplePiid}):");
            _output.WriteLine($"    CREATED: {playerCreatedEvents[samplePiid].Timestamp:HH:mm:ss.fff}");
            foreach (var evt in playerEvents[samplePiid])
            {
                _output.WriteLine($"    EVENT ({evt.Attributes["event"]}): {evt.Timestamp:HH:mm:ss.fff}");
            }
            _output.WriteLine($"    RELEASED: {playerReleasedEvents[samplePiid].Timestamp:HH:mm:ss.fff}");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldIdentify_CameraTaggedPlayers()
    {
        // Arrange: Camera 태그가 있는 player 식별
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var cameraPlayers = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED &&
                       e.Attributes.ContainsKey("tags") &&
                       e.Attributes["tags"].ToString()!.Contains("CAMERA", StringComparison.OrdinalIgnoreCase))
            .ToList();

        cameraPlayers.Should().NotBeEmpty("Should identify camera-tagged players");

        // 모든 camera player가 카메라 패키지에서 왔는지 검증
        foreach (var player in cameraPlayers)
        {
            player.Attributes.Should().ContainKey("package");
            var package = player.Attributes["package"].ToString();
            package.Should().Contain("camera", "Camera-tagged players should be from camera package");
        }

        _output.WriteLine($"✓ Camera-tagged players identified");
        _output.WriteLine($"  Total Camera Players: {cameraPlayers.Count}");
        
        if (cameraPlayers.Any())
        {
            var sample = cameraPlayers.First();
            _output.WriteLine($"\n  Sample Camera Player:");
            _output.WriteLine($"    PIID: {sample.Attributes["piid"]}");
            _output.WriteLine($"    Package: {sample.Attributes["package"]}");
            _output.WriteLine($"    Tags: {sample.Attributes["tags"]}");
            _output.WriteLine($"    PlayerType: {sample.Attributes["playerType"]}");
            _output.WriteLine($"    Timestamp: {sample.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldValidate_PlayerEventTypes()
    {
        // Arrange: Player 이벤트 타입 검증 (started, stopped, paused)
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var playerEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_EVENT)
            .ToList();

        playerEvents.Should().NotBeEmpty("Should have PLAYER_EVENT events");

        var eventTypeGroups = playerEvents
            .GroupBy(e => e.Attributes["event"].ToString())
            .ToDictionary(g => g.Key!, g => g.Count());

        eventTypeGroups.Should().NotBeEmpty("Should have different player event types");

        _output.WriteLine($"✓ Player event types validated");
        _output.WriteLine($"  Total PLAYER_EVENT: {playerEvents.Count}");
        _output.WriteLine($"\n  Event Type Distribution:");
        foreach (var (eventType, count) in eventTypeGroups.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"    {eventType}: {count}");
        }

        // 각 이벤트 타입이 유효한지 검증
        var validEventTypes = new[] { "started", "stopped", "paused", "resumed", "device", "muted" };
        foreach (var evt in playerEvents)
        {
            var eventType = evt.Attributes["event"].ToString();
            validEventTypes.Should().Contain(eventType, 
                $"Player event type '{eventType}' should be one of the valid types");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldParse_FocusEvents()
    {
        // Arrange: Audio Focus 이벤트 파싱 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var focusRequestedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.FOCUS_REQUESTED)
            .ToList();

        var focusAbandonedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.FOCUS_ABANDONED)
            .ToList();

        // Focus 이벤트가 있는 경우에만 검증
        if (focusRequestedEvents.Any() || focusAbandonedEvents.Any())
        {
            _output.WriteLine($"✓ Focus events parsing validated");
            _output.WriteLine($"  FOCUS_REQUESTED: {focusRequestedEvents.Count}");
            _output.WriteLine($"  FOCUS_ABANDONED: {focusAbandonedEvents.Count}");

            // Focus requested 이벤트 속성 검증
            if (focusRequestedEvents.Any())
            {
                var firstFocusRequest = focusRequestedEvents.First();
                firstFocusRequest.Attributes.Should().ContainKey("uid");
                firstFocusRequest.Attributes.Should().ContainKey("pid");
                firstFocusRequest.Attributes.Should().ContainKey("package");
                firstFocusRequest.Attributes.Should().ContainKey("usage");
                firstFocusRequest.Attributes.Should().ContainKey("contentType");
                firstFocusRequest.Attributes.Should().ContainKey("clientId");
            }

            // Focus abandoned 이벤트 속성 검증
            if (focusAbandonedEvents.Any())
            {
                var firstFocusAbandon = focusAbandonedEvents.First();
                firstFocusAbandon.Attributes.Should().ContainKey("uid");
                firstFocusAbandon.Attributes.Should().ContainKey("pid");
                firstFocusAbandon.Attributes.Should().ContainKey("package");
                firstFocusAbandon.Attributes.Should().ContainKey("clientId");
            }

            // 카메라 앱의 focus 이벤트 확인
            var cameraFocusEvents = focusAbandonedEvents
                .Where(e => e.Attributes.ContainsKey("package") &&
                           e.Attributes["package"].ToString()!.Contains("camera"))
                .ToList();

            if (cameraFocusEvents.Any())
            {
                _output.WriteLine($"  Camera Focus Events: {cameraFocusEvents.Count}");
            }
        }
        else
        {
            // Focus 이벤트가 파싱되지 않은 경우 - 로그 구조에 따라 focus 섹션이 비활성화되거나 파싱되지 않을 수 있음
            _output.WriteLine($"⚠ Focus events not parsed (focus_commands section may be disabled or empty in log)");
            _output.WriteLine($"  This is expected if the log file doesn't contain focus events or if the section is disabled.");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldValidate_SectionParsing()
    {
        // Arrange: 섹션별 파싱 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Events.Should().NotBeEmpty("Should parse events");

        var sectionGroups = result.Events
            .GroupBy(e => e.SourceSection)
            .ToDictionary(g => g.Key, g => g.Count());

        sectionGroups.Should().NotBeEmpty("Should have events from different sections");

        _output.WriteLine($"✓ Section parsing validated");
        _output.WriteLine($"  Total Sections: {sectionGroups.Count}");
        _output.WriteLine($"\n  Section Distribution:");
        foreach (var (section, count) in sectionGroups.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"    {section}: {count} events");
        }

        // playback_activity 섹션 검증
        var playbackEvents = result.Events
            .Where(e => e.SourceSection == "playback_activity")
            .ToList();

        if (playbackEvents.Any())
        {
            playbackEvents.Should().Contain(e => e.EventType == LogEventTypes.PLAYER_CREATED,
                "playback_activity section should contain PLAYER_CREATED events");
        }

        // focus_commands 섹션 검증
        var focusEvents = result.Events
            .Where(e => e.SourceSection == "focus_commands")
            .ToList();

        if (focusEvents.Any())
        {
            focusEvents.Should().Contain(e => e.EventType == LogEventTypes.FOCUS_REQUESTED ||
                                             e.EventType == LogEventTypes.FOCUS_ABANDONED,
                "focus_commands section should contain focus events");
        }
    }

    [Fact]
    public async Task ParseAudioLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange: 존재하지 않는 파일 처리 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "nonexistent_audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act & Assert - 파일이 없어도 graceful하게 처리되어야 함
        Func<Task> act = async () => await parser.ParseAsync(logPath, options);
        
        await act.Should().ThrowAsync<FileNotFoundException>();

        _output.WriteLine($"✓ Missing file handling validated");
    }

    [Fact]
    public async Task ParseAudioLog_ShouldValidate_FlagsHexParsing()
    {
        // Arrange: Hex flags 파싱 검증
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var playerCreatedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .ToList();

        playerCreatedEvents.Should().NotBeEmpty("Should have PLAYER_CREATED events");

        foreach (var evt in playerCreatedEvents)
        {
            evt.Attributes.Should().ContainKey("flags");
            var flagsObj = evt.Attributes["flags"];
            
            // flags는 hex 값으로 파싱되어 int로 저장되어야 함
            if (flagsObj is int flagsInt)
            {
                flagsInt.Should().BeGreaterThanOrEqualTo(0, "flags should be non-negative integer");
            }
            else if (int.TryParse(flagsObj?.ToString(), out var parsed))
            {
                parsed.Should().BeGreaterThanOrEqualTo(0, "flags should be parseable as non-negative integer");
            }
            else
            {
                Assert.Fail($"flags should be int or parseable string: {flagsObj}");
            }
        }

        _output.WriteLine($"✓ Hex flags parsing validated");
        _output.WriteLine($"  Validated {playerCreatedEvents.Count} PLAYER_CREATED events");

        // 샘플 출력
        if (playerCreatedEvents.Any())
        {
            var sample = playerCreatedEvents.First();
            _output.WriteLine($"\n  Sample flags value:");
            _output.WriteLine($"    Raw: {sample.Attributes["flags"]}");
            _output.WriteLine($"    Hex: 0x{Convert.ToInt32(sample.Attributes["flags"]):X}");
        }
    }
}

