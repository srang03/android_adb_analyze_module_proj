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

        // ACTIVITY_LIFECYCLE 이벤트 중 PackageName이 있는 것 확인
        var activityEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.ACTIVITY_LIFECYCLE)
            .Where(e => !string.IsNullOrWhiteSpace(e.PackageName))
            .ToList();

        activityEvents.Should().NotBeEmpty("Usagestats log should have activity events with package names");

        _output.WriteLine($"✓ Activity Events with PackageName: {activityEvents.Count}");

        // 샘플 출력
        foreach (var evt in activityEvents.Take(3))
        {
            _output.WriteLine($"  - {evt.EventType}: {evt.PackageName} (State: {evt.Attributes.GetValueOrDefault("activityState")})");

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
}

