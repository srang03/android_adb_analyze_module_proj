using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Captures;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Context;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Parser.Configuration;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Reboot;

/// <summary>
/// 재부팅 시점 탐지 기능 통합 테스트
/// 
/// 목적:
/// - CocktailBarService.log 파싱이 정상 작동하는지 확인
/// - 재부팅 이벤트가 AnalysisResult.RebootEvents에 올바르게 포함되는지 검증
/// - 재부팅 이벤트가 Statistics에 정확히 집계되는지 확인
/// - 분석 보고서 출력 시 재부팅 정보가 포함되는지 검증
/// 
/// 테스트 데이터:
/// - sample_logs/시나리오 외 상황/재부팅 로그/CocktailBarService.log
///   - 첫 번째 bootCompleted: 10-18 18:14:44.219
///   - 두 번째 bootCompleted: 10-18 18:14:45.287 (무시됨)
/// </summary>
public sealed class RebootDetectionIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEvents;
    
    // Ground Truth 기준값
    private readonly DateTime _expectedRebootTime = new(2025, 10, 18, 18, 14, 44, 219);
    private const string ExpectedEventType = "DEVICE_BOOT_COMPLETED";
    private const string ExpectedSourceFile = "CocktailBarService.log";
    
    private const string RebootLogDirectory = "시나리오 외 상황/재부팅 로그";

    public RebootDetectionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        var workspaceRoot = Path.Combine("..", "..", "..", "..", "..");
        _sampleLogsPath = Path.Combine(workspaceRoot, "sample_logs");
        _parserConfigPath = Path.Combine(workspaceRoot, "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 재부팅 시점 탐지 기능 통합 테스트 초기화 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _orchestrator = CreateOrchestratorWithYamlConfig();
        _parsedEvents = await ParseRebootLogsAsync();
        
        _output.WriteLine($"📊 파싱된 이벤트 수: {_parsedEvents.Count}");
        
        // 파싱된 DEVICE_BOOT_COMPLETED 이벤트 확인
        var bootEvents = _parsedEvents.Where(e => e.EventType == "DEVICE_BOOT_COMPLETED").ToList();
        _output.WriteLine($"📊 DEVICE_BOOT_COMPLETED 이벤트 수: {bootEvents.Count}");
        
        if (bootEvents.Any())
        {
            _output.WriteLine($"\n파싱된 재부팅 이벤트:");
            foreach (var evt in bootEvents)
            {
                _output.WriteLine($"   - {evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | {evt.EventType} | {evt.SourceFileName}");
            }
        }
        
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region 재부팅 시점 탐지 검증

    [Fact]
    public async Task Should_Detect_RebootEvent_FromCocktailBarLog()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 테스트 1: 재부팅 이벤트 탐지 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("📊 AnalysisResult.RebootEvents 검증:\n");
        
        // 재부팅 이벤트가 정확히 1개 탐지되어야 함
        result.RebootEvents.Should().NotBeNull();
        result.RebootEvents.Count.Should().Be(1, "CocktailBarService.log에서 첫 번째 bootCompleted만 추출");

        var rebootEvent = result.RebootEvents[0];
        
        // 이벤트 타입 검증
        _output.WriteLine($"✅ 이벤트 타입: {rebootEvent.EventType}");
        rebootEvent.EventType.Should().Be(ExpectedEventType);
        
        // 타임스탬프 검증
        _output.WriteLine($"✅ 재부팅 시점: {rebootEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        rebootEvent.Timestamp.Should().Be(_expectedRebootTime, "첫 번째 bootCompleted 시간과 일치");
        
        // 소스 파일 검증
        _output.WriteLine($"✅ 소스 파일: {rebootEvent.SourceFileName}");
        rebootEvent.SourceFileName.Should().Contain(ExpectedSourceFile);
        
        // 원본 라인 검증
        _output.WriteLine($"✅ 원본 라인: {rebootEvent.RawLine}");
        rebootEvent.RawLine.Should().Contain("bootCompleted");
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════\n");
    }

    [Fact]
    public async Task Should_Include_RebootEvent_InStatistics()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 테스트 2: 재부팅 이벤트 통계 집계 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("📊 AnalysisStatistics 검증:\n");
        
        // Statistics에 재부팅 이벤트 개수가 정확히 반영되어야 함
        result.Statistics.Should().NotBeNull();
        
        _output.WriteLine($"✅ TotalRebootEvents: {result.Statistics.TotalRebootEvents}");
        result.Statistics.TotalRebootEvents.Should().Be(1, "재부팅 이벤트 1개가 통계에 반영");
        
        // 기타 통계 정보 확인
        _output.WriteLine($"   TotalSourceEvents: {result.Statistics.TotalSourceEvents}");
        _output.WriteLine($"   TotalSessions: {result.Statistics.TotalSessions}");
        _output.WriteLine($"   TotalCaptureEvents: {result.Statistics.TotalCaptureEvents}");
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════\n");
    }

    [Fact]
    public async Task Should_Output_RebootInformation_InReport()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 테스트 3: 분석 보고서 재부팅 정보 출력 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("📋 분석 결과 요약 (논문용 보고서 형식):\n");
        
        // 1. 재부팅 이벤트 정보
        _output.WriteLine("▶ 재부팅 탐지 결과:");
        _output.WriteLine($"   - 재부팅 횟수: {result.Statistics.TotalRebootEvents}회");
        
        if (result.RebootEvents.Count > 0)
        {
            var reboot = result.RebootEvents[0];
            _output.WriteLine($"   - 재부팅 시점: {reboot.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            _output.WriteLine($"   - 이벤트 타입: {reboot.EventType}");
            _output.WriteLine($"   - 로그 소스: {reboot.SourceFileName}");
            _output.WriteLine($"   - 원본 데이터: {reboot.RawLine?.Trim()}");
        }
        else
        {
            _output.WriteLine($"   ⚠️  CocktailBarService.log 없음");
        }
        
        _output.WriteLine("");
        
        // 2. 세션 정보
        _output.WriteLine("▶ 세션 탐지 결과:");
        _output.WriteLine($"   - 탐지된 세션: {result.Sessions.Count}개");
        
        _output.WriteLine("");
        
        // 3. 촬영 이벤트 정보
        _output.WriteLine("▶ 촬영 탐지 결과:");
        _output.WriteLine($"   - 탐지된 촬영: {result.CaptureEvents.Count}개");
        
        _output.WriteLine("");
        
        // 4. 전체 통계
        _output.WriteLine("▶ 전체 통계:");
        _output.WriteLine($"   - 전체 이벤트: {result.Statistics.TotalSourceEvents}개");
        _output.WriteLine($"   - 중복 제거: {result.Statistics.DeduplicatedEvents}개");
        _output.WriteLine($"   - 재부팅 이벤트: {result.Statistics.TotalRebootEvents}개");
        _output.WriteLine($"   - 세션: {result.Statistics.TotalSessions}개");
        _output.WriteLine($"   - 촬영: {result.Statistics.TotalCaptureEvents}개");
        _output.WriteLine($"   - 분석 소요 시간: {result.Statistics.ProcessingTime.TotalMilliseconds:F0}ms");
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ 재부팅 정보가 분석 보고서에 정상 출력됨");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // Validation
        result.RebootEvents.Count.Should().Be(1);
        result.Statistics.TotalRebootEvents.Should().Be(1);
    }

    [Fact]
    public async Task Should_Compare_RebootTime_WithGroundTruth()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 테스트 4: Ground Truth 비교 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("🔍 Ground Truth 비교:\n");
        
        var actualRebootTime = result.RebootEvents[0].Timestamp;
        
        _output.WriteLine($"   예상 재부팅 시점: {_expectedRebootTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"   실제 탐지 시점:   {actualRebootTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"   일치 여부:       {_expectedRebootTime == actualRebootTime}");
        
        if (_expectedRebootTime == actualRebootTime)
        {
            _output.WriteLine($"\n   ✅ Ground Truth와 완벽히 일치");
        }
        else
        {
            var diff = (actualRebootTime - _expectedRebootTime).TotalMilliseconds;
            _output.WriteLine($"\n   ⚠️  시간 차이: {diff}ms");
        }
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════\n");
        
        // Validation
        actualRebootTime.Should().Be(_expectedRebootTime);
    }

    [Fact]
    public async Task Should_OnlyExtract_FirstBootCompleted()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 테스트 5: 첫 번째 bootCompleted만 추출 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("📊 CocktailBarService.log에는 2개의 bootCompleted가 존재:\n");
        _output.WriteLine("   1. 10-18 18:14:44.219: bootCompleted (첫 번째, 추출됨)");
        _output.WriteLine("   2. 10-18 18:14:45.287: bootCompleted (두 번째, 무시됨)");
        
        _output.WriteLine($"\n✅ 실제 추출된 이벤트 수: {result.RebootEvents.Count}개");
        _output.WriteLine($"✅ 추출된 시점: {result.RebootEvents[0].Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        
        _output.WriteLine("\n📝 설계 의도:");
        _output.WriteLine("   - AnalysisOrchestrator는 .Take(1)로 첫 번째 bootCompleted만 추출");
        _output.WriteLine("   - 중복 재부팅 이벤트를 방지하여 분석 결과의 명확성 확보");
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════\n");
        
        // Validation
        result.RebootEvents.Count.Should().Be(1, "첫 번째 bootCompleted만 추출되어야 함");
        result.RebootEvents[0].Timestamp.Should().Be(_expectedRebootTime, "첫 번째 시간과 일치");
    }

    #endregion

    #region Helper Methods

    private IAnalysisOrchestrator CreateOrchestratorWithYamlConfig()
    {
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions 등록
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        // YAML 설정 로드
        var yamlConfigPath = Path.Combine(_parserConfigPath, "artifact-detection-config.example.yaml");
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<RebootDetectionIntegrationTests>();
        var config = YamlConfigurationLoader.LoadFromFile(yamlConfigPath, logger);
        
        // Configuration을 DI에 등록
        services.AddSingleton(config);
        
        // AndroidAdbAnalysis 서비스 등록 (Configuration 주입)
        RegisterServicesWithConfig(services);
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    private void RegisterServicesWithConfig(IServiceCollection services)
    {
        // ===== Core Services =====
        
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies (Configuration 주입)
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TelegramStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new TelegramStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KakaoTalkStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new KakaoTalkStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BasePatternStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new BasePatternStrategy(logger, calculator, config);
        });
        
        // Capture Detector
        services.AddSingleton<ICaptureDetector, CameraCaptureDetector>();
        
        // Confidence Calculator (Configuration 주입)
        services.AddSingleton<IConfidenceCalculator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConfidenceCalculator>>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new ConfidenceCalculator(logger, config);
        });
        
        // Session Sources
        services.AddSingleton<ISessionSource, UsagestatsSessionSource>();
        services.AddSingleton<ISessionSource, MediaCameraSessionSource>();
        
        // Session Detector
        services.AddSingleton<ISessionDetector, CameraSessionDetector>();
        
        // ===== Deduplication Services =====
        
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // ===== Orchestration =====
        
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    private async Task<List<NormalizedLogEvent>> ParseRebootLogsAsync()
    {
        var rebootLogsPath = Path.Combine(_sampleLogsPath, RebootLogDirectory);
        var allEvents = new List<NormalizedLogEvent>();
        
        // CocktailBarService.log만 파싱
        var logFile = Path.Combine(rebootLogsPath, "CocktailBarService.log");
        
        if (File.Exists(logFile))
        {
            var events = await ParseLogFileAsync(logFile);
            allEvents.AddRange(events);
            _output.WriteLine($"   ✅ {Path.GetFileName(logFile)}: {events.Count}개 이벤트 파싱");
        }
        else
        {
            _output.WriteLine($"   ⚠️  {Path.GetFileName(logFile)}: 파일 없음");
        }
        
        return allEvents;
    }

    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(string filePath)
    {
        // adb_cocktail_config.yaml 매핑
        var configFileName = "adb_cocktail_config.yaml";
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        
        if (!File.Exists(configPath))
        {
            return new List<NormalizedLogEvent>();
        }
        
        // YAML 설정 로드
        var configLoader = new Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var parserConfig = configLoader.Load(configPath);
        
        var parser = new AdbLogParser(parserConfig, NullLogger<AdbLogParser>.Instance);
        
        var options = new LogParsingOptions
        {
            ConvertToUtc = false,
            DeviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = DateTime.Now,
                AndroidVersion = "15",
                Manufacturer = "Samsung",
                Model = "SM-G991N"
            }
        };
        
        var parseResult = await parser.ParseAsync(filePath, options);
        
        return parseResult.Success 
            ? parseResult.Events.ToList() 
            : new List<NormalizedLogEvent>();
    }

    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3,
            CaptureDeduplicationWindow = TimeSpan.FromSeconds(1)
        };
    }

    #endregion
}
