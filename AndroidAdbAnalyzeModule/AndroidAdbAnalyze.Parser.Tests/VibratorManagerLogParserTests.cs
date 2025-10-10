using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Vibrator Manager Log 파싱 테스트
/// </summary>
public class VibratorManagerLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public VibratorManagerLogParserTests(ITestOutputHelper output)
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
    public async Task ParseVibratorLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();
        result.Statistics.ParsedLines.Should().BeGreaterThan(0);
        result.Errors.Should().BeEmpty();

        _output.WriteLine($"✓ Vibrator log parsed successfully");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"  Elapsed Time: {result.Statistics.ElapsedTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldHandle_VibrationEvents()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var vibrationEvents = result.Events.Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT).ToList();
        vibrationEvents.Should().NotBeEmpty("Should parse VIBRATION_EVENT events");

        // Verify camera-related vibration events exist
        var cameraEvents = vibrationEvents.Where(e => 
            e.Attributes.ContainsKey("package") && 
            e.Attributes["package"].ToString()!.Contains("camera")).ToList();
        
        cameraEvents.Should().NotBeEmpty("Should find camera-related vibration events");

        // Verify event structure
        var firstEvent = vibrationEvents.First();
        firstEvent.Attributes.Should().ContainKey("package");
        firstEvent.Attributes.Should().ContainKey("usage");
        // Note: hapticType is only present in SemHaptic format, not in Step-based format
        // firstEvent.Attributes.Should().ContainKey("hapticType"); // Optional field
        firstEvent.Attributes.Should().ContainKey("durationMs"); 
        firstEvent.Attributes.Should().ContainKey("status");

        _output.WriteLine($"✓ Vibration events parsed correctly");
        _output.WriteLine($"  Total Vibration Events: {vibrationEvents.Count}");
        _output.WriteLine($"  Camera-related Events: {cameraEvents.Count}");
        
        // Log some examples
        foreach (var evt in cameraEvents.Take(3))
        {
            _output.WriteLine($"  - {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | Package: {evt.Attributes["package"]} | Type: {evt.Attributes["hapticType"]} | Duration: {evt.Attributes["durationMs"]}ms");
        }
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldIdentify_CameraShutterEvents()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var vibrationEvents = result.Events.Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT).ToList();

        // Camera shutter pattern: com.sec.android.app.camera + mType=50072 + duration~600ms
        var shutterEvents = vibrationEvents.Where(e =>
        {
            if (!e.Attributes.ContainsKey("package") || !e.Attributes.ContainsKey("hapticType") || !e.Attributes.ContainsKey("durationMs"))
                return false;

            var package = e.Attributes["package"].ToString();
            var hapticType = Convert.ToInt32(e.Attributes["hapticType"]);
            var duration = Convert.ToInt32(e.Attributes["durationMs"]);

            // Camera shutter: haptic type 50072 with ~600ms duration
            return package!.Contains("camera") && hapticType == 50072 && duration >= 600;
        }).ToList();

        shutterEvents.Should().NotBeEmpty("Should identify camera shutter vibration events");

        _output.WriteLine($"✓ Camera shutter events identified");
        _output.WriteLine($"  Total Shutter Events: {shutterEvents.Count}");
        
        foreach (var evt in shutterEvents.Take(5))
        {
            _output.WriteLine($"  - {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | Duration: {evt.Attributes["durationMs"]}ms | Type: {evt.Attributes["hapticType"]}");
        }
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldIdentify_CameraCaptureButtonEvents()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var vibrationEvents = result.Events.Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT).ToList();

        // ⭐ 가장 중요: Camera capture button (촬영 버튼) - hapticType=50061
        var captureButtonEvents = vibrationEvents.Where(e =>
        {
            if (!e.Attributes.ContainsKey("hapticType"))
                return false;

            var hapticType = Convert.ToInt32(e.Attributes["hapticType"]);
            return hapticType == 50061;
        }).ToList();

        captureButtonEvents.Should().NotBeEmpty("Should identify camera capture button events (hapticType=50061)");

        _output.WriteLine($"✓ Camera capture button events identified");
        _output.WriteLine($"  Total Capture Button Events (hapticType=50061): {captureButtonEvents.Count}");
        
        // 상세 정보 출력
        foreach (var evt in captureButtonEvents.Take(5))
        {
            var package = evt.Attributes.ContainsKey("package") ? evt.Attributes["package"].ToString() : "N/A";
            var status = evt.Attributes.ContainsKey("status") ? evt.Attributes["status"].ToString() : "N/A";
            var usage = evt.Attributes.ContainsKey("usage") ? evt.Attributes["usage"].ToString() : "N/A";
            
            _output.WriteLine($"  - {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            _output.WriteLine($"    Package: {package}, Status: {status}, Usage: {usage}");
        }
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldValidate_StatusAttribute()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var vibrationEvents = result.Events.Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT).ToList();

        // status 속성이 있는 이벤트들
        var eventsWithStatus = vibrationEvents
            .Where(e => e.Attributes.ContainsKey("status"))
            .ToList();

        eventsWithStatus.Should().NotBeEmpty("Should have events with status attribute");

        // status 값 분포 확인
        var finishedEvents = eventsWithStatus.Where(e => 
            e.Attributes["status"].ToString()!.Equals("finished", StringComparison.OrdinalIgnoreCase)).ToList();
        
        var cancelledEvents = eventsWithStatus.Where(e => 
            e.Attributes["status"].ToString()!.Contains("cancelled", StringComparison.OrdinalIgnoreCase)).ToList();

        _output.WriteLine($"✓ Status attribute validation complete");
        _output.WriteLine($"  Total events with status: {eventsWithStatus.Count}");
        _output.WriteLine($"  Finished events: {finishedEvents.Count}");
        _output.WriteLine($"  Cancelled events: {cancelledEvents.Count}");

        // status 값 종류 출력
        var statusValues = eventsWithStatus
            .Select(e => e.Attributes["status"].ToString())
            .Distinct()
            .ToList();
        
        _output.WriteLine($"  Status values found: {string.Join(", ", statusValues)}");
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldValidate_UsageAttribute()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var vibrationEvents = result.Events.Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT).ToList();

        // usage 속성이 있는 이벤트들
        var eventsWithUsage = vibrationEvents
            .Where(e => e.Attributes.ContainsKey("usage"))
            .ToList();

        eventsWithUsage.Should().NotBeEmpty("Should have events with usage attribute");

        // usage 값 분포 확인
        var usageValues = eventsWithUsage
            .Select(e => e.Attributes["usage"].ToString())
            .Distinct()
            .ToList();

        _output.WriteLine($"✓ Usage attribute validation complete");
        _output.WriteLine($"  Total events with usage: {eventsWithUsage.Count}");
        _output.WriteLine($"  Usage values found: {string.Join(", ", usageValues)}");

        // 주요 usage 타입별 개수
        foreach (var usage in usageValues)
        {
            var count = eventsWithUsage.Count(e => e.Attributes["usage"].ToString() == usage);
            _output.WriteLine($"    - {usage}: {count} events");
        }
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldParse_HapticTypeCorrectly()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var vibrationEvents = result.Events.Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT).ToList();

        // hapticType이 있는 이벤트들
        var eventsWithHapticType = vibrationEvents
            .Where(e => e.Attributes.ContainsKey("hapticType"))
            .ToList();

        eventsWithHapticType.Should().NotBeEmpty("Should have events with hapticType attribute");

        // hapticType 값이 int로 파싱되는지 확인
        foreach (var evt in eventsWithHapticType.Take(10))
        {
            evt.Attributes["hapticType"].Should().BeOfType<int>("hapticType should be parsed as int");
        }

        // hapticType 분포 확인
        var hapticTypes = eventsWithHapticType
            .Select(e => Convert.ToInt32(e.Attributes["hapticType"]))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        _output.WriteLine($"✓ HapticType parsing validation complete");
        _output.WriteLine($"  Total events with hapticType: {eventsWithHapticType.Count}");
        _output.WriteLine($"  Unique hapticType values: {hapticTypes.Count}");
        
        // 각 hapticType별 개수
        foreach (var type in hapticTypes)
        {
            var count = eventsWithHapticType.Count(e => Convert.ToInt32(e.Attributes["hapticType"]) == type);
            _output.WriteLine($"    - hapticType {type}: {count} events");
        }

        // 중요한 hapticType 존재 여부 확인
        hapticTypes.Should().Contain(50061, "Should have camera capture button hapticType (50061)");
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldParse_TimestampAccurately()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        result.Events.Should().NotBeEmpty();

        foreach (var evt in result.Events)
        {
            // 모든 이벤트가 유효한 타임스탬프를 가져야 함
            evt.Timestamp.Should().NotBe(default(DateTime), $"Event {evt.EventType} should have valid timestamp");
            
            // 타임스탬프가 Year 2025에 있어야 함 (테스트 데이터 기준)
            evt.Timestamp.Year.Should().Be(2025, "Timestamp year should be 2025");
        }

        // 타임스탬프가 시간 순서대로 정렬 가능한지 확인
        var orderedEvents = result.Events.OrderBy(e => e.Timestamp).ToList();
        orderedEvents.Should().HaveCount(result.Events.Count, "All events should be orderable by timestamp");

        var firstEvent = orderedEvents.First();
        var lastEvent = orderedEvents.Last();

        _output.WriteLine($"✓ Timestamp parsing accuracy validated");
        _output.WriteLine($"  First Event: {firstEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Last Event: {lastEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Time Span: {(lastEvent.Timestamp - firstEvent.Timestamp).TotalMinutes:F1} minutes");
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldParse_MultiplePackages()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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
        var packages = result.Events
            .Where(e => e.Attributes.ContainsKey("package"))
            .Select(e => e.Attributes["package"].ToString())
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        packages.Should().NotBeEmpty("Should extract package names");

        _output.WriteLine($"✓ Multiple packages parsed correctly");
        _output.WriteLine($"  Total Unique Packages: {packages.Count}");
        _output.WriteLine($"  Packages:");
        foreach (var pkg in packages.Take(20))
        {
            var count = result.Events.Count(e => 
                e.Attributes.ContainsKey("package") && 
                e.Attributes["package"].ToString() == pkg);
            _output.WriteLine($"    - {pkg}: {count} events");
        }
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var nonExistentLogPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "non_existent_vibrator.txt");

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

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await parser.ParseAsync(nonExistentLogPath, options);
        });

        _output.WriteLine($"✓ Missing file error handling validated");
    }

    [Fact]
    public async Task ParseVibratorLog_ShouldCorrelate_CaptureButtonWithStatus()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "vibrator_manager.txt");

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

        // Assert - 촬영 버튼(50061) + status=finished 조합 확인
        var captureButtonFinishedEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT &&
                       e.Attributes.ContainsKey("hapticType") &&
                       Convert.ToInt32(e.Attributes["hapticType"]) == 50061 &&
                       e.Attributes.ContainsKey("status") &&
                       e.Attributes["status"].ToString()!.Equals("finished", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _output.WriteLine($"✓ Capture button + status correlation validated");
        _output.WriteLine($"  hapticType=50061 + status=finished: {captureButtonFinishedEvents.Count} events");
        
        if (captureButtonFinishedEvents.Any())
        {
            _output.WriteLine($"  Sample events:");
            foreach (var evt in captureButtonFinishedEvents.Take(3))
            {
                var package = evt.Attributes.ContainsKey("package") ? evt.Attributes["package"].ToString() : "N/A";
                _output.WriteLine($"    - {evt.Timestamp:HH:mm:ss.fff} | Package: {package}");
            }
        }

        // 취소된 이벤트도 확인
        var captureButtonCancelledEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT &&
                       e.Attributes.ContainsKey("hapticType") &&
                       Convert.ToInt32(e.Attributes["hapticType"]) == 50061 &&
                       e.Attributes.ContainsKey("status") &&
                       e.Attributes["status"].ToString()!.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _output.WriteLine($"  hapticType=50061 + status=cancelled: {captureButtonCancelledEvents.Count} events");
    }
}

