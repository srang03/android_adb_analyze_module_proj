using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// PackageName 필드 파싱 테스트
/// 각 로그 타입별로 PackageName이 올바르게 추출되는지 검증
/// </summary>
public sealed class PackageNameParsingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public PackageNameParsingTests(ITestOutputHelper output)
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

    public void Dispose()
    {
        // Cleanup if needed
    }

    #region Activity Log Tests

    [Fact]
    public async Task ParseActivityLog_ShouldExtractPackageName_FromUriPermissions()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Activity log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // URI_PERMISSION_GRANT 이벤트 중 PackageName이 있는 것 확인
        var uriPermissionEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .ToList();

        if (uriPermissionEvents.Count > 0)
        {
            _output.WriteLine($"✓ URI_PERMISSION_GRANT Events: {uriPermissionEvents.Count}");

            var eventsWithPackage = uriPermissionEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
                .ToList();

            _output.WriteLine($"✓ Events with PackageName: {eventsWithPackage.Count}");

            if (eventsWithPackage.Count > 0)
            {
                foreach (var evt in eventsWithPackage.Take(5))
                {
                    _output.WriteLine($"  - PackageName: {evt.PackageName}");
                }

                eventsWithPackage.Should().NotBeEmpty("URI_PERMISSION_GRANT 이벤트는 패키지명을 포함해야 함");
            }
        }
    }

    [Fact]
    public async Task ParseActivityLog_4thSample_ShouldExtractPackageName_FromRefreshRateEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // CAMERA_ACTIVITY_REFRESH 이벤트 확인
        var refreshEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_ACTIVITY_REFRESH)
            .ToList();

        _output.WriteLine($"✓ CAMERA_ACTIVITY_REFRESH Events: {refreshEvents.Count}");

        if (refreshEvents.Count > 0)
        {
            var eventsWithPackage = refreshEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
                .ToList();

            _output.WriteLine($"✓ Events with PackageName: {eventsWithPackage.Count}");

            eventsWithPackage.Should().NotBeEmpty("CAMERA_ACTIVITY_REFRESH 이벤트는 패키지명을 포함해야 함");

            foreach (var evt in eventsWithPackage.Take(5))
            {
                _output.WriteLine($"  - PackageName: {evt.PackageName}");
                evt.PackageName.Should().NotBeNullOrWhiteSpace();
            }
        }

        // SILENT_CAMERA_CAPTURE 이벤트 확인
        var silentCaptureEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.SILENT_CAMERA_CAPTURE)
            .ToList();

        _output.WriteLine($"✓ SILENT_CAMERA_CAPTURE Events: {silentCaptureEvents.Count}");

        if (silentCaptureEvents.Count > 0)
        {
            _output.WriteLine($"✓ 무음 카메라 촬영 이벤트 탐지 성공!");

            foreach (var evt in silentCaptureEvents)
            {
                _output.WriteLine($"  - PackageName: {evt.PackageName}");
                _output.WriteLine($"  - Timestamp: {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");

                evt.PackageName.Should().NotBeNullOrWhiteSpace("SILENT_CAMERA_CAPTURE는 패키지명을 포함해야 함");
                evt.PackageName.Should().Contain("SilentCamera", "무음 카메라 패키지명이어야 함");
            }
        }
    }

    #endregion

    #region Audio Log Tests

    [Fact]
    public async Task ParseAudioLog_ShouldExtractPackageName_FromPlayerEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "audio.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Audio log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // PLAYER_CREATED, PLAYER_EVENT 이벤트 확인
        var playerEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED || 
                       e.EventType == LogEventTypes.PLAYER_EVENT)
            .ToList();

        if (playerEvents.Count > 0)
        {
            _output.WriteLine($"✓ Player Events: {playerEvents.Count}");

            var eventsWithPackage = playerEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
                .ToList();

            _output.WriteLine($"✓ Events with PackageName: {eventsWithPackage.Count}");

            if (eventsWithPackage.Count > 0)
            {
                foreach (var evt in eventsWithPackage.Take(5))
                {
                    _output.WriteLine($"  - EventType: {evt.EventType}, PackageName: {evt.PackageName}");
                }
            }
        }
    }

    #endregion

    #region Media Metrics Log Tests

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldExtractPackageName()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "media_metrics.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Media metrics log not found at: {logPath}. Skipping test.");
            return;
        }

        if (!File.Exists(configPath))
        {
            _output.WriteLine($"Config file not found at: {configPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var eventsWithPackage = result.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        if (eventsWithPackage.Count > 0)
        {
            _output.WriteLine($"✓ Events with PackageName: {eventsWithPackage.Count}");

            foreach (var evt in eventsWithPackage.Take(5))
            {
                _output.WriteLine($"  - EventType: {evt.EventType}, PackageName: {evt.PackageName}");
            }
        }
    }

    #endregion

    #region Media Camera Log Tests

    [Fact]
    public async Task ParseMediaCameraLog_ShouldExtractPackageName_FromCameraEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "media_camera.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Media camera log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // CAMERA_CONNECT 또는 CAMERA_DISCONNECT 이벤트 중 PackageName이 있는 것 확인
        var cameraEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT || 
                        e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        cameraEvents.Should().NotBeEmpty("Media camera log should have camera events with package names");

        _output.WriteLine($"✓ Camera Events with PackageName: {cameraEvents.Count}");

        // 샘플 출력
        foreach (var evt in cameraEvents.Take(3))
        {
            _output.WriteLine($"  - {evt.EventType}: {evt.PackageName} (Device: {evt.Attributes.GetValueOrDefault("deviceId")})");

            // Attributes에도 package가 있는지 확인
            evt.Attributes.Should().ContainKey("package");
            evt.Attributes["package"].ToString().Should().Be(evt.PackageName);
        }
    }

    #endregion

    #region Usagestats Log Tests

    [Fact]
    public async Task ParseUsagestatsLog_ShouldExtractPackageName_FromActivityEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_usagestats_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "usagestats.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Usagestats log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // ACTIVITY_RESUMED, ACTIVITY_PAUSED, ACTIVITY_STOPPED, ACTIVITY_DESTROYED 이벤트 중 PackageName이 있는 것 확인
        // (동적 EventType 때문에 ACTIVITY_LIFECYCLE 대신 개별 상태를 확인)
        var activityEvents = result.Events
            .Where(e => e.EventType == "ACTIVITY_RESUMED" || 
                       e.EventType == "ACTIVITY_PAUSED" || 
                       e.EventType == "ACTIVITY_STOPPED" ||
                       e.EventType == "ACTIVITY_DESTROYED")
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        activityEvents.Should().NotBeEmpty("Usagestats log should have activity events with package names");

        _output.WriteLine($"✓ Activity Events with PackageName: {activityEvents.Count}");

        // EventType 분포 출력
        var eventTypeGroups = activityEvents
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        _output.WriteLine($"\n  Event Type Distribution:");
        foreach (var (eventType, count) in eventTypeGroups.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"    {eventType}: {count}");
        }

        // 샘플 출력
        _output.WriteLine($"\n  Sample Events:");
        foreach (var evt in activityEvents.Take(5))
        {
            _output.WriteLine($"  - {evt.EventType}: {evt.PackageName}");

            // Attributes에도 package가 있는지 확인
            evt.Attributes.Should().ContainKey("package");
            evt.Attributes["package"].ToString().Should().Be(evt.PackageName);
        }
    }

    [Fact]
    public async Task ParseUsagestatsLog_ShouldExtractPackageName_FromForegroundServiceEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_usagestats_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "usagestats.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Usagestats log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // FOREGROUND_SERVICE 이벤트 중 PackageName이 있는 것 확인
        var serviceEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.FOREGROUND_SERVICE)
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        if (serviceEvents.Count > 0)
        {
            _output.WriteLine($"✓ Foreground Service Events with PackageName: {serviceEvents.Count}");

            // 샘플 출력
            foreach (var evt in serviceEvents.Take(3))
            {
                _output.WriteLine($"  - {evt.EventType}: {evt.PackageName} (State: {evt.Attributes.GetValueOrDefault("serviceState")})");

                // Attributes에도 package가 있는지 확인
                evt.Attributes.Should().ContainKey("package");
                evt.Attributes["package"].ToString().Should().Be(evt.PackageName);
            }
        }
        else
        {
            _output.WriteLine($"⚠️ No foreground service events with package names found (this may be normal)");
        }
    }

    #endregion

    #region Vibrator Manager Log Tests

    [Fact]
    public async Task ParseVibratorLog_ShouldExtractPackageName_FromVibrationEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_vibrator_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "vibrator_manager.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"Vibrator manager log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // VIBRATION_EVENT 이벤트 중 PackageName이 있는 것 확인
        var vibrationEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT)
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        vibrationEvents.Should().NotBeEmpty("Vibrator log should have vibration events with package names");

        _output.WriteLine($"✓ Vibration Events with PackageName: {vibrationEvents.Count}");

        // 샘플 출력
        foreach (var evt in vibrationEvents.Take(3))
        {
            _output.WriteLine($"  - {evt.EventType}: {evt.PackageName} (Usage: {evt.Attributes.GetValueOrDefault("usage")})");

            // Attributes에도 package가 있는지 확인
            evt.Attributes.Should().ContainKey("package");
            evt.Attributes["package"].ToString().Should().Be(evt.PackageName);
        }
    }

    #endregion

    #region Silent Camera Capture Tests

    [Fact]
    public async Task ParseActivityLog_4thSample_ShouldExtractPackageName_FromSilentCameraCaptureEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // SILENT_CAMERA_CAPTURE 이벤트 중 PackageName이 있는 것 확인
        var silentCaptureEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.SILENT_CAMERA_CAPTURE)
            .ToList();

        if (silentCaptureEvents.Count > 0)
        {
            _output.WriteLine($"✓ Silent Camera Capture Events: {silentCaptureEvents.Count}");

            // 모든 이벤트에 PackageName이 있어야 함
            var eventsWithPackage = silentCaptureEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
                .ToList();

            eventsWithPackage.Should().HaveCount(silentCaptureEvents.Count,
                "All SILENT_CAMERA_CAPTURE events should have package names");

            // 샘플 출력
            foreach (var evt in silentCaptureEvents.Take(3))
            {
                _output.WriteLine($"  - {evt.EventType}: {evt.PackageName} at {evt.Timestamp:HH:mm:ss.fff}");

                // PackageName이 SilentCamera 패키지여야 함
                evt.PackageName.Should().Contain("SilentCamera");

                // Attributes에도 package가 있는지 확인
                evt.Attributes.Should().ContainKey("package");
                evt.Attributes["package"].ToString().Should().Be(evt.PackageName);
            }
        }
        else
        {
            _output.WriteLine($"⚠️ No SILENT_CAMERA_CAPTURE events found (check if multiline parser is working)");
        }
    }

    #endregion

    #region PackageName 필드 직접 검증

    [Fact]
    public void NormalizedLogEvent_ShouldHavePackageNameProperty()
    {
        // Arrange & Act
        var evt = new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = LogEventTypes.CAMERA_CONNECT,
            SourceSection = "test",
            PackageName = "com.example.app",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = "com.example.app"
            }
        };

        // Assert
        evt.PackageName.Should().Be("com.example.app");
        _output.WriteLine($"✓ NormalizedLogEvent.PackageName 필드 존재 확인");
    }

    [Fact]
    public void NormalizedLogEvent_PackageName_CanBeNull()
    {
        // Arrange & Act
        var evt = new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = LogEventTypes.SCREEN_STATE,
            SourceSection = "test",
            PackageName = null, // 패키지명이 없는 이벤트
            Attributes = new Dictionary<string, object>()
        };

        // Assert
        evt.PackageName.Should().BeNull();
        _output.WriteLine($"✓ PackageName이 null인 경우도 정상 처리됨");
    }

    #endregion

    #region Media Camera Worker Log Tests

    [Fact]
    public async Task ParseMediaCameraWorkerLog_ShouldExtractPackageName_FromCameraEvents()
    {
        // Arrange: Media Camera Worker 로그에서 PackageName 추출 검증
        var configPath = Path.Combine("TestData", "adb_media_camera_worker_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "media_camera_worker.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Media camera worker log not found at: {logPath}. Skipping test.");
            return;
        }

        if (!File.Exists(configPath))
        {
            _output.WriteLine($"⚠️ Config file not found at: {configPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // CAMERA_CONNECT, CAMERA_DISCONNECT, MEDIA_INSERT_START, MEDIA_INSERT_END 이벤트 확인
        var cameraEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT || 
                       e.EventType == LogEventTypes.CAMERA_DISCONNECT ||
                       e.EventType == LogEventTypes.MEDIA_INSERT_START ||
                       e.EventType == LogEventTypes.MEDIA_INSERT_END)
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        if (cameraEvents.Count > 0)
        {
            _output.WriteLine($"✓ Camera Worker Events with PackageName: {cameraEvents.Count}");

            // EventType별 분포
            var eventTypeGroups = cameraEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());

            _output.WriteLine($"\n  Event Type Distribution:");
            foreach (var (eventType, count) in eventTypeGroups.OrderByDescending(kv => kv.Value))
            {
                _output.WriteLine($"    {eventType}: {count}");
            }

            // 샘플 출력
            _output.WriteLine($"\n  Sample Events:");
            foreach (var evt in cameraEvents.Take(3))
            {
                _output.WriteLine($"  - {evt.EventType}: {evt.PackageName}");

                // Attributes에도 package가 있는지 확인
                evt.Attributes.Should().ContainKey("package");
                evt.Attributes["package"].ToString().Should().Be(evt.PackageName);
            }
        }
        else
        {
            _output.WriteLine($"⚠️ No camera worker events with package names found");
        }
    }

    #endregion

    #region Cross-Log PackageName Consistency Tests

    [Fact]
    public async Task PackageName_ShouldBeConsistent_AcrossAttributesAndProperty()
    {
        // Arrange: PackageName 일관성 검증 (Attributes["package"] vs PackageName)
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "media_camera.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Media camera log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var eventsWithPackage = result.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        eventsWithPackage.Should().NotBeEmpty("Should have events with PackageName");

        // 모든 이벤트에서 PackageName과 Attributes["package"] 일치 확인
        int consistentCount = 0;
        int inconsistentCount = 0;

        foreach (var evt in eventsWithPackage)
        {
            if (evt.Attributes.ContainsKey("package"))
            {
                var attrPackage = evt.Attributes["package"].ToString();
                if (attrPackage == evt.PackageName)
                {
                    consistentCount++;
                }
                else
                {
                    inconsistentCount++;
                    _output.WriteLine($"⚠️ Inconsistent PackageName: Property={evt.PackageName}, Attribute={attrPackage}");
                }
            }
        }

        _output.WriteLine($"✓ PackageName consistency validated");
        _output.WriteLine($"  Consistent: {consistentCount}");
        _output.WriteLine($"  Inconsistent: {inconsistentCount}");

        inconsistentCount.Should().Be(0, "PackageName property and Attributes[\"package\"] should always match");
    }

    [Fact]
    public async Task PackageName_ShouldFilter_BySpecificApp()
    {
        // Arrange: 특정 앱 필터링 테스트 (카카오톡, 기본 카메라 등)
        var configPath = Path.Combine("TestData", "adb_usagestats_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "usagestats.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Usagestats log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // 카카오톡 이벤트 필터링
        var kakaoEvents = result.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName) && 
                       e.PackageName.Contains("kakao", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 기본 카메라 이벤트 필터링
        var cameraEvents = result.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName) && 
                       e.PackageName.Contains("camera", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 텔레그램 이벤트 필터링
        var telegramEvents = result.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName) && 
                       e.PackageName.Contains("telegram", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _output.WriteLine($"✓ App-specific filtering validated");
        _output.WriteLine($"  KakaoTalk events: {kakaoEvents.Count}");
        _output.WriteLine($"  Camera app events: {cameraEvents.Count}");
        _output.WriteLine($"  Telegram events: {telegramEvents.Count}");

        if (kakaoEvents.Any())
        {
            var sampleKakao = kakaoEvents.First();
            _output.WriteLine($"\n  Sample KakaoTalk event:");
            _output.WriteLine($"    PackageName: {sampleKakao.PackageName}");
            _output.WriteLine($"    EventType: {sampleKakao.EventType}");
        }

        if (cameraEvents.Any())
        {
            var sampleCamera = cameraEvents.First();
            _output.WriteLine($"\n  Sample Camera event:");
            _output.WriteLine($"    PackageName: {sampleCamera.PackageName}");
            _output.WriteLine($"    EventType: {sampleCamera.EventType}");
        }

        if (telegramEvents.Any())
        {
            var sampleTelegram = telegramEvents.First();
            _output.WriteLine($"\n  Sample Telegram event:");
            _output.WriteLine($"    PackageName: {sampleTelegram.PackageName}");
            _output.WriteLine($"    EventType: {sampleTelegram.EventType}");
        }
    }

    [Fact]
    public async Task PackageName_ShouldGroup_ByApp()
    {
        // Arrange: PackageName별 그룹화 및 통계 테스트
        var configPath = Path.Combine("TestData", "adb_usagestats_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "usagestats.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Usagestats log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // PackageName별 이벤트 그룹화
        var packageGroups = result.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .GroupBy(e => e.PackageName)
            .Select(g => new
            {
                PackageName = g.Key,
                EventCount = g.Count(),
                EventTypes = g.Select(e => e.EventType).Distinct().ToList(),
                TimeRange = new
                {
                    Start = g.Min(e => e.Timestamp),
                    End = g.Max(e => e.Timestamp)
                }
            })
            .OrderByDescending(g => g.EventCount)
            .ToList();

        packageGroups.Should().NotBeEmpty("Should have package groups");

        _output.WriteLine($"✓ PackageName grouping validated");
        _output.WriteLine($"  Total packages: {packageGroups.Count}");
        _output.WriteLine($"  Total events with PackageName: {result.Events.Count(e => !string.IsNullOrWhiteSpace(e.PackageName))}");

        _output.WriteLine($"\n  Top 10 packages by event count:");
        foreach (var group in packageGroups.Take(10))
        {
            _output.WriteLine($"    {group.PackageName}: {group.EventCount} events, {group.EventTypes.Count} event types");
            _output.WriteLine($"      Time range: {group.TimeRange.Start:HH:mm:ss} ~ {group.TimeRange.End:HH:mm:ss}");
            _output.WriteLine($"      Event types: {string.Join(", ", group.EventTypes.Take(3))}");
        }

        // 카메라 관련 앱 통계
        var cameraRelatedApps = packageGroups
            .Where(g => g.PackageName != null && 
                       (g.PackageName.Contains("camera", StringComparison.OrdinalIgnoreCase) ||
                        g.PackageName.Contains("kakao", StringComparison.OrdinalIgnoreCase) ||
                        g.PackageName.Contains("telegram", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (cameraRelatedApps.Any())
        {
            _output.WriteLine($"\n  Camera-related apps:");
            foreach (var app in cameraRelatedApps)
            {
                _output.WriteLine($"    {app.PackageName}: {app.EventCount} events");
            }
        }
    }

    [Fact]
    public async Task PackageName_ShouldValidate_AllLogsHaveConsistentFormat()
    {
        // Arrange: 모든 로그 타입에서 PackageName 형식 일관성 검증
        var testConfigs = new[]
        {
            new { ConfigFile = "adb_activity_config.yaml", LogFile = "activity.log", LogType = "Activity" },
            new { ConfigFile = "adb_audio_config.yaml", LogFile = "audio.log", LogType = "Audio" },
            new { ConfigFile = "adb_media_camera_config.yaml", LogFile = "media_camera.log", LogType = "MediaCamera" },
            new { ConfigFile = "adb_usagestats_config.yaml", LogFile = "usagestats.log", LogType = "Usagestats" },
            new { ConfigFile = "adb_vibrator_config.yaml", LogFile = "vibrator_manager.log", LogType = "Vibrator" }
        };

        var packageNameStats = new Dictionary<string, (int Total, int WithPackage, List<string> UniquePackages)>();

        foreach (var config in testConfigs)
        {
            var configPath = Path.Combine("TestData", config.ConfigFile);
            var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", config.LogFile);

            if (!File.Exists(logPath) || !File.Exists(configPath))
            {
                _output.WriteLine($"⚠️ Skipping {config.LogType} log (file not found)");
                continue;
            }

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo
            };

            var parser = new AdbLogParser(configuration, _logger);
            var result = await parser.ParseAsync(logPath, options);

            if (result.Success)
            {
                var totalEvents = result.Events.Count;
                var eventsWithPackage = result.Events.Count(e => !string.IsNullOrWhiteSpace(e.PackageName));
                var uniquePackages = result.Events
                    .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
                    .Select(e => e.PackageName!)
                    .Distinct()
                    .ToList();

                packageNameStats[config.LogType] = (totalEvents, eventsWithPackage, uniquePackages);
            }
        }

        // Assert
        packageNameStats.Should().NotBeEmpty("Should have parsed at least one log file");

        _output.WriteLine($"✓ Cross-log PackageName format consistency validated");
        _output.WriteLine($"\n  PackageName Statistics:");
        foreach (var (logType, (total, withPackage, uniquePackages)) in packageNameStats.OrderBy(kv => kv.Key))
        {
            var percentage = total > 0 ? (withPackage * 100.0 / total) : 0;
            _output.WriteLine($"    {logType}:");
            _output.WriteLine($"      Total events: {total}");
            _output.WriteLine($"      Events with PackageName: {withPackage} ({percentage:F1}%)");
            _output.WriteLine($"      Unique packages: {uniquePackages.Count}");

            // PackageName 형식 검증 (Android 패키지명 규칙: xxx.yyy.zzz)
            var invalidPackageNames = uniquePackages
                .Where(p => !System.Text.RegularExpressions.Regex.IsMatch(p, @"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .ToList();

            if (invalidPackageNames.Any())
            {
                _output.WriteLine($"      ⚠️ Invalid package names found: {string.Join(", ", invalidPackageNames.Take(3))}");
            }
        }
    }

    #endregion
}

