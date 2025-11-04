using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Sessions;

/// <summary>
/// Sample 6 실제 로그 파일로 CameraDeviceIds 추출 검증
/// </summary>
public sealed class Sample6DeviceIdIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public Sample6DeviceIdIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Sample6_MediaCamera_ShouldExtractCameraDeviceIds()
    {
        // Arrange
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "6차 샘플_25_10_16", "media_camera.log");
        var configPath = Path.Combine("..", "..", "..", "..", "..", "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs", "adb_media_camera_config.yaml");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Log file not found: {logPath}");
            return; // Skip test if file not found
        }

        _output.WriteLine($"✓ Log file found: {logPath}");
        _output.WriteLine($"✓ Config file: {configPath}");

        // Parse logs
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        var configLogger = loggerFactory.CreateLogger<YamlConfigurationLoader>();
        var parserLogger = loggerFactory.CreateLogger<AdbLogParser>();

        var configLoader = new YamlConfigurationLoader(configPath, configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, parserLogger);

        var parsingOptions = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        var parseResult = await parser.ParseAsync(logPath, parsingOptions);

        // Assert parsing
        parseResult.Success.Should().BeTrue("Parser should succeed");
        parseResult.Events.Should().NotBeEmpty("Should have parsed events");

        _output.WriteLine($"\n=== Parser Results ===");
        _output.WriteLine($"Total Events: {parseResult.Events.Count}");
        
        // Check parsed events have deviceId
        var connectEvents = parseResult.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        connectEvents.Should().NotBeEmpty("Should have CAMERA_CONNECT events");

        _output.WriteLine($"CAMERA_CONNECT Events: {connectEvents.Count}");
        
        foreach (var evt in connectEvents.Take(5))
        {
            _output.WriteLine($"  Event {evt.EventId}:");
            _output.WriteLine($"    Timestamp: {evt.Timestamp:HH:mm:ss.fff}");
            _output.WriteLine($"    Attributes: {string.Join(", ", evt.Attributes.Select(kv => $"{kv.Key}={kv.Value}"))}");
            
            evt.Attributes.Should().ContainKey("deviceId", "CAMERA_CONNECT should have deviceId");
            evt.Attributes.Should().ContainKey("package", "CAMERA_CONNECT should have package");
            evt.Attributes.Should().ContainKey("pid", "CAMERA_CONNECT should have pid");
        }

        // Act: Extract sessions using MediaCameraSessionSource
        var sourceLogger = loggerFactory.CreateLogger<MediaCameraSessionSource>();
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        var source = new MediaCameraSessionSource(sourceLogger, confidenceCalculator);

        var analysisOptions = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3,
            EventCorrelationWindow = TimeSpan.FromSeconds(30)
        };

        var sessions = source.ExtractSessions(parseResult.Events.ToList(), analysisOptions);

        // Assert sessions
        sessions.Should().NotBeEmpty("Should extract sessions");

        _output.WriteLine($"\n=== Session Extraction Results ===");
        _output.WriteLine($"Total Sessions: {sessions.Count}");

        foreach (var session in sessions)
        {
            _output.WriteLine($"\nSession {session.SessionId}:");
            _output.WriteLine($"  Package: {session.PackageName}");
            _output.WriteLine($"  Start: {session.StartTime:HH:mm:ss}");
            _output.WriteLine($"  End: {session.EndTime:HH:mm:ss}");
            _output.WriteLine($"  CameraDeviceIds: {(session.CameraDeviceIds != null ? string.Join(", ", session.CameraDeviceIds) : "NULL ❌")}");
            _output.WriteLine($"  SourceEventIds: {session.SourceEventIds.Count}");
        }

        // Assert CameraDeviceIds
        var sessionsWithDeviceIds = sessions.Where(s => s.CameraDeviceIds != null).ToList();
        _output.WriteLine($"\nSessions with CameraDeviceIds: {sessionsWithDeviceIds.Count} / {sessions.Count}");

        sessionsWithDeviceIds.Should().NotBeEmpty("At least some sessions should have CameraDeviceIds");
    }

    [Fact]
    public async Task Sample6_WithSessionDetector_ShouldPreserveCameraDeviceIds()
    {
        // Arrange: Parse media_camera.log AND usagestats.log
        var mediaCameraLogPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "6차 샘플_25_10_16", "media_camera.log");
        var usagestatsLogPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "6차 샘플_25_10_16", "usagestats.log");
        var mediaCameraConfigPath = Path.Combine("..", "..", "..", "..", "..", "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs", "adb_media_camera_config.yaml");
        var usagestatsConfigPath = Path.Combine("..", "..", "..", "..", "..", "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs", "adb_usagestats_config.yaml");

        if (!File.Exists(mediaCameraLogPath) || !File.Exists(usagestatsLogPath))
        {
            _output.WriteLine($"⚠️ Log files not found");
            return;
        }

        // Parse both logs
        var loggerFactory = NullLoggerFactory.Instance;
        
        // Parse media_camera.log
        var mediaCameraConfigLoader = new YamlConfigurationLoader(mediaCameraConfigPath, loggerFactory.CreateLogger<YamlConfigurationLoader>());
        var mediaCameraConfig = await mediaCameraConfigLoader.LoadAsync(mediaCameraConfigPath);
        var mediaCameraParser = new AdbLogParser(mediaCameraConfig, loggerFactory.CreateLogger<AdbLogParser>());
        
        var parsingOptions = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };
        
        var mediaCameraResult = await mediaCameraParser.ParseAsync(mediaCameraLogPath, parsingOptions);
        
        // Parse usagestats.log
        var usagestatsConfigLoader = new YamlConfigurationLoader(usagestatsConfigPath, loggerFactory.CreateLogger<YamlConfigurationLoader>());
        var usagestatsConfig = await usagestatsConfigLoader.LoadAsync(usagestatsConfigPath);
        var usagestatsParser = new AdbLogParser(usagestatsConfig, loggerFactory.CreateLogger<AdbLogParser>());
        var usagestatsResult = await usagestatsParser.ParseAsync(usagestatsLogPath, parsingOptions);

        _output.WriteLine($"media_camera events: {mediaCameraResult.Events.Count}");
        _output.WriteLine($"usagestats events: {usagestatsResult.Events.Count}");

        // Act: Use CameraSessionDetector with both sources
        var confidenceCalculator = new ConfidenceCalculator(loggerFactory.CreateLogger<ConfidenceCalculator>());
        var mediaCameraSource = new MediaCameraSessionSource(loggerFactory.CreateLogger<MediaCameraSessionSource>(), confidenceCalculator);
        var usagestatsSource = new UsagestatsSessionSource(loggerFactory.CreateLogger<UsagestatsSessionSource>(), confidenceCalculator);

        var sessionDetector = new CameraSessionDetector(
            loggerFactory.CreateLogger<CameraSessionDetector>(),
            confidenceCalculator,
            new ISessionSource[] { usagestatsSource, mediaCameraSource });

        var analysisOptions = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3,
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MaxSessionGap = TimeSpan.FromMinutes(5),
            EnableIncompleteSessionHandling = true
        };

        // Combine all events and apply time filter
        var allEvents = mediaCameraResult.Events.Concat(usagestatsResult.Events).ToList();
        var startTime = new DateTime(2025, 10, 16, 16, 34, 0);
        var endTime = new DateTime(2025, 10, 16, 16, 49, 0);
        var filteredEvents = allEvents
            .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
            .ToList();

        var sessions = sessionDetector.DetectSessions(filteredEvents, analysisOptions);

        // Assert
        _output.WriteLine($"\n=== CameraSessionDetector Results ===");
        _output.WriteLine($"Total Sessions: {sessions.Count}");

        foreach (var session in sessions)
        {
            _output.WriteLine($"\nSession {session.SessionId}:");
            _output.WriteLine($"  Package: {session.PackageName}");
            _output.WriteLine($"  Start: {session.StartTime:HH:mm:ss}");
            _output.WriteLine($"  End: {session.EndTime:HH:mm:ss}");
            _output.WriteLine($"  Sources: {string.Join(", ", session.SourceLogTypes)}");
            _output.WriteLine($"  CameraDeviceIds: {(session.CameraDeviceIds != null ? string.Join(", ", session.CameraDeviceIds) : "NULL ❌")}");
        }

        var sessionsWithDeviceIds = sessions.Where(s => s.CameraDeviceIds != null && s.CameraDeviceIds.Count > 0).ToList();
        _output.WriteLine($"\n⭐ Sessions with CameraDeviceIds: {sessionsWithDeviceIds.Count} / {sessions.Count}");

        if (sessionsWithDeviceIds.Count == 0)
        {
            _output.WriteLine("\n❌❌❌ CameraDeviceIds가 병합 후 모두 null이 됨! ❌❌❌");
            
            // 디버깅: 병합 전 세션 확인
            var mediaCameraSessions = mediaCameraSource.ExtractSessions(mediaCameraResult.Events.ToList(), analysisOptions);
            _output.WriteLine($"\n=== 병합 전 MediaCamera 세션 ===");
            foreach (var s in mediaCameraSessions.Take(3))
            {
                _output.WriteLine($"  {s.PackageName}: CameraDeviceIds={string.Join(", ", s.CameraDeviceIds ?? new List<int>())}");
            }
        }

        sessionsWithDeviceIds.Should().NotBeEmpty("병합 후에도 CameraDeviceIds가 보존되어야 함");
    }
}

