using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Media Camera Service 로그 파싱 테스트
/// </summary>
public class MediaCameraLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public MediaCameraLogParserTests(ITestOutputHelper output)
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
    public async Task ParseMediaCameraLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();
        result.Statistics.TotalLines.Should().BeGreaterThan(0);
        result.Statistics.ParsedLines.Should().BeGreaterThan(0);
        result.Statistics.SuccessRate.Should().BeGreaterThan(0);
        
        _output.WriteLine($"총 이벤트: {result.Events.Count}");
        _output.WriteLine($"파싱된 라인: {result.Statistics.ParsedLines}");
        _output.WriteLine($"성공률: {result.Statistics.SuccessRate:P2}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldParse_ConnectEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Success.Should().BeTrue();
        
        var connectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT)
            .ToList();

        connectEvents.Should().NotBeEmpty("CAMERA_CONNECT 이벤트가 파싱되어야 합니다");
        
        // 첫 번째 CONNECT 이벤트 검증
        var firstConnect = connectEvents.First();
        firstConnect.Attributes.Should().ContainKey("deviceId");
        firstConnect.Attributes.Should().ContainKey("package");
        firstConnect.Attributes.Should().ContainKey("pid");
        firstConnect.Attributes.Should().ContainKey("priority");
        
        _output.WriteLine($"CONNECT 이벤트 수: {connectEvents.Count}");
        _output.WriteLine($"첫 CONNECT - Device: {firstConnect.Attributes["deviceId"]}, Package: {firstConnect.Attributes["package"]}, PID: {firstConnect.Attributes["pid"]}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldParse_DisconnectEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Success.Should().BeTrue();
        
        var disconnectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .ToList();

        disconnectEvents.Should().NotBeEmpty("CAMERA_DISCONNECT 이벤트가 파싱되어야 합니다");
        
        // 첫 번째 DISCONNECT 이벤트 검증
        var firstDisconnect = disconnectEvents.First();
        firstDisconnect.Attributes.Should().ContainKey("deviceId");
        firstDisconnect.Attributes.Should().ContainKey("package");
        firstDisconnect.Attributes.Should().ContainKey("pid");
        
        _output.WriteLine($"DISCONNECT 이벤트 수: {disconnectEvents.Count}");
        _output.WriteLine($"첫 DISCONNECT - Device: {firstDisconnect.Attributes["deviceId"]}, Package: {firstDisconnect.Attributes["package"]}, PID: {firstDisconnect.Attributes["pid"]}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldIdentify_CameraUsageSession()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Success.Should().BeTrue();
        
        var connectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        var disconnectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT).ToList();

        connectEvents.Should().NotBeEmpty();
        disconnectEvents.Should().NotBeEmpty();
        
        // 카메라 세션 매칭 가능 여부 검증 (PID 기반)
        var connectPids = connectEvents
            .Select(e => e.Attributes["pid"].ToString())
            .ToHashSet();
        
        var disconnectPids = disconnectEvents
            .Select(e => e.Attributes["pid"].ToString())
            .ToHashSet();
        
        var matchingPids = connectPids.Intersect(disconnectPids).ToList();
        
        matchingPids.Should().NotBeEmpty("CONNECT와 DISCONNECT가 매칭되는 PID가 있어야 합니다");
        
        _output.WriteLine($"총 CONNECT 이벤트: {connectEvents.Count}");
        _output.WriteLine($"총 DISCONNECT 이벤트: {disconnectEvents.Count}");
        _output.WriteLine($"매칭되는 PID 수: {matchingPids.Count}");
        _output.WriteLine($"매칭 PID 목록: {string.Join(", ", matchingPids)}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Success.Should().BeTrue();
        
        // 상위 애플리케이션에서 상관관계 분석에 필요한 데이터 검증
        var allEvents = result.Events.ToList();
        
        // 1. 타임스탬프가 파싱되어야 함
        allEvents.All(e => e.Timestamp != default).Should().BeTrue("모든 이벤트에 타임스탬프가 있어야 합니다");
        
        // 2. DeviceId, Package, PID 필드가 있어야 함
        allEvents.All(e => 
            e.Attributes.ContainsKey("deviceId") &&
            e.Attributes.ContainsKey("package") &&
            e.Attributes.ContainsKey("pid")
        ).Should().BeTrue("상관관계 분석에 필요한 필드(deviceId, package, pid)가 모두 있어야 합니다");
        
        // 3. 카메라 앱 패키지 필터링 가능 여부
        var cameraAppEvents = allEvents
            .Where(e => e.Attributes["package"].ToString()!.Contains("camera", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        cameraAppEvents.Should().NotBeEmpty("카메라 앱 이벤트가 있어야 합니다");
        
        _output.WriteLine($"전체 이벤트: {allEvents.Count}");
        _output.WriteLine($"카메라 앱 이벤트: {cameraAppEvents.Count}");
        _output.WriteLine("✅ 상위 애플리케이션에서 상관관계 분석(CONNECT→DISCONNECT 세션 추적)에 필요한 모든 데이터 제공됨");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldParse_TimestampAccurately()
    {
        // Arrange: 타임스탬프 파싱 정확도 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        // 모든 이벤트가 유효한 타임스탬프를 가져야 함
        result.Events.Should().OnlyContain(e => e.Timestamp != default,
            "All events should have valid timestamps");

        // 타임스탬프가 정렬 가능해야 함
        var sortedEvents = result.Events.OrderBy(e => e.Timestamp).ToList();
        sortedEvents.Should().HaveCountGreaterThan(1, "Should have multiple events for sorting validation");

        // 시간 순서 검증
        for (int i = 1; i < sortedEvents.Count; i++)
        {
            sortedEvents[i].Timestamp.Should().BeOnOrAfter(sortedEvents[i - 1].Timestamp,
                "Events should be chronologically orderable");
        }

        _output.WriteLine($"✓ Timestamp parsing validated");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Time Range: {sortedEvents.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff} ~ {sortedEvents.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Duration: {(sortedEvents.Last().Timestamp - sortedEvents.First().Timestamp).TotalSeconds:F2}s");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldValidate_PidType()
    {
        // Arrange: PID가 올바른 타입으로 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        foreach (var evt in result.Events)
        {
            evt.Attributes.Should().ContainKey("pid", "All events should have PID");
            var pid = evt.Attributes["pid"];
            pid.Should().NotBeNull("PID should not be null");

            if (pid is int)
            {
                ((int)pid).Should().BeGreaterThanOrEqualTo(0, "PID should be a non-negative integer");
            }
            else if (int.TryParse(pid.ToString(), out var parsedPid))
            {
                parsedPid.Should().BeGreaterThanOrEqualTo(0, "PID should be parseable as a non-negative integer");
            }
            else
            {
                Assert.Fail($"PID should be an integer, but got: {pid} (type: {pid.GetType().Name})");
            }
        }

        // PID 분포 확인
        var uniquePids = result.Events
            .Select(e => e.Attributes["pid"].ToString())
            .Distinct()
            .Count();

        _output.WriteLine($"✓ PID type validation passed");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Unique PIDs: {uniquePids}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldValidate_DeviceIdType()
    {
        // Arrange: DeviceId가 올바르게 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        foreach (var evt in result.Events)
        {
            evt.Attributes.Should().ContainKey("deviceId", "All events should have deviceId");
            var deviceId = evt.Attributes["deviceId"];
            deviceId.Should().NotBeNull("deviceId should not be null");

            var deviceIdStr = deviceId.ToString();
            deviceIdStr.Should().NotBeNullOrWhiteSpace("deviceId should not be empty");
        }

        // DeviceId 분포 확인
        var uniqueDeviceIds = result.Events
            .Select(e => e.Attributes["deviceId"].ToString())
            .Distinct()
            .ToList();

        _output.WriteLine($"✓ DeviceId validation passed");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Unique DeviceIds: {uniqueDeviceIds.Count}");
        _output.WriteLine($"  DeviceId List: {string.Join(", ", uniqueDeviceIds)}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldValidate_PriorityAttribute()
    {
        // Arrange: Priority 속성 검증 (CONNECT 이벤트에만 존재)
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        var connectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        connectEvents.Should().NotBeEmpty("Should have CAMERA_CONNECT events");

        // CONNECT 이벤트는 priority 속성을 가져야 함
        foreach (var evt in connectEvents)
        {
            evt.Attributes.Should().ContainKey("priority", "CAMERA_CONNECT events should have priority attribute");
            
            var priority = evt.Attributes["priority"];
            priority.Should().NotBeNull("priority should not be null");

            // Priority는 숫자 형태여야 함
            if (priority is int)
            {
                ((int)priority).Should().BeGreaterThanOrEqualTo(0, "priority should be a non-negative integer");
            }
            else if (int.TryParse(priority.ToString(), out var parsedPriority))
            {
                parsedPriority.Should().BeGreaterThanOrEqualTo(0, "priority should be parseable as a non-negative integer");
            }
            else
            {
                // 일부 priority는 특수 문자열일 수 있음 (예: "MAX")
                _output.WriteLine($"  Non-numeric priority detected: {priority}");
            }
        }

        // Priority 분포 확인
        var priorityDistribution = connectEvents
            .GroupBy(e => e.Attributes["priority"].ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"✓ Priority attribute validation passed");
        _output.WriteLine($"  Total CONNECT Events: {connectEvents.Count}");
        _output.WriteLine($"  Priority Distribution:");
        foreach (var group in priorityDistribution)
        {
            _output.WriteLine($"    {group.Key}: {group.Count()}");
        }
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldParse_MultiplePackages()
    {
        // Arrange: 다양한 패키지의 이벤트가 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        var packageGroups = result.Events
            .Where(e => e.Attributes.ContainsKey("package"))
            .GroupBy(e => e.Attributes["package"].ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        packageGroups.Should().NotBeEmpty("Should parse events from at least one package");

        _output.WriteLine($"✓ Multiple package parsing validated");
        _output.WriteLine($"  Total Packages: {packageGroups.Count}");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"\n  Package Distribution:");

        foreach (var group in packageGroups)
        {
            var packageName = group.Key;
            var eventCount = group.Count();
            var connectCount = group.Count(e => e.EventType == LogEventTypes.CAMERA_CONNECT);
            var disconnectCount = group.Count(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT);

            _output.WriteLine($"    - {packageName}");
            _output.WriteLine($"      Total: {eventCount}, CONNECT: {connectCount}, DISCONNECT: {disconnectCount}");
        }

        // 카메라 앱이 포함되어 있어야 함
        var cameraPackages = packageGroups
            .Where(g => g.Key?.Contains("camera", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        cameraPackages.Should().NotBeEmpty("Should include camera-related packages");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldValidate_EventTypeDistribution()
    {
        // Arrange: 이벤트 타입 분포 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        var eventTypeGroups = result.Events
            .GroupBy(e => e.EventType)
            .OrderByDescending(g => g.Count())
            .ToList();

        eventTypeGroups.Should().NotBeEmpty("Should have at least one event type");

        // CAMERA_CONNECT와 CAMERA_DISCONNECT 이벤트가 모두 존재해야 함
        var connectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        var disconnectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT).ToList();

        connectEvents.Should().NotBeEmpty("Should have CAMERA_CONNECT events");
        disconnectEvents.Should().NotBeEmpty("Should have CAMERA_DISCONNECT events");

        _output.WriteLine($"✓ Event type distribution validated");
        _output.WriteLine($"  Total Event Types: {eventTypeGroups.Count}");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"\n  Event Type Distribution:");

        foreach (var group in eventTypeGroups)
        {
            var percentage = (group.Count() * 100.0 / result.Events.Count);
            _output.WriteLine($"    {group.Key}: {group.Count()} ({percentage:F1}%)");
        }

        // CONNECT와 DISCONNECT 비율 확인
        var connectDisconnectRatio = (double)connectEvents.Count / disconnectEvents.Count;
        _output.WriteLine($"\n  CONNECT/DISCONNECT Ratio: {connectDisconnectRatio:F2}");
        
        // 일반적으로 CONNECT와 DISCONNECT 수가 비슷해야 함
        Math.Abs(connectDisconnectRatio - 1.0).Should().BeLessThan(0.5, 
            "CONNECT and DISCONNECT events should have similar counts (ratio close to 1.0)");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldCorrelate_ConnectDisconnectTiming()
    {
        // Arrange: CONNECT-DISCONNECT 시간 상관관계 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        var connectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT)
            .OrderBy(e => e.Timestamp)
            .ToList();

        var disconnectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .OrderBy(e => e.Timestamp)
            .ToList();

        connectEvents.Should().NotBeEmpty("Should have CAMERA_CONNECT events");
        disconnectEvents.Should().NotBeEmpty("Should have CAMERA_DISCONNECT events");

        // PID별로 CONNECT-DISCONNECT 페어 찾기
        var sessions = new List<(string pid, DateTime connectTime, DateTime disconnectTime, double durationSeconds)>();

        foreach (var connect in connectEvents)
        {
            var connectPid = connect.Attributes["pid"].ToString();
            var connectPackage = connect.Attributes["package"].ToString();

            // 같은 PID와 Package의 DISCONNECT 이벤트 찾기
            var matchingDisconnect = disconnectEvents
                .Where(d => d.Attributes["pid"].ToString() == connectPid &&
                           d.Attributes["package"].ToString() == connectPackage &&
                           d.Timestamp >= connect.Timestamp)
                .OrderBy(d => d.Timestamp)
                .FirstOrDefault();

            if (matchingDisconnect != null)
            {
                var duration = (matchingDisconnect.Timestamp - connect.Timestamp).TotalSeconds;
                sessions.Add((connectPid!, connect.Timestamp, matchingDisconnect.Timestamp, duration));
            }
        }

        sessions.Should().NotBeEmpty("Should have at least one matched CONNECT-DISCONNECT session");

        _output.WriteLine($"✓ CONNECT-DISCONNECT correlation validated");
        _output.WriteLine($"  Total CONNECT Events: {connectEvents.Count}");
        _output.WriteLine($"  Total DISCONNECT Events: {disconnectEvents.Count}");
        _output.WriteLine($"  Matched Sessions: {sessions.Count}");

        if (sessions.Any())
        {
            var avgDuration = sessions.Average(s => s.durationSeconds);
            var maxDuration = sessions.Max(s => s.durationSeconds);
            var minDuration = sessions.Min(s => s.durationSeconds);

            _output.WriteLine($"\n  Session Duration Statistics:");
            _output.WriteLine($"    Average: {avgDuration:F2}s");
            _output.WriteLine($"    Min: {minDuration:F2}s");
            _output.WriteLine($"    Max: {maxDuration:F2}s");

            _output.WriteLine($"\n  Sample Sessions:");
            foreach (var session in sessions.Take(3))
            {
                _output.WriteLine($"    PID {session.pid}: {session.connectTime:HH:mm:ss.fff} → {session.disconnectTime:HH:mm:ss.fff} (duration: {session.durationSeconds:F2}s)");
            }
        }
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldCalculate_SessionDuration()
    {
        // Arrange: 세션 Duration 계산 가능성 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        var connectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT)
            .ToList();

        var disconnectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .ToList();

        connectEvents.Should().NotBeEmpty();
        disconnectEvents.Should().NotBeEmpty();

        // PID와 Package별로 그룹핑하여 세션 추적
        var sessionsByPidAndPackage = connectEvents
            .GroupBy(e => (pid: e.Attributes["pid"].ToString(), package: e.Attributes["package"].ToString()))
            .Where(g => g.Key.pid != null && g.Key.package != null)
            .ToList();

        var calculatedSessions = 0;
        var totalDuration = 0.0;

        foreach (var group in sessionsByPidAndPackage)
        {
            var connects = group.OrderBy(e => e.Timestamp).ToList();
            var disconnects = disconnectEvents
                .Where(d => d.Attributes["pid"].ToString() == group.Key.pid &&
                           d.Attributes["package"].ToString() == group.Key.package)
                .OrderBy(e => e.Timestamp)
                .ToList();

            for (int i = 0; i < Math.Min(connects.Count, disconnects.Count); i++)
            {
                if (disconnects[i].Timestamp >= connects[i].Timestamp)
                {
                    var duration = (disconnects[i].Timestamp - connects[i].Timestamp).TotalSeconds;
                    totalDuration += duration;
                    calculatedSessions++;
                }
            }
        }

        calculatedSessions.Should().BeGreaterThan(0, "Should be able to calculate at least one session duration");

        var avgDuration = totalDuration / calculatedSessions;

        _output.WriteLine($"✓ Session duration calculation validated");
        _output.WriteLine($"  Total Sessions with Duration: {calculatedSessions}");
        _output.WriteLine($"  Average Session Duration: {avgDuration:F2}s");
        _output.WriteLine($"  Total Camera Usage Time: {totalDuration:F2}s");
        
        // 평균 세션 길이가 합리적인 범위여야 함 (0초 초과, 3600초 미만)
        avgDuration.Should().BeGreaterThan(0, "Average session duration should be positive");
        avgDuration.Should().BeLessThan(3600, "Average session duration should be less than 1 hour");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange: 파일이 없거나 잘못된 경우 에러 처리 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var nonExistentLogPath = Path.Combine("TestData", "non_existent_media_camera.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await parser.ParseAsync(nonExistentLogPath, options);
        });

        _output.WriteLine($"✓ Error handling validated");
        _output.WriteLine($"  FileNotFoundException correctly thrown for missing file");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldGroup_EventsByPackage()
    {
        // Arrange: 패키지별 그룹핑 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

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
        var packageGroups = result.Events
            .GroupBy(e => e.Attributes["package"].ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        packageGroups.Should().NotBeEmpty("Should be able to group events by package");

        _output.WriteLine($"✓ Package grouping validated");
        _output.WriteLine($"  Total Packages: {packageGroups.Count}");
        _output.WriteLine($"\n  Package Details:");

        foreach (var group in packageGroups)
        {
            var packageName = group.Key;
            var connects = group.Count(e => e.EventType == LogEventTypes.CAMERA_CONNECT);
            var disconnects = group.Count(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT);
            var firstEvent = group.OrderBy(e => e.Timestamp).First().Timestamp;
            var lastEvent = group.OrderBy(e => e.Timestamp).Last().Timestamp;
            var totalTime = (lastEvent - firstEvent).TotalSeconds;

            _output.WriteLine($"    {packageName}:");
            _output.WriteLine($"      Total Events: {group.Count()}");
            _output.WriteLine($"      CONNECT: {connects}, DISCONNECT: {disconnects}");
            _output.WriteLine($"      Time Range: {firstEvent:HH:mm:ss} ~ {lastEvent:HH:mm:ss} ({totalTime:F2}s)");
        }
    }
}

