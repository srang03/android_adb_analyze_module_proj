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
/// Media Metrics 로그 파싱 테스트
/// 카메라 셔터 사운드 재생 이벤트 파싱 검증
/// </summary>
public class MediaMetricsLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public MediaMetricsLogParserTests(ITestOutputHelper output)
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
    public async Task ParseMediaMetricsLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = DateTime.Now
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
        result.Events.Should().NotBeEmpty();
        result.Statistics.ParsedLines.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldParse_ExtractorEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var extractorEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR)
            .ToList();

        _output.WriteLine($"Total Extractor Events: {extractorEvents.Count}");
        
        extractorEvents.Should().NotBeEmpty("extractor events should be parsed");
        
        // 첫 번째 extractor 이벤트 검증
        var firstEvent = extractorEvents.First();
        _output.WriteLine($"First Extractor Event: {firstEvent.Timestamp}, Package: {firstEvent.Attributes.GetValueOrDefault("package")}");
        
        firstEvent.Attributes.Should().ContainKey("package");
        firstEvent.Attributes.Should().ContainKey("lineNumber");
        firstEvent.Attributes.Should().ContainKey("pid");
        firstEvent.Attributes.Should().ContainKey("uid");
        firstEvent.Attributes.Should().ContainKey("attributes_raw");
        
        // 카메라 앱과 관련된 extractor 이벤트 확인
        var cameraExtractorEvents = extractorEvents
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .ToList();
        
        _output.WriteLine($"Camera App Extractor Events: {cameraExtractorEvents.Count}");
        cameraExtractorEvents.Should().NotBeEmpty("camera app should have extractor events for shutter sound");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldParse_AudioTrackEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var audioTrackEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.AUDIO_TRACK)
            .ToList();

        _output.WriteLine($"Total Audio Track Events: {audioTrackEvents.Count}");
        
        audioTrackEvents.Should().NotBeEmpty("audio track events should be parsed");
        
        // 첫 번째 audio.track 이벤트 검증
        var firstEvent = audioTrackEvents.First();
        _output.WriteLine($"First Audio Track Event: {firstEvent.Timestamp}, TrackId: {firstEvent.Attributes.GetValueOrDefault("trackId")}, Package: {firstEvent.Attributes.GetValueOrDefault("package")}");
        
        firstEvent.Attributes.Should().ContainKey("trackId");
        firstEvent.Attributes.Should().ContainKey("package");
        firstEvent.Attributes.Should().ContainKey("lineNumber");
        firstEvent.Attributes.Should().ContainKey("pid");
        firstEvent.Attributes.Should().ContainKey("uid");
        firstEvent.Attributes.Should().ContainKey("attributes_raw");
        
        // 카메라 앱과 관련된 audio track 이벤트 확인
        var cameraAudioTrackEvents = audioTrackEvents
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .ToList();
        
        _output.WriteLine($"Camera App Audio Track Events: {cameraAudioTrackEvents.Count}");
        cameraAudioTrackEvents.Should().NotBeEmpty("camera app should have audio track events for shutter sound playback");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldIdentify_ShutterSoundSequence()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var cameraEvents = result.Events
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .OrderBy(e => e.Timestamp)
            .ToList();

        _output.WriteLine($"Total Camera-related Events: {cameraEvents.Count}");
        
        cameraEvents.Should().NotBeEmpty("camera app should have media events");
        
        // Extractor + Audio Track 이벤트 조합 확인
        var extractorEvents = cameraEvents.Where(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR).ToList();
        var audioTrackEvents = cameraEvents.Where(e => e.EventType == LogEventTypes.AUDIO_TRACK).ToList();
        
        _output.WriteLine($"Camera Extractor Events: {extractorEvents.Count}");
        _output.WriteLine($"Camera Audio Track Events: {audioTrackEvents.Count}");
        
        // 카메라 촬영 시: Extractor (audio/ogg 파일 추출) + Audio Track (셔터 사운드 재생)
        extractorEvents.Should().NotBeEmpty("camera should extract audio/ogg shutter sound file");
        audioTrackEvents.Should().NotBeEmpty("camera should play shutter sound via audio track");
        
        // 첫 번째 촬영 시퀀스 샘플 출력
        if (extractorEvents.Any() && audioTrackEvents.Any())
        {
            var firstExtractor = extractorEvents.First();
            var nearbyAudioTracks = audioTrackEvents
                .Where(at => Math.Abs((at.Timestamp - firstExtractor.Timestamp).TotalSeconds) < 5)
                .Take(3)
                .ToList();
            
            _output.WriteLine($"\n📸 Sample Shutter Sound Sequence:");
            _output.WriteLine($"  Extractor: {firstExtractor.Timestamp:HH:mm:ss.fff}");
            foreach (var track in nearbyAudioTracks)
            {
                _output.WriteLine($"  Audio Track {track.Attributes.GetValueOrDefault("trackId")}: {track.Timestamp:HH:mm:ss.fff}");
            }
        }
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldGroup_EventsByTrackId()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var audioTrackEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.AUDIO_TRACK)
            .ToList();

        // Track ID별로 그룹핑 (상위 앱에서 수행할 작업 시뮬레이션)
        var trackGroups = audioTrackEvents
            .GroupBy(e => e.Attributes.GetValueOrDefault("trackId")?.ToString())
            .Where(g => g.Key != null)
            .OrderBy(g => g.First().Timestamp)
            .ToList();

        _output.WriteLine($"Total Audio Track Groups: {trackGroups.Count}");
        
        trackGroups.Should().NotBeEmpty("should have multiple track groups");
        
        // 각 Track ID는 여러 이벤트를 가질 수 있음 (server.ctor → create → start → stop)
        var cameraTrackGroups = trackGroups
            .Where(g => g.Any(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera"))
            .ToList();
        
        _output.WriteLine($"Camera App Track Groups: {cameraTrackGroups.Count}");
        
        cameraTrackGroups.Should().NotBeEmpty("camera app should have multiple track sessions");
        
        // 샘플 출력: 첫 번째 카메라 track 세션의 이벤트들
        if (cameraTrackGroups.Any())
        {
            var firstTrackGroup = cameraTrackGroups.First();
            var trackId = firstTrackGroup.Key;
            var events = firstTrackGroup.OrderBy(e => e.Attributes.GetValueOrDefault("lineNumber")).ToList();
            
            _output.WriteLine($"\n📊 Sample Camera Track Session (ID: {trackId}):");
            _output.WriteLine($"   Total Events: {events.Count}");
            _output.WriteLine($"   Time Range: {events.First().Timestamp:HH:mm:ss.fff} - {events.Last().Timestamp:HH:mm:ss.fff}");
        }
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        // 상위 앱에서 카메라 촬영을 감지하기 위한 필수 정보 검증
        var cameraEvents = result.Events
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .OrderBy(e => e.Timestamp)
            .ToList();

        _output.WriteLine($"\n✅ Data for Upper-App Correlation:");
        _output.WriteLine($"   Total Camera Events: {cameraEvents.Count}");
        
        // 1. Timestamp: 시간 기반 상관관계 분석
        cameraEvents.Should().OnlyContain(e => e.Timestamp != default, 
            "all events should have valid timestamps for time-based correlation");
        
        // 2. Package: 앱 식별
        cameraEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("package"),
            "all events should have package info for app identification");
        
        // 3. Track ID: 동일한 촬영 세션 그룹핑
        var audioTrackEvents = cameraEvents.Where(e => e.EventType == LogEventTypes.AUDIO_TRACK).ToList();
        audioTrackEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("trackId"),
            "audio track events should have trackId for session grouping");
        
        // 4. Line Number: 이벤트 순서 보장
        cameraEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("lineNumber"),
            "all events should have line numbers for ordering");
        
        // 5. PID/UID: 프로세스 식별
        cameraEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("pid") && e.Attributes.ContainsKey("uid"),
            "all events should have PID/UID for process tracking");
        
        _output.WriteLine($"   ✓ Timestamps: Valid");
        _output.WriteLine($"   ✓ Package Info: Present");
        _output.WriteLine($"   ✓ Track IDs: Present ({audioTrackEvents.Count} audio tracks)");
        _output.WriteLine($"   ✓ Line Numbers: Present");
        _output.WriteLine($"   ✓ PID/UID: Present");
        
        _output.WriteLine($"\n💡 Upper-app can:");
        _output.WriteLine($"   1. Group events by trackId to identify camera shutter sessions");
        _output.WriteLine($"   2. Correlate extractor + audio.track events within time window");
        _output.WriteLine($"   3. Detect camera capture by matching package=com.sec.android.app.camera");
        _output.WriteLine($"   4. Distinguish interrupts using lineNumber and timestamp gaps");
    }
}

