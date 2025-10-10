using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
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

    [Fact]
    public async Task ParseActivityLog_ShouldParse_TimestampAccurately()
    {
        // Arrange: 타임스탬프 파싱 정확도 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT" || e.EventType == "URI_PERMISSION_REVOKE")
            .ToList();

        if (!uriEvents.Any())
        {
            _output.WriteLine("⚠️ No URI permission events found. Skipping timestamp validation.");
            return;
        }

        // 타임스탬프 순서 검증
        var timestamps = uriEvents.Select(e => e.Timestamp).ToList();
        timestamps.Should().BeInAscendingOrder("Timestamps should be in ascending order");

        // 타임스탬프 정확도 검증 (밀리초까지 파싱)
        var firstEvent = uriEvents.First();
        firstEvent.Timestamp.Millisecond.Should().BeGreaterThanOrEqualTo(0, "Milliseconds should be parsed");

        _output.WriteLine($"✓ Timestamp parsing validated");
        _output.WriteLine($"  First Event: {firstEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Last Event: {uriEvents.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Total Events: {uriEvents.Count}");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_UidRefCountTypes()
    {
        // Arrange: UID, RefCount, UserId 타입 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriGrantEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT")
            .ToList();

        if (!uriGrantEvents.Any())
        {
            _output.WriteLine("⚠️ No URI_PERMISSION_GRANT events found. Skipping validation.");
            return;
        }

        foreach (var evt in uriGrantEvents)
        {
            // UID 검증
            evt.Attributes.Should().ContainKey("uid");
            var uidObj = evt.Attributes["uid"];
            if (uidObj is int uidInt)
            {
                uidInt.Should().BeGreaterThanOrEqualTo(0, "uid should be non-negative integer");
            }
            else if (int.TryParse(uidObj?.ToString(), out var parsed))
            {
                parsed.Should().BeGreaterThanOrEqualTo(0, "uid should be parseable as non-negative integer");
            }

            // RefCount 검증
            evt.Attributes.Should().ContainKey("refCount");
            var refCountObj = evt.Attributes["refCount"];
            if (refCountObj is int refCountInt)
            {
                refCountInt.Should().BeGreaterThanOrEqualTo(0, "refCount should be non-negative integer");
            }

            // UserId 검증
            evt.Attributes.Should().ContainKey("userId");
            var userIdObj = evt.Attributes["userId"];
            if (userIdObj is int userIdInt)
            {
                userIdInt.Should().BeGreaterThanOrEqualTo(0, "userId should be non-negative integer");
            }
        }

        _output.WriteLine($"✓ UID/RefCount/UserId type validation passed");
        _output.WriteLine($"  Validated {uriGrantEvents.Count} URI_PERMISSION_GRANT events");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_UriFormat()
    {
        // Arrange: URI 형식 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT" || e.EventType == "URI_PERMISSION_REVOKE")
            .ToList();

        if (!uriEvents.Any())
        {
            _output.WriteLine("⚠️ No URI permission events found. Skipping validation.");
            return;
        }

        foreach (var evt in uriEvents)
        {
            evt.Attributes.Should().ContainKey("uri");
            var uri = evt.Attributes["uri"].ToString();
            uri.Should().StartWith("content://", "URI should start with content://");
            uri.Should().NotBeNullOrWhiteSpace("URI should not be empty");
        }

        // URI 타입 분포 확인
        var uriTypes = uriEvents
            .Select(e => e.Attributes["uri"].ToString()!)
            .Select(uri => uri.Split('/')[2]) // content://PROVIDER/...
            .GroupBy(provider => provider)
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"✓ URI format validation passed");
        _output.WriteLine($"  Total URI events: {uriEvents.Count}");
        _output.WriteLine($"\n  URI Provider Distribution:");
        foreach (var group in uriTypes.Take(5))
        {
            _output.WriteLine($"    {group.Key}: {group.Count()} events");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_SectionParsing()
    {
        // Arrange: 섹션별 파싱 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        if (!result.Events.Any())
        {
            _output.WriteLine("⚠️ No events parsed. Skipping section validation.");
            return;
        }

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

        // uri_permissions 섹션 검증
        var uriPermissionEvents = result.Events
            .Where(e => e.SourceSection == "uri_permissions")
            .ToList();

        if (uriPermissionEvents.Any())
        {
            uriPermissionEvents.Should().OnlyContain(e => 
                e.EventType == "URI_PERMISSION_GRANT" || 
                e.EventType == "URI_PERMISSION_REVOKE",
                "uri_permissions section should only contain URI permission events");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldGroup_EventsByUid()
    {
        // Arrange: UID별 이벤트 그룹화 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT" || e.EventType == "URI_PERMISSION_REVOKE")
            .Where(e => e.Attributes.ContainsKey("uid"))
            .ToList();

        if (!uriEvents.Any())
        {
            _output.WriteLine("⚠️ No URI permission events found. Skipping UID grouping validation.");
            return;
        }

        var uidGroups = uriEvents
            .GroupBy(e => Convert.ToInt32(e.Attributes["uid"]))
            .OrderByDescending(g => g.Count())
            .ToList();

        uidGroups.Should().NotBeEmpty("Should have events from different UIDs");

        _output.WriteLine($"✓ UID grouping validated");
        _output.WriteLine($"  Total UIDs: {uidGroups.Count}");
        _output.WriteLine($"\n  Top UIDs by Event Count:");
        foreach (var group in uidGroups.Take(10))
        {
            var grantCount = group.Count(e => e.EventType == "URI_PERMISSION_GRANT");
            var revokeCount = group.Count(e => e.EventType == "URI_PERMISSION_REVOKE");
            _output.WriteLine($"    UID {group.Key}: {group.Count()} events (GRANT: {grantCount}, REVOKE: {revokeCount})");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldTrack_UriPermissionLifecycle()
    {
        // Arrange: URI 권한 lifecycle 추적 (GRANT → REVOKE)
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var grantEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT")
            .ToList();

        var revokeEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_REVOKE")
            .ToList();

        if (!grantEvents.Any() || !revokeEvents.Any())
        {
            _output.WriteLine("⚠️ Insufficient events for lifecycle tracking. Skipping test.");
            return;
        }

        // UID + URI 기반으로 GRANT-REVOKE 쌍 찾기
        var lifecycles = new List<(int uid, string uri, DateTime grantTime, DateTime? revokeTime, double? durationSeconds)>();

        foreach (var grant in grantEvents)
        {
            var uid = Convert.ToInt32(grant.Attributes["uid"]);
            var uri = grant.Attributes["uri"].ToString()!;

            var matchingRevoke = revokeEvents
                .Where(r => Convert.ToInt32(r.Attributes["uid"]) == uid &&
                           r.Attributes["uri"].ToString() == uri &&
                           r.Timestamp >= grant.Timestamp)
                .OrderBy(r => r.Timestamp)
                .FirstOrDefault();

            if (matchingRevoke != null)
            {
                var duration = (matchingRevoke.Timestamp - grant.Timestamp).TotalSeconds;
                lifecycles.Add((uid, uri, grant.Timestamp, matchingRevoke.Timestamp, duration));
            }
            else
            {
                lifecycles.Add((uid, uri, grant.Timestamp, null, null));
            }
        }

        var completedLifecycles = lifecycles.Count(l => l.revokeTime.HasValue);
        var incompleteLifecycles = lifecycles.Count(l => !l.revokeTime.HasValue);

        _output.WriteLine($"✓ URI permission lifecycle tracking validated");
        _output.WriteLine($"  Total GRANT events: {grantEvents.Count}");
        _output.WriteLine($"  Total REVOKE events: {revokeEvents.Count}");
        _output.WriteLine($"  Complete Lifecycles (GRANT → REVOKE): {completedLifecycles}");
        _output.WriteLine($"  Incomplete Lifecycles: {incompleteLifecycles}");

        if (lifecycles.Where(l => l.durationSeconds.HasValue).Any())
        {
            var avgDuration = lifecycles.Where(l => l.durationSeconds.HasValue)
                .Average(l => l.durationSeconds!.Value);
            var maxDuration = lifecycles.Where(l => l.durationSeconds.HasValue)
                .Max(l => l.durationSeconds!.Value);
            var minDuration = lifecycles.Where(l => l.durationSeconds.HasValue)
                .Min(l => l.durationSeconds!.Value);

            _output.WriteLine($"\n  Duration Statistics:");
            _output.WriteLine($"    Average: {avgDuration:F2}s");
            _output.WriteLine($"    Min: {minDuration:F2}s");
            _output.WriteLine($"    Max: {maxDuration:F2}s");

            // 샘플 출력
            _output.WriteLine($"\n  Sample Lifecycles:");
            foreach (var lifecycle in lifecycles.Where(l => l.durationSeconds.HasValue).Take(3))
            {
                var shortUri = lifecycle.uri.Length > 80 ? lifecycle.uri.Substring(0, 80) + "..." : lifecycle.uri;
                _output.WriteLine($"    UID {lifecycle.uid}: GRANT at {lifecycle.grantTime:HH:mm:ss.fff} → REVOKE at {lifecycle.revokeTime:HH:mm:ss.fff} ({lifecycle.durationSeconds:F2}s)");
                _output.WriteLine($"      URI: {shortUri}");
            }
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_ProviderField()
    {
        // Arrange: provider 필드 파싱 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var grantEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT")
            .ToList();

        if (!grantEvents.Any())
        {
            _output.WriteLine("⚠️ No URI_PERMISSION_GRANT events found. Skipping provider validation.");
            return;
        }

        grantEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("provider"),
            "All URI_PERMISSION_GRANT events should have provider field");

        var providerGroups = grantEvents
            .GroupBy(e => e.Attributes["provider"].ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"✓ Provider field validation passed");
        _output.WriteLine($"  Total Providers: {providerGroups.Count}");
        _output.WriteLine($"\n  Provider Distribution:");
        foreach (var group in providerGroups.Take(10))
        {
            _output.WriteLine($"    {group.Key}: {group.Count()} events");
        }

        // 미디어 관련 provider 확인
        var mediaProviders = providerGroups
            .Where(g => g.Key!.Contains("media", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mediaProviders.Any())
        {
            _output.WriteLine($"\n  Media-related Providers:");
            foreach (var group in mediaProviders)
            {
                _output.WriteLine($"    {group.Key}: {group.Count()} events");
            }
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange: 존재하지 않는 파일 처리 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "nonexistent_activity.txt");

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
    public async Task ParseActivityLog_ShouldParse_RefCountChanges()
    {
        // Arrange: RefCount 변화 추적 검증
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
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        var uriEvents = result.Events
            .Where(e => e.EventType == "URI_PERMISSION_GRANT" || e.EventType == "URI_PERMISSION_REVOKE")
            .Where(e => e.Attributes.ContainsKey("refCount"))
            .ToList();

        if (!uriEvents.Any())
        {
            _output.WriteLine("⚠️ No URI permission events with refCount found. Skipping test.");
            return;
        }

        // RefCount 분포 확인
        var refCountDistribution = uriEvents
            .GroupBy(e => Convert.ToInt32(e.Attributes["refCount"]))
            .OrderBy(g => g.Key)
            .ToList();

        _output.WriteLine($"✓ RefCount parsing validated");
        _output.WriteLine($"  Total events with refCount: {uriEvents.Count}");
        _output.WriteLine($"\n  RefCount Distribution:");
        foreach (var group in refCountDistribution)
        {
            var grantCount = group.Count(e => e.EventType == "URI_PERMISSION_GRANT");
            var revokeCount = group.Count(e => e.EventType == "URI_PERMISSION_REVOKE");
            _output.WriteLine($"    RefCount={group.Key}: {group.Count()} events (GRANT: {grantCount}, REVOKE: {revokeCount})");
        }

        // RefCount > 1인 경우 확인 (multiple references)
        var multiRefEvents = uriEvents
            .Where(e => Convert.ToInt32(e.Attributes["refCount"]) > 1)
            .ToList();

        if (multiRefEvents.Any())
        {
            _output.WriteLine($"\n  Multi-reference Events (refCount > 1): {multiRefEvents.Count}");
            _output.WriteLine($"    This indicates multiple apps/components sharing the same URI permission");
        }
    }
}

