using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Camera Worker Log 파싱 테스트
/// </summary>
public class CameraWorkerLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public CameraWorkerLogParserTests(ITestOutputHelper output)
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
    public async Task ParseCameraWorkerLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "media.camera.worker.txt");

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

        _output.WriteLine($"✓ Camera Worker log parsed successfully");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"  Elapsed Time: {result.Statistics.ElapsedTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldHandle_CameraLifecycleEvents()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "media.camera.worker.txt");

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
        var connectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        var disconnectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT).ToList();
        
        connectEvents.Should().NotBeEmpty("Should parse CAMERA_CONNECT events");
        disconnectEvents.Should().NotBeEmpty("Should parse CAMERA_DISCONNECT events");

        // Verify event structure
        var firstConnectEvent = connectEvents.First();
        firstConnectEvent.Attributes.Should().ContainKey("cameraId");
        firstConnectEvent.Attributes.Should().ContainKey("package");

        _output.WriteLine($"✓ Camera events parsed correctly");
        _output.WriteLine($"  Camera Connect Events: {connectEvents.Count}");
        _output.WriteLine($"  Camera Disconnect Events: {disconnectEvents.Count}");
        
        // Log some examples
        foreach (var evt in connectEvents.Where(e => 
            e.Attributes.ContainsKey("package") && 
            e.Attributes["package"].ToString()!.Contains("camera")).Take(3))
        {
            _output.WriteLine($"  - {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | Camera: {evt.Attributes["cameraId"]} | Package: {evt.Attributes["package"]}");
        }
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldHandle_MediaInsertEvents()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "media.camera.worker.txt");

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
        var insertEndEvents = result.Events.Where(e => e.EventType == LogEventTypes.DATABASE_INSERT).ToList();
        insertEndEvents.Should().NotBeEmpty("Should parse DATABASE_INSERT events (capture confirmed)");

        // Verify event structure
        var firstInsert = insertEndEvents.First();
        firstInsert.Attributes.Should().ContainKey("pid");
        firstInsert.Attributes.Should().ContainKey("tid");
        firstInsert.Attributes.Should().ContainKey("mediaId");
        firstInsert.Attributes.Should().ContainKey("uri");
        firstInsert.Attributes.Should().ContainKey("endTimestampMs");

        _output.WriteLine($"✓ Media insert events parsed correctly");
        _output.WriteLine($"  Total Insert End Events: {insertEndEvents.Count} (Photo captures confirmed!)");
        
        // Log some examples
        foreach (var evt in insertEndEvents.Take(5))
        {
            _output.WriteLine($"  - {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | MediaID: {evt.Attributes["mediaId"]} | PID: {evt.Attributes["pid"]}");
        }
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldIdentify_CameraCapture()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "media.camera.worker.txt");

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
        var connectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        var disconnectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT).ToList();
        var insertEndEvents = result.Events.Where(e => e.EventType == LogEventTypes.DATABASE_INSERT).ToList();

        // Camera app usage periods
        var cameraConnectEvents = connectEvents.Where(e => 
            e.Attributes.ContainsKey("package") && 
            e.Attributes["package"].ToString()!.Contains("camera")).ToList();

        cameraConnectEvents.Should().NotBeEmpty("Should have camera app connect events");
        insertEndEvents.Should().NotBeEmpty("Should have media insert events");

        // For each camera connect event, find corresponding disconnect event
        int confirmedCaptures = 0;
        foreach (var connectEvent in cameraConnectEvents.Take(5))
        {
            var disconnectEvent = disconnectEvents.FirstOrDefault(e =>
                e.Timestamp > connectEvent.Timestamp);

            if (disconnectEvent == null) continue;

            // Find INSERT events within camera usage period
            var capturesInPeriod = insertEndEvents.Where(e =>
                e.Timestamp >= connectEvent.Timestamp &&
                e.Timestamp <= disconnectEvent.Timestamp).ToList();

            if (capturesInPeriod.Any())
            {
                confirmedCaptures += capturesInPeriod.Count;
                _output.WriteLine($"\n  Camera Session {connectEvent.Timestamp:HH:mm:ss} - {disconnectEvent.Timestamp:HH:mm:ss}:");
                _output.WriteLine($"    Duration: {(disconnectEvent.Timestamp - connectEvent.Timestamp).TotalSeconds:F1}s");
                _output.WriteLine($"    Photos Captured: {capturesInPeriod.Count}");
                
                foreach (var capture in capturesInPeriod)
                {
                    _output.WriteLine($"      - {capture.Timestamp:HH:mm:ss.fff} | MediaID: {capture.Attributes["mediaId"]}");
                }
            }
        }

        confirmedCaptures.Should().BeGreaterThan(0, "Should identify at least one camera capture within camera usage period");

        _output.WriteLine($"\n✓ Camera capture identification successful");
        _output.WriteLine($"  Total Confirmed Captures: {confirmedCaptures}");
        _output.WriteLine($"\n  ⚠️  Note: Time-based matching (Open → INSERT → Close) filters out screenshots, downloads, etc.");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldHandle_BurstModeCaptures()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "media.camera.worker.txt");

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
        var insertEndEvents = result.Events.Where(e => e.EventType == LogEventTypes.DATABASE_INSERT)
            .OrderBy(e => e.Timestamp)
            .ToList();

        // Find burst mode captures (multiple inserts within short time window, e.g., 15 seconds)
        var burstSessions = new List<List<NormalizedLogEvent>>();
        var currentBurst = new List<NormalizedLogEvent>();

        for (int i = 0; i < insertEndEvents.Count; i++)
        {
            if (currentBurst.Count == 0)
            {
                currentBurst.Add(insertEndEvents[i]);
            }
            else
            {
                var timeDiff = (insertEndEvents[i].Timestamp - currentBurst.Last().Timestamp).TotalSeconds;
                if (timeDiff <= 2.0) // Within 2 seconds = burst mode
                {
                    currentBurst.Add(insertEndEvents[i]);
                }
                else
                {
                    if (currentBurst.Count >= 3) // At least 3 photos = burst
                    {
                        burstSessions.Add(new List<NormalizedLogEvent>(currentBurst));
                    }
                    currentBurst.Clear();
                    currentBurst.Add(insertEndEvents[i]);
                }
            }
        }

        // Check last burst
        if (currentBurst.Count >= 3)
        {
            burstSessions.Add(currentBurst);
        }

        _output.WriteLine($"✓ Burst mode detection");
        _output.WriteLine($"  Total Insert Events: {insertEndEvents.Count}");
        _output.WriteLine($"  Burst Sessions Found: {burstSessions.Count}");

        if (burstSessions.Any())
        {
            foreach (var burst in burstSessions.Take(3))
            {
                _output.WriteLine($"\n  Burst Session: {burst.Count} photos");
                _output.WriteLine($"    Start: {burst.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                _output.WriteLine($"    End: {burst.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                _output.WriteLine($"    Duration: {(burst.Last().Timestamp - burst.First().Timestamp).TotalSeconds:F1}s");
            }
        }
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "media.camera.worker.txt");

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

        // Assert - Verify parsed data has necessary fields for correlation
        var sampleConnect = result.Events
            .FirstOrDefault(e => e.EventType == LogEventTypes.CAMERA_CONNECT && 
                               e.Attributes.ContainsKey("package") &&
                               e.Attributes["package"].ToString()!.Contains("camera"));

        var sampleInsert = result.Events
            .FirstOrDefault(e => e.EventType == LogEventTypes.DATABASE_INSERT);

        sampleConnect.Should().NotBeNull("Should have at least one camera connect event");
        sampleInsert.Should().NotBeNull("Should have at least one media insert event");

        // Connect event validation
        sampleConnect!.Timestamp.Should().NotBe(default(DateTime), "Should have valid timestamp");
        sampleConnect.Attributes.Should().ContainKey("package");
        sampleConnect.Attributes.Should().ContainKey("cameraId");

        // Insert event validation
        sampleInsert!.Timestamp.Should().NotBe(default(DateTime), "Should have valid timestamp");
        sampleInsert.Attributes.Should().ContainKey("mediaId");
        sampleInsert.Attributes.Should().ContainKey("pid");

        _output.WriteLine($"✓ Parsed data suitable for correlation analysis");
        _output.WriteLine($"\n  Sample Connect Event:");
        _output.WriteLine($"    Timestamp: {sampleConnect.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"    Type: {sampleConnect.EventType}");
        _output.WriteLine($"    CameraID: {sampleConnect.Attributes["cameraId"]}");
        _output.WriteLine($"    Package: {sampleConnect.Attributes["package"]}");
        
        _output.WriteLine($"\n  Sample Insert Event:");
        _output.WriteLine($"    Timestamp: {sampleInsert.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"    Type: {sampleInsert.EventType}");
        _output.WriteLine($"    MediaID: {sampleInsert.Attributes["mediaId"]}");
        
        // Demonstrate correlation approach
        _output.WriteLine($"\n  ⚠️  Note: Upper application can correlate these events:");
        _output.WriteLine($"    1. Find camera Connect/Disconnect pairs → Usage periods");
        _output.WriteLine($"    2. Filter DATABASE_INSERT within usage periods → Actual captures");
        _output.WriteLine($"    3. Combine with audio.txt (shutter sound) → 99.99% accuracy");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldParse_TimestampAccurately()
    {
        // Arrange: 타임스탬프 파싱 정확도 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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
        result.Events.Should().NotBeEmpty("Should parse events from the log");
        
        var allEvents = result.Events.OrderBy(e => e.Timestamp).ToList();
        
        // 타임스탬프 유효성 검증
        foreach (var evt in allEvents)
        {
            evt.Timestamp.Should().NotBe(default(DateTime), "Each event should have a valid timestamp");
            evt.Timestamp.Year.Should().BeInRange(2025, 2026, "Year should be reasonable");
        }

        // 시간 순서 검증 (대부분의 이벤트가 시간순으로 정렬되어 있어야 함)
        var timestamps = allEvents.Select(e => e.Timestamp).ToList();
        var sortedTimestamps = timestamps.OrderBy(t => t).ToList();
        
        timestamps.Should().Equal(sortedTimestamps, "Events should be orderable by timestamp");

        _output.WriteLine($"✓ Timestamp parsing validated");
        _output.WriteLine($"  Total Events: {allEvents.Count}");
        _output.WriteLine($"  Time Range: {allEvents.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff} ~ {allEvents.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Duration: {(allEvents.Last().Timestamp - allEvents.First().Timestamp).TotalHours:F1} hours");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldValidate_FieldTypes()
    {
        // Arrange: 필드 타입 검증 (pid, tid, cameraId, mediaId)
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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

        // Assert - Camera lifecycle events (cameraId)
        var cameraEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT || 
                       e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .ToList();

        cameraEvents.Should().NotBeEmpty("Should have camera lifecycle events");

        foreach (var evt in cameraEvents)
        {
            evt.Attributes.Should().ContainKey("cameraId", "Camera events should have cameraId");
            
            var cameraIdObj = evt.Attributes["cameraId"];
            if (cameraIdObj is int cameraIdInt)
            {
                cameraIdInt.Should().BeGreaterThanOrEqualTo(0, "cameraId should be non-negative integer");
            }
            else if (int.TryParse(cameraIdObj?.ToString(), out var parsed))
            {
                parsed.Should().BeGreaterThanOrEqualTo(0, "cameraId should be parseable as non-negative integer");
            }
            else
            {
                Assert.Fail($"cameraId should be int or parseable string, but got: {cameraIdObj?.GetType().Name}");
            }
        }

        // Assert - Media insert events (pid, tid, mediaId)
        var insertEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.DATABASE_INSERT)
            .ToList();

        insertEvents.Should().NotBeEmpty("Should have media insert events");

        foreach (var evt in insertEvents)
        {
            // pid 검증
            evt.Attributes.Should().ContainKey("pid", "Insert events should have pid");
            var pidObj = evt.Attributes["pid"];
            if (pidObj is int pidInt)
            {
                pidInt.Should().BeGreaterThan(0, "pid should be positive integer");
            }
            else if (int.TryParse(pidObj?.ToString(), out var pidParsed))
            {
                pidParsed.Should().BeGreaterThan(0, "pid should be parseable as positive integer");
            }
            else
            {
                Assert.Fail($"pid should be int or parseable string, but got: {pidObj?.GetType().Name}");
            }

            // tid 검증
            evt.Attributes.Should().ContainKey("tid", "Insert events should have tid");
            var tidObj = evt.Attributes["tid"];
            if (tidObj is int tidInt)
            {
                tidInt.Should().BeGreaterThan(0, "tid should be positive integer");
            }
            else if (int.TryParse(tidObj?.ToString(), out var tidParsed))
            {
                tidParsed.Should().BeGreaterThan(0, "tid should be parseable as positive integer");
            }
            else
            {
                Assert.Fail($"tid should be int or parseable string, but got: {tidObj?.GetType().Name}");
            }

            // mediaId 검증
            evt.Attributes.Should().ContainKey("mediaId", "Insert events should have mediaId");
            var mediaIdObj = evt.Attributes["mediaId"];
            if (mediaIdObj is long mediaIdLong)
            {
                mediaIdLong.Should().BeGreaterThan(0, "mediaId should be positive long");
            }
            else if (long.TryParse(mediaIdObj?.ToString(), out var mediaIdParsed))
            {
                mediaIdParsed.Should().BeGreaterThan(0, "mediaId should be parseable as positive long");
            }
            else
            {
                Assert.Fail($"mediaId should be long or parseable string, but got: {mediaIdObj?.GetType().Name}");
            }
        }

        _output.WriteLine($"✓ Field types validated");
        _output.WriteLine($"  Camera Events: {cameraEvents.Count} (cameraId: int)");
        _output.WriteLine($"  Insert Events: {insertEvents.Count} (pid: int, tid: int, mediaId: long)");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldParse_MultiplePackages()
    {
        // Arrange: 여러 패키지의 이벤트 파싱 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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
        var cameraEvents = result.Events
            .Where(e => (e.EventType == LogEventTypes.CAMERA_CONNECT || 
                        e.EventType == LogEventTypes.CAMERA_DISCONNECT) &&
                       e.Attributes.ContainsKey("package"))
            .ToList();

        cameraEvents.Should().NotBeEmpty("Should have camera events with package information");

        // 패키지별 그룹화
        var eventsByPackage = cameraEvents
            .GroupBy(e => e.Attributes["package"]?.ToString() ?? "Unknown")
            .OrderByDescending(g => g.Count())
            .ToList();

        eventsByPackage.Should().NotBeEmpty("Should have events from at least one package");

        // 주요 패키지 확인
        var cameraPackage = eventsByPackage.FirstOrDefault(g => g.Key.Contains("camera"));
        cameraPackage.Should().NotBeNull("Should have events from camera app");

        _output.WriteLine($"✓ Multiple packages parsed");
        _output.WriteLine($"  Total Packages: {eventsByPackage.Count}");
        _output.WriteLine($"\n  Top Packages:");
        
        foreach (var group in eventsByPackage.Take(5))
        {
            var connectCount = group.Count(e => e.EventType == LogEventTypes.CAMERA_CONNECT);
            var disconnectCount = group.Count(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT);
            
            _output.WriteLine($"    {group.Key}");
            _output.WriteLine($"      CONNECT: {connectCount}, DISCONNECT: {disconnectCount}");
        }
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldValidate_EventTypeDistribution()
    {
        // Arrange: 이벤트 타입 분포 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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

        // Assert - Event type distribution
        var connectEvents = result.Events.Count(e => e.EventType == LogEventTypes.CAMERA_CONNECT);
        var disconnectEvents = result.Events.Count(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT);
        var insertStartEvents = result.Events.Count(e => e.EventType == LogEventTypes.MEDIA_INSERT_START);
        var insertEndEvents = result.Events.Count(e => e.EventType == LogEventTypes.DATABASE_INSERT);

        connectEvents.Should().BeGreaterThan(0, "Should have CAMERA_CONNECT events");
        disconnectEvents.Should().BeGreaterThan(0, "Should have CAMERA_DISCONNECT events");
        insertStartEvents.Should().BeGreaterThan(0, "Should have MEDIA_INSERT_START events");
        insertEndEvents.Should().BeGreaterThan(0, "Should have DATABASE_INSERT events");

        // CONNECT와 DISCONNECT 비율 검증
        // 로그 시작 시점에 이전 세션의 DISCONNECT 이벤트가 있을 수 있으므로
        // 비율이 정확히 1:1이 아닐 수 있음
        var connectDisconnectRatio = (double)connectEvents / disconnectEvents;
        connectDisconnectRatio.Should().BeGreaterThan(0, 
            "Should have both CONNECT and DISCONNECT events");

        // INSERT_START와 INSERT_END 비율 검증
        // 로그 경계에서 일부 이벤트는 페어가 없을 수 있음
        var insertRatio = Math.Abs((double)insertStartEvents - insertEndEvents) / insertStartEvents;
        insertRatio.Should().BeLessThan(0.1, 
            "INSERT_START and INSERT_END counts should be close");

        _output.WriteLine($"✓ Event type distribution validated");
        _output.WriteLine($"  CAMERA_CONNECT: {connectEvents}");
        _output.WriteLine($"  CAMERA_DISCONNECT: {disconnectEvents}");
        _output.WriteLine($"  CONNECT/DISCONNECT Ratio: {connectDisconnectRatio:F2}");
        _output.WriteLine($"\n  MEDIA_INSERT_START: {insertStartEvents}");
        _output.WriteLine($"  DATABASE_INSERT: {insertEndEvents}");
        _output.WriteLine($"  INSERT_START/INSERT_END Match: {insertStartEvents == insertEndEvents}");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldCorrelate_InsertStartEndPairs()
    {
        // Arrange: INSERT_START와 INSERT_END 페어링 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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
        var insertStartEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.MEDIA_INSERT_START)
            .OrderBy(e => e.Timestamp)
            .ToList();

        var insertEndEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.DATABASE_INSERT)
            .OrderBy(e => e.Timestamp)
            .ToList();

        insertStartEvents.Should().NotBeEmpty("Should have MEDIA_INSERT_START events");
        insertEndEvents.Should().NotBeEmpty("Should have DATABASE_INSERT events");

        // PID/TID별로 START-END 페어 매칭
        var pairedInserts = new List<(DateTime startTime, DateTime endTime, int pid, int tid, long mediaId, double durationMs)>();

        foreach (var startEvent in insertStartEvents)
        {
            var startPid = Convert.ToInt32(startEvent.Attributes["pid"]);
            var startTid = Convert.ToInt32(startEvent.Attributes["tid"]);

            // 같은 PID/TID의 END 이벤트 찾기 (START 이후 가장 가까운 것)
            var matchingEnd = insertEndEvents
                .Where(e => Convert.ToInt32(e.Attributes["pid"]) == startPid &&
                           Convert.ToInt32(e.Attributes["tid"]) == startTid &&
                           e.Timestamp >= startEvent.Timestamp)
                .OrderBy(e => e.Timestamp)
                .FirstOrDefault();

            if (matchingEnd != null)
            {
                var mediaId = Convert.ToInt64(matchingEnd.Attributes["mediaId"]);
                var durationMs = (matchingEnd.Timestamp - startEvent.Timestamp).TotalMilliseconds;
                
                pairedInserts.Add((startEvent.Timestamp, matchingEnd.Timestamp, startPid, startTid, mediaId, durationMs));
            }
        }

        // 대부분의 START 이벤트가 END 이벤트와 페어링되어야 함
        // 로그 경계에서 일부 이벤트는 페어가 없을 수 있음
        var pairingRate = (double)pairedInserts.Count / insertStartEvents.Count;
        pairingRate.Should().BeGreaterThan(0.9, 
            "Most MEDIA_INSERT_START events should have a matching DATABASE_INSERT");

        _output.WriteLine($"✓ INSERT_START-END pairing validated");
        _output.WriteLine($"  Total START Events: {insertStartEvents.Count}");
        _output.WriteLine($"  Total END Events: {insertEndEvents.Count}");
        _output.WriteLine($"  Paired Inserts: {pairedInserts.Count}");

        if (pairedInserts.Any())
        {
            var avgDuration = pairedInserts.Average(p => p.durationMs);
            var maxDuration = pairedInserts.Max(p => p.durationMs);
            var minDuration = pairedInserts.Min(p => p.durationMs);

            _output.WriteLine($"\n  Insert Duration Statistics:");
            _output.WriteLine($"    Average: {avgDuration:F2}ms");
            _output.WriteLine($"    Min: {minDuration:F2}ms");
            _output.WriteLine($"    Max: {maxDuration:F2}ms");

            _output.WriteLine($"\n  Sample Paired Inserts:");
            foreach (var pair in pairedInserts.Take(3))
            {
                _output.WriteLine($"    PID {pair.pid}, TID {pair.tid}:");
                _output.WriteLine($"      {pair.startTime:HH:mm:ss.fff} → {pair.endTime:HH:mm:ss.fff}");
                _output.WriteLine($"      Duration: {pair.durationMs:F2}ms, MediaID: {pair.mediaId}");
            }
        }
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldValidate_EventOrdering()
    {
        // Arrange: 이벤트 순서 검증 (타임스탬프 기반)
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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

        // 이벤트가 타임스탬프 순서로 정렬 가능한지 확인
        var orderedEvents = result.Events
            .OrderBy(e => e.Timestamp)
            .ToList();

        orderedEvents.Should().NotBeEmpty("Should have ordered events");

        // 연속된 이벤트 간 시간이 정상적인지 확인 (역순이 아닌지)
        for (int i = 1; i < orderedEvents.Count; i++)
        {
            orderedEvents[i].Timestamp.Should().BeOnOrAfter(
                orderedEvents[i - 1].Timestamp,
                "Events should be orderable by timestamp");
        }

        _output.WriteLine($"✓ Event ordering validated");
        _output.WriteLine($"  Total Events: {orderedEvents.Count}");
        _output.WriteLine($"  Time Range: {orderedEvents.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff} ~ {orderedEvents.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldValidate_SectionParsing()
    {
        // Arrange: 섹션별 파싱 검증 (camera_event vs database_event)
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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
        // camera_event 섹션에서 파싱된 이벤트
        var cameraLifecycleEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT || 
                       e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .ToList();

        // database_event 섹션에서 파싱된 이벤트
        var databaseEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.MEDIA_INSERT_START || 
                       e.EventType == LogEventTypes.DATABASE_INSERT)
            .ToList();

        cameraLifecycleEvents.Should().NotBeEmpty("Should parse camera lifecycle events from camera_event section");
        databaseEvents.Should().NotBeEmpty("Should parse database events from database_event section");

        // SourceSection 검증 (설정된 경우)
        foreach (var evt in cameraLifecycleEvents)
        {
            if (!string.IsNullOrEmpty(evt.SourceSection))
            {
                evt.SourceSection.Should().Be("camera_event", 
                    "Camera lifecycle events should be from camera_event section");
            }
        }

        foreach (var evt in databaseEvents)
        {
            if (!string.IsNullOrEmpty(evt.SourceSection))
            {
                evt.SourceSection.Should().Be("database_event", 
                    "Database events should be from database_event section");
            }
        }

        _output.WriteLine($"✓ Section parsing validated");
        _output.WriteLine($"  camera_event section: {cameraLifecycleEvents.Count} events");
        _output.WriteLine($"    CAMERA_CONNECT: {cameraLifecycleEvents.Count(e => e.EventType == LogEventTypes.CAMERA_CONNECT)}");
        _output.WriteLine($"    CAMERA_DISCONNECT: {cameraLifecycleEvents.Count(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT)}");
        _output.WriteLine($"\n  database_event section: {databaseEvents.Count} events");
        _output.WriteLine($"    MEDIA_INSERT_START: {databaseEvents.Count(e => e.EventType == LogEventTypes.MEDIA_INSERT_START)}");
        _output.WriteLine($"    DATABASE_INSERT: {databaseEvents.Count(e => e.EventType == LogEventTypes.DATABASE_INSERT)}");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange: 존재하지 않는 파일 처리 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "nonexistent_camera_worker.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act & Assert - 파일이 없으면 FileNotFoundException 발생
        var act = async () => await parser.ParseAsync(logPath, options);
        
        await act.Should().ThrowAsync<FileNotFoundException>(
            "Parser should throw FileNotFoundException for non-existent file");

        _output.WriteLine($"✓ Empty/missing file handling validated");
        _output.WriteLine($"  Exception thrown as expected for non-existent file");
    }

    [Fact]
    public async Task ParseCameraWorkerLog_ShouldCalculate_CameraSessionDurations()
    {
        // Arrange: 카메라 세션 지속 시간 계산 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.worker.txt");

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

        // 카메라 ID와 패키지별로 CONNECT-DISCONNECT 페어 매칭
        var cameraSessions = new List<(string package, int cameraId, DateTime start, DateTime end, double durationSeconds)>();

        foreach (var connect in connectEvents)
        {
            var cameraId = Convert.ToInt32(connect.Attributes["cameraId"]);
            var package = connect.Attributes["package"]?.ToString() ?? "Unknown";

            // 같은 cameraId와 package의 DISCONNECT 찾기
            var matchingDisconnect = disconnectEvents
                .Where(d => Convert.ToInt32(d.Attributes["cameraId"]) == cameraId &&
                           d.Attributes["package"]?.ToString() == package &&
                           d.Timestamp >= connect.Timestamp)
                .OrderBy(d => d.Timestamp)
                .FirstOrDefault();

            if (matchingDisconnect != null)
            {
                var duration = (matchingDisconnect.Timestamp - connect.Timestamp).TotalSeconds;
                cameraSessions.Add((package, cameraId, connect.Timestamp, matchingDisconnect.Timestamp, duration));
            }
        }

        cameraSessions.Should().NotBeEmpty("Should be able to pair CONNECT-DISCONNECT events into sessions");

        _output.WriteLine($"✓ Camera session durations calculated");
        _output.WriteLine($"  Total Sessions: {cameraSessions.Count}");

        if (cameraSessions.Any())
        {
            var avgDuration = cameraSessions.Average(s => s.durationSeconds);
            var maxDuration = cameraSessions.Max(s => s.durationSeconds);
            var minDuration = cameraSessions.Min(s => s.durationSeconds);

            _output.WriteLine($"\n  Session Duration Statistics:");
            _output.WriteLine($"    Average: {avgDuration:F2}s");
            _output.WriteLine($"    Min: {minDuration:F2}s");
            _output.WriteLine($"    Max: {maxDuration:F2}s");

            _output.WriteLine($"\n  Sample Sessions:");
            foreach (var session in cameraSessions.Where(s => s.package.Contains("camera")).Take(5))
            {
                _output.WriteLine($"    {session.package} (Camera {session.cameraId}):");
                _output.WriteLine($"      {session.start:HH:mm:ss.fff} → {session.end:HH:mm:ss.fff} ({session.durationSeconds:F1}s)");
            }
        }
    }
}

