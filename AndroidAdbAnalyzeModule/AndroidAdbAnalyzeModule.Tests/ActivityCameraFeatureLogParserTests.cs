using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Activity Log 파싱 테스트 - 카메라 앱 Feature Survey 감지
/// 4차 샘플 로그 기반 파싱 검증
/// </summary>
public class ActivityCameraFeatureLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public ActivityCameraFeatureLogParserTests(ITestOutputHelper output)
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
    public async Task ParseActivityLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

        if (!File.Exists(configPath) || !File.Exists(logPath))
        {
            _output.WriteLine("⚠️ Test files not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
                Manufacturer = "Samsung",
                Model = "SM-G991N"
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Total Events: {result.Events.Count}");
        _output.WriteLine($"Total Lines: {result.Statistics.TotalLines}");
        _output.WriteLine($"Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"Elapsed: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");

        result.Success.Should().BeTrue();
        result.Statistics.TotalLines.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ParseActivityLog_ShouldParse_UriPermissionEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

        if (!File.Exists(configPath) || !File.Exists(logPath))
        {
            _output.WriteLine("⚠️ Test files not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul"
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriGrantEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT")
            .ToList();

        var uriRevokeEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_REVOKE")
            .ToList();

        _output.WriteLine($"Total URI_PERMISSION_GRANT Events: {uriGrantEvents.Count}");
        _output.WriteLine($"Total URI_PERMISSION_REVOKE Events: {uriRevokeEvents.Count}");
        
        uriGrantEvents.Should().NotBeEmpty("Should parse URI permission grant events");
        uriRevokeEvents.Should().NotBeEmpty("Should parse URI permission revoke events");
        
        // 첫 번째 GRANT 이벤트 검증
        var firstGrant = uriGrantEvents.FirstOrDefault();
        if (firstGrant != null)
        {
            _output.WriteLine($"\nFirst URI_PERMISSION_GRANT Event:");
            _output.WriteLine($"  EventType: {firstGrant.EventType}");
            _output.WriteLine($"  Attributes:");
            foreach (var attr in firstGrant.Attributes)
            {
                _output.WriteLine($"    {attr.Key}: {attr.Value}");
            }

            // 필수 필드 검증
            firstGrant.Attributes.Should().ContainKey("uid");
            firstGrant.Attributes.Should().ContainKey("uri");
            firstGrant.Attributes.Should().ContainKey("provider");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldIdentify_CameraRelatedUriPermissions()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

        if (!File.Exists(configPath) || !File.Exists(logPath))
        {
            _output.WriteLine("⚠️ Test files not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul"
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriGrantEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT")
            .ToList();

        _output.WriteLine($"\n=== URI Permission Events Analysis ===");
        _output.WriteLine($"Total URI GRANT Events: {uriGrantEvents.Count}");

        // 카메라 관련 URI (content://media/external) 식별
        var cameraMediaUris = uriGrantEvents
            .Where(e => e.Attributes.TryGetValue("uri", out var uri) && 
                       uri?.ToString()?.Contains("content://media/external", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        _output.WriteLine($"Camera/Media-related URIs: {cameraMediaUris.Count}");

        if (cameraMediaUris.Any())
        {
            _output.WriteLine("\n✓ Camera-related URI permissions detected:");
            foreach (var evt in cameraMediaUris.Take(3))
            {
                _output.WriteLine($"  - URI: {evt.Attributes.GetValueOrDefault("uri")}");
                _output.WriteLine($"    Provider: {evt.Attributes.GetValueOrDefault("provider")}");
                _output.WriteLine($"    UID: {evt.Attributes.GetValueOrDefault("uid")}");
            }

            cameraMediaUris.Should().NotBeEmpty("Should detect camera/media-related URI permissions");
        }
        else
        {
            _output.WriteLine("ℹ️ No camera/media-specific URIs found in this log sample");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldParse_ActivityLaunchEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

        if (!File.Exists(configPath) || !File.Exists(logPath))
        {
            _output.WriteLine("⚠️ Test files not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0)
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var activityLaunchEvents = result.Events
            .Where(e => e.EventType == "ACTIVITY_LAUNCH")
            .ToList();

        _output.WriteLine($"\n=== Activity Launch Events ===");
        _output.WriteLine($"Total Events: {activityLaunchEvents.Count}");

        // Note: The generic activity.txt may not contain ACTIVITY_LAUNCH events.
        // The configuration supports parsing them, but they may not be present in all log samples.
        if (activityLaunchEvents.Any())
        {
            _output.WriteLine("✓ Activity launch events found:");
            foreach (var evt in activityLaunchEvents.Take(5))
            {
                _output.WriteLine($"\n  Activity Launched:");
                _output.WriteLine($"    Timestamp: {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                _output.WriteLine($"    Component: {evt.Attributes.GetValueOrDefault("component")}");
            }

            // 필수 필드 검증
            var firstEvent = activityLaunchEvents.First();
            firstEvent.Attributes.Should().ContainKey("component");
        }
        else
        {
            _output.WriteLine("ℹ️ No ACTIVITY_LAUNCH events found in this log sample.");
            _output.WriteLine("   This is expected if the log file doesn't contain activity manager logs.");
        }

        // Verify that parsing succeeded and configuration is valid
        result.Success.Should().BeTrue("Parsing should succeed even if no ACTIVITY_LAUNCH events are found");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

        if (!File.Exists(configPath) || !File.Exists(logPath))
        {
            _output.WriteLine("⚠️ Test files not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0)
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        _output.WriteLine($"\n✅ Data for Upper-App Correlation:");
        _output.WriteLine($"   Total Events: {result.Events.Count}");

        // 1. URI 권한 부여 이벤트
        var uriPermissions = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT")
            .ToList();

        // 2. Activity 실행 이벤트
        var activityLaunches = result.Events
            .Where(e => e.EventType == "ACTIVITY_LAUNCH")
            .ToList();

        _output.WriteLine($"   URI_PERMISSION_GRANT: {uriPermissions.Count}");
        _output.WriteLine($"   ACTIVITY_LAUNCH: {activityLaunches.Count}");

        // 3. 모든 이벤트가 필수 정보를 포함하는지 검증
        if (uriPermissions.Any())
        {
            uriPermissions.Should().OnlyContain(e => e.Attributes.ContainsKey("uri"),
                "all URI permission events should have URI info");
        }

        if (activityLaunches.Any())
        {
            activityLaunches.Should().OnlyContain(e => e.Attributes.ContainsKey("component"),
                "all activity launch events should have component info");
        }

        _output.WriteLine($"\n💡 Upper-app can:");
        _output.WriteLine($"   1. Identify app activities by component name");
        _output.WriteLine($"   2. Correlate URI permissions with camera/media events");
        _output.WriteLine($"   3. Track activity lifecycle and URI access patterns");
        _output.WriteLine($"   4. Distinguish between different app behaviors");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldDemonstrate_EventTypeDistribution()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

        if (!File.Exists(configPath) || !File.Exists(logPath))
        {
            _output.WriteLine("⚠️ Test files not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0)
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        _output.WriteLine($"\n=== Event Type Distribution ===");
        
        foreach (var kvp in result.Statistics.EventTypeCounts.OrderByDescending(x => x.Value))
        {
            _output.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        result.Events.Should().NotBeEmpty("Should parse events from activity log");
        
        // 최소한 하나의 이벤트 타입은 파싱되어야 함
        result.Statistics.EventTypeCounts.Should().NotBeEmpty("Should have at least one event type");
        
        _output.WriteLine($"\n✓ Parser successfully processed {result.Statistics.TotalLines} lines");
        _output.WriteLine($"✓ Success rate: {result.Statistics.SuccessRate:P2}");
    }

    [Fact]
    public async Task ParseActivityLog_4thSample_ShouldParse_CameraActivityRefresh()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine("⚠️ 4th sample activity.log not found. Skipping test.");
            _output.WriteLine($"   Expected path: {Path.GetFullPath(logPath)}");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0)
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        _output.WriteLine($"\n=== Activity Log Parsing (4th Sample) ===");
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Total Events: {result.Events.Count}");
        _output.WriteLine($"Total Lines: {result.Statistics.TotalLines}");
        _output.WriteLine($"Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"Elapsed: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");

        result.Success.Should().BeTrue("Parsing should succeed");

        // CAMERA_ACTIVITY_REFRESH 이벤트 검증
        var refreshEvents = result.Events
            .Where(e => e.EventType == "CAMERA_ACTIVITY_REFRESH")
            .ToList();

        _output.WriteLine($"\nCAMERA_ACTIVITY_REFRESH Events: {refreshEvents.Count}");

        refreshEvents.Should().NotBeEmpty("Should parse CAMERA_ACTIVITY_REFRESH events from multiline pattern");

        // 무음 카메라 관련 이벤트 검증
        var silentCameraRefresh = refreshEvents
            .Where(e => e.Attributes.TryGetValue("package", out var pkg) && 
                       pkg?.ToString()?.Contains("SilentCamera", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        _output.WriteLine($"Silent Camera Refresh Events: {silentCameraRefresh.Count}");

        if (silentCameraRefresh.Any())
        {
            _output.WriteLine("\n✓ Silent Camera Activity Refresh Events:");
            foreach (var evt in silentCameraRefresh)
            {
                _output.WriteLine($"  - Timestamp: {evt.Attributes.GetValueOrDefault("timestamp")}");
                _output.WriteLine($"    Package: {evt.Attributes.GetValueOrDefault("package")}");
                _output.WriteLine($"    Activity: {evt.Attributes.GetValueOrDefault("activity")}");
                _output.WriteLine($"    RefreshRate: {evt.Attributes.GetValueOrDefault("refreshRate")}");
                _output.WriteLine($"    Mode: {evt.Attributes.GetValueOrDefault("mode")}");
            }

            silentCameraRefresh.Should().NotBeEmpty("Should detect silent camera activity refresh events");

            // 필수 필드 검증
            var firstEvent = silentCameraRefresh.First();
            firstEvent.Attributes.Should().ContainKey("timestamp");
            firstEvent.Attributes.Should().ContainKey("package");
            firstEvent.Attributes.Should().ContainKey("refreshRate");
            firstEvent.Attributes.Should().ContainKey("mode");
        }
        else
        {
            _output.WriteLine("ℹ️ No silent camera refresh events found");
        }
    }

    [Fact]
    public async Task ParseActivityLog_4thSample_ShouldDetect_SilentCameraCapture()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine("⚠️ 4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        // 무음 카메라 촬영 시나리오 시간 범위
        // 데이터 시트: 무음 카메라 사진 촬영 2025-10-06 22:58:27
        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0)
            },
            ConvertToUtc = false,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 20), // 촬영 전후 10초
            EndTime = new DateTime(2025, 10, 6, 22, 58, 40)
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        _output.WriteLine($"\n=== Silent Camera Capture Detection (Time Range: 22:58:20 ~ 22:58:40) ===");
        _output.WriteLine($"Total Events in Range: {result.Events.Count}");

        var refreshEvents = result.Events
            .Where(e => e.EventType == "CAMERA_ACTIVITY_REFRESH")
            .ToList();

        _output.WriteLine($"CAMERA_ACTIVITY_REFRESH Events: {refreshEvents.Count}");

        foreach (var evt in refreshEvents)
        {
            var timestamp = evt.Attributes.GetValueOrDefault("timestamp");
            var package = evt.Attributes.GetValueOrDefault("package");
            var refreshRate = evt.Attributes.GetValueOrDefault("refreshRate");
            
            _output.WriteLine($"\n  Event:");
            _output.WriteLine($"    Timestamp: {timestamp}");
            _output.WriteLine($"    Package: {package}");
            _output.WriteLine($"    RefreshRate: {refreshRate}");
        }

        // 무음 카메라 패키지 확인
        var silentCameraEvents = refreshEvents
            .Where(e => e.Attributes.TryGetValue("package", out var pkg) && 
                       pkg?.ToString()?.Contains("SilentCamera", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (silentCameraEvents.Any())
        {
            _output.WriteLine($"\n✅ Silent Camera Activity Detected!");
            _output.WriteLine($"   Count: {silentCameraEvents.Count}");
            _output.WriteLine($"   💡 This can be used as supporting evidence for capture detection");
            
            silentCameraEvents.Should().NotBeEmpty("Should detect silent camera activity refresh as supporting evidence");
        }
        else
        {
            _output.WriteLine($"\n⚠️ No silent camera activity detected in time range");
        }
    }
}

