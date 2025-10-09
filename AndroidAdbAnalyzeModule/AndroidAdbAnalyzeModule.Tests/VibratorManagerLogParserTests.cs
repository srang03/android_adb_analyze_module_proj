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
}

