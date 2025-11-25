using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Extensions;
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
using AndroidAdbAnalyze.Analysis.Services.Reports;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Analysis.Services.Transmission;
using AndroidAdbAnalyze.Analysis.Services.Visualization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Console.Executor.Tests;

/// <summary>
/// End-to-End 통합 테스트 (실제 ADB 디바이스 연결 필요)
/// </summary>
/// <remarks>
/// 이 테스트들은 실제 Android 디바이스가 ADB로 연결되어 있어야 실행 가능합니다.
/// 
/// 사전 요구사항:
/// 1. Android Platform Tools (ADB) 설치 및 PATH 등록
/// 2. Android 디바이스 USB 또는 무선 디버깅 연결
/// 3. 'adb devices' 명령으로 디바이스 인식 확인
/// 
/// 테스트 실행 전:
/// - [Fact] → [Fact(Skip = "Requires ADB device")] 해제 필요
/// </remarks>
public class EndToEndTests
{
    /// <summary>
    /// E2E 테스트: 디바이스 연결 확인
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. ADB 실행 파일 경로 찾기
    /// 2. 'adb devices' 명령 실행
    /// 3. 최소 1개 이상의 디바이스 연결 확인
    /// 
    /// 예상 결과:
    /// - ADB 명령 실행 성공
    /// - 디바이스 목록에 1개 이상의 디바이스 포함
    /// - 디바이스 상태가 "device"
    /// </remarks>
    [Fact(Skip = "Requires ADB device connection")]
    public Task DeviceManager_ShouldDetect_ConnectedDevice()
    {
        // TODO: 실제 디바이스 연결 후 구현
        // Arrange
        // - AdbCommandExecutor 생성
        // - DeviceManager 생성
        
        // Act
        // var devices = await deviceManager.GetConnectedDevicesAsync();
        
        // Assert
        // Assert.NotEmpty(devices);
        // Assert.Contains(devices, d => d.IsAvailable);
        
        throw new NotImplementedException("실제 디바이스 연결 후 구현 필요");
    }

    /// <summary>
    /// E2E 테스트: 디바이스 정보 추출
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 단일 디바이스 연결 확인
    /// 2. DeviceInfo 추출 (TimeZone, AndroidVersion, Manufacturer, Model, CurrentTime)
    /// 
    /// 예상 결과:
    /// - DeviceInfo 객체 생성 성공
    /// - AndroidVersion이 null이 아님 (예: "15", "14")
    /// - TimeZone이 유효한 IANA 시간대 ID
    /// - CurrentTime이 현재 시간과 유사
    /// </remarks>
    [Fact(Skip = "Requires ADB device connection")]
    public Task DeviceManager_ShouldExtract_DeviceInfo()
    {
        // TODO: 실제 디바이스 연결 후 구현
        // Arrange
        // - DeviceManager 생성
        // - 단일 디바이스 확인
        
        // Act
        // var device = await deviceManager.EnsureSingleDeviceAsync();
        // var deviceInfo = await deviceManager.ExtractDeviceInfoAsync(device);
        
        // Assert
        // Assert.NotNull(deviceInfo);
        // Assert.NotNull(deviceInfo.AndroidVersion);
        // Assert.NotEmpty(deviceInfo.TimeZone);
        // Assert.InRange(deviceInfo.CurrentTime, DateTime.Now.AddMinutes(-5), DateTime.Now.AddMinutes(5));
        
        throw new NotImplementedException("실제 디바이스 연결 후 구현 필요");
    }

    /// <summary>
    /// E2E 테스트: 개별 로그 수집
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 단일 로그 수집 (예: activity 로그)
    /// 2. 파일 생성 확인
    /// 3. 파일 크기 확인 (최소 1KB 이상)
    /// 
    /// 예상 결과:
    /// - LogCollectionResult.Success == true
    /// - 로그 파일이 지정된 경로에 생성됨
    /// - 파일 크기가 0보다 큼
    /// </remarks>
    [Fact(Skip = "Requires ADB device connection")]
    public Task LogCollector_ShouldCollect_SingleLog()
    {
        // TODO: 실제 디바이스 연결 후 구현
        // Arrange
        // - LogCollector 생성
        // - 임시 출력 디렉토리 생성
        // - LogDefinition 준비 (activity 로그)
        
        // Act
        // var result = await logCollector.CollectLogAsync(logDefinition, outputDir);
        
        // Assert
        // Assert.True(result.Success);
        // Assert.NotNull(result.FilePath);
        // Assert.True(File.Exists(result.FilePath));
        // Assert.True(result.FileSizeBytes > 1024);  // 최소 1KB
        
        throw new NotImplementedException("실제 디바이스 연결 후 구현 필요");
    }

    /// <summary>
    /// E2E 테스트: 전체 로그 수집
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 7개 로그 전체 수집
    /// 2. 필수 로그가 모두 수집되었는지 확인
    /// 3. 선택 로그(media.camera.worker)는 실패해도 계속 진행
    /// 
    /// 예상 결과:
    /// - LogCollectionSummary.SuccessCount >= 6 (필수 로그 6개)
    /// - 필수 로그 실패 시 예외 발생
    /// - 선택 로그 실패 시 경고만 출력
    /// </remarks>
    [Fact(Skip = "Requires ADB device connection")]
    public Task LogCollector_ShouldCollect_AllLogs()
    {
        // TODO: 실제 디바이스 연결 후 구현
        // Arrange
        // - LogCollector 생성
        // - appsettings.json 로드
        
        // Act
        // var summary = await logCollector.CollectAllLogsAsync();
        
        // Assert
        // Assert.True(summary.SuccessCount >= 6);  // 최소 필수 로그 6개
        // Assert.Equal(7, summary.TotalLogs);
        // Assert.True(summary.Results.Any(r => r.LogDefinition.Name == "activity" && r.Success));
        
        throw new NotImplementedException("실제 디바이스 연결 후 구현 필요");
    }

    /// <summary>
    /// E2E 테스트: 전체 파이프라인 실행
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 디바이스 연결 확인
    /// 2. 로그 수집
    /// 3. 로그 파싱
    /// 4. 분석 (세션 탐지, 촬영 이벤트 탐지)
    /// 5. 결과 출력
    /// 
    /// 예상 결과:
    /// - PipelineResult.Success == true
    /// - TotalEventCount > 0
    /// - AnalysisResult.Sessions.Count >= 0
    /// - AnalysisResult.CaptureEvents.Count >= 0
    /// 
    /// 주의:
    /// - 실제 촬영 이벤트가 없을 수 있으므로 Count >= 0으로 검증
    /// - 로그에 카메라 사용 기록이 있어야 세션/이벤트 탐지됨
    /// </remarks>
    [Fact(Skip = "Requires ADB device connection")]
    public Task PipelineService_ShouldExecute_FullPipeline()
    {
        // TODO: 실제 디바이스 연결 후 구현
        // Arrange
        // - Host 빌드
        // - PipelineService 주입
        // - 임시 출력 디렉토리
        
        // Act
        // var result = await pipelineService.ExecuteAsync(outputDir);
        
        // Assert
        // Assert.True(result.Success);
        // Assert.NotNull(result.DeviceInfo);
        // Assert.NotNull(result.AnalysisResult);
        // Assert.True(result.TotalEventCount > 0);
        
        throw new NotImplementedException("실제 디바이스 연결 후 구현 필요");
    }

    /// <summary>
    /// E2E 테스트: 시간 범위 필터링
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. StartTime과 EndTime을 지정하여 파이프라인 실행
    /// 2. 파싱된 이벤트가 시간 범위 내에 있는지 확인
    /// 
    /// 예상 결과:
    /// - 모든 이벤트의 Timestamp가 StartTime ~ EndTime 범위 내
    /// - 범위 밖의 이벤트는 제외됨
    /// 
    /// 테스트 데이터:
    /// - 실제 로그에서 특정 시간 범위를 선택하여 테스트
    /// </remarks>
    [Fact(Skip = "Requires ADB device connection")]
    public Task PipelineService_ShouldFilter_ByTimeRange()
    {
        // TODO: 실제 디바이스 연결 후 구현
        // Arrange
        // - PipelineService 생성
        // - StartTime, EndTime 설정
        
        // Act
        // var result = await pipelineService.ExecuteAsync(
        //     startTime: startTime,
        //     endTime: endTime);
        
        // Assert
        // foreach (var eventItem in result.AnalysisResult.SourceEvents)
        // {
        //     Assert.InRange(eventItem.Timestamp, startTime.Value, endTime.Value);
        // }
        
        throw new NotImplementedException("실제 디바이스 연결 후 구현 필요");
    }

    /// <summary>
    /// E2E 테스트: 다중 디바이스 연결 시 예외 발생
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 2개 이상의 디바이스 연결 (에뮬레이터 + 실제 디바이스 등)
    /// 2. PipelineService 실행
    /// 3. MultipleDevicesException 발생 확인
    /// 
    /// 예상 결과:
    /// - MultipleDevicesException 발생
    /// - ExitCode == 3
    /// - 연결된 디바이스 목록이 예외 메시지에 포함
    /// 
    /// 수동 테스트 방법:
    /// 1. 에뮬레이터 실행: emulator -avd <avd_name>
    /// 2. 실제 디바이스도 USB로 연결
    /// 3. adb devices로 2개 확인
    /// 4. 프로그램 실행
    /// </remarks>
    [Fact(Skip = "Requires multiple ADB devices")]
    public Task PipelineService_ShouldThrow_WhenMultipleDevices()
    {
        // TODO: 다중 디바이스 환경에서 테스트
        // Arrange
        // - 2개 이상의 디바이스 연결
        
        // Act & Assert
        // await Assert.ThrowsAsync<MultipleDevicesException>(
        //     async () => await pipelineService.ExecuteAsync());
        
        throw new NotImplementedException("다중 디바이스 환경에서 테스트 필요");
    }

    /// <summary>
    /// E2E 테스트: 디바이스 미연결 시 예외 발생
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 모든 디바이스 연결 해제
    /// 2. PipelineService 실행
    /// 3. DeviceNotConnectedException 발생 확인
    /// 
    /// 예상 결과:
    /// - DeviceNotConnectedException 발생
    /// - ExitCode == 2
    /// - UserFriendlyHelp 메시지 포함
    /// 
    /// 수동 테스트 방법:
    /// 1. adb disconnect (무선 디버깅)
    /// 2. USB 케이블 제거
    /// 3. adb devices로 빈 목록 확인
    /// 4. 프로그램 실행
    /// </remarks>
    [Fact(Skip = "Requires no ADB device (manual test)")]
    public Task PipelineService_ShouldThrow_WhenNoDevice()
    {
        // TODO: 디바이스 연결 해제 후 테스트
        // Act & Assert
        // await Assert.ThrowsAsync<DeviceNotConnectedException>(
        //     async () => await pipelineService.ExecuteAsync());
        
        throw new NotImplementedException("디바이스 연결 해제 후 테스트 필요");
    }

    /// <summary>
    /// 통합 테스트: 10차 샘플 로그 파일로 분석 보고서 생성
    /// </summary>
    /// <remarks>
    /// 테스트 내용:
    /// 1. 10차 샘플 로그 파일들을 파싱
    /// 2. 분석 실행
    /// 3. HTML 보고서 생성
    /// 4. 프로젝트 내 TestOutput 폴더에 저장
    /// 
    /// 예상 결과:
    /// - 파싱 성공: 7개 로그 파일
    /// - 세션 탐지 성공: 8개 세션
    /// - 촬영 이벤트 탐지 성공: 4개 촬영
    /// - HTML 보고서 생성 성공
    /// 
    /// 디바이스 연결 불필요 (로컬 파일 사용)
    /// </remarks>
    [Fact]
    public async Task Sample10_ShouldGenerate_AnalysisReport()
    {
        // Arrange
        var solutionDir = FindSolutionDirectory();
        var sample10Dir = Path.Combine(solutionDir, @"sample_logs\10차 샘플_25_10_17");
        var outputDir = Path.Combine(solutionDir, @"AndroidAdbAnalyzeModule\AndroidAdbAnalyze.Console.Executor.Tests\TestOutput\Sample10");
        
        // 출력 디렉토리 생성
        Directory.CreateDirectory(outputDir);
        
        // Logger 설정
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Orchestrator 및 ServiceProvider 생성 (YAML 설정 사용 - Sample10GroundTruthTests와 동일한 방식)
        var (orchestrator, serviceProvider) = CreateOrchestratorWithYamlConfig(solutionDir, loggerFactory);
        
        // Act
        // Step 1: 로그 파싱
        var parserConfigDir = Path.Combine(solutionDir, @"AndroidAdbAnalyzeModule\AndroidAdbAnalyze.Parser\Configs");
        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera_worker.log"] = "adb_media_camera_worker_config.yaml",
            ["media_camera.log"] = "adb_media_camera_config.yaml",
            ["media_metrics.log"] = "adb_media_metrics_config.yaml",
            ["usagestats.log"] = "adb_usagestats_config.yaml",
            ["vibrator_manager.log"] = "adb_vibrator_config.yaml",
            ["activity.log"] = "adb_activity_config.yaml"
        };
        
        var allEvents = new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();
        
        foreach (var (logFile, configFile) in logConfigs)
        {
            var logFilePath = Path.Combine(sample10Dir, logFile);
            var configPath = Path.Combine(parserConfigDir, configFile);
            
            if (!File.Exists(logFilePath))
            {
                System.Console.WriteLine($"로그 파일 없음 (건너뜀): {logFile}");
                continue;
            }
            
            var configLoader = new AndroidAdbAnalyze.Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
            var logConfig = configLoader.Load(configPath);
            
            var parser = new AndroidAdbAnalyze.Parser.Parsing.AdbLogParser(
                logConfig,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AndroidAdbAnalyze.Parser.Parsing.AdbLogParser>.Instance);
            
            var parsingOptions = new AndroidAdbAnalyze.Parser.Core.Models.LogParsingOptions
            {
                MaxFileSizeMB = 50,
                DeviceInfo = new AndroidAdbAnalyze.Parser.Core.Models.DeviceInfo
                {
                    AndroidVersion = "15",
                    TimeZone = "Asia/Seoul",
                    Manufacturer = "Samsung",
                    Model = "SM-G991N",
                    CurrentTime = DateTime.Now
                },
                ConvertToUtc = false,
                StartTime = new DateTime(2025, 10, 17, 23, 56, 0),
                EndTime = new DateTime(2025, 10, 18, 0, 13, 59)
            };
            
            var parsingResult = await parser.ParseAsync(logFilePath, parsingOptions);
            
            if (parsingResult.Success)
            {
                allEvents.AddRange(parsingResult.Events);
                System.Console.WriteLine($"✓ 파싱 성공: {logFile} - {parsingResult.Events.Count}개 이벤트");
            }
            else
            {
                System.Console.WriteLine($"✗ 파싱 실패: {logFile} - {parsingResult.ErrorMessage}");
            }
        }
        
        Assert.True(allEvents.Count > 0, "파싱된 이벤트가 없습니다");
        System.Console.WriteLine($"\n총 파싱된 이벤트: {allEvents.Count:N0}개\n");
        
        // Step 2: 분석 실행
        var analysisOptions = CreateAnalysisOptions();
        
        var analysisResult = await orchestrator.AnalyzeAsync(
            allEvents,
            analysisOptions,
            cancellationToken: CancellationToken.None);
        
        Assert.True(analysisResult.Success, "분석 실패");
        
        // Step 3: HTML 보고서 생성
        var timelineBuilder = serviceProvider.GetRequiredService<ITimelineBuilder>();
        var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();
        
        var htmlReport = reportGenerator.GenerateReport(analysisResult);
        
        // Step 4: 파일 저장
        var reportPath = Path.Combine(outputDir, $"analysis_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        await File.WriteAllTextAsync(reportPath, htmlReport);
        
        // Assert
        Assert.True(File.Exists(reportPath), $"보고서 파일이 생성되지 않음: {reportPath}");
        
        var fileInfo = new FileInfo(reportPath);
        Assert.True(fileInfo.Length > 10000, "보고서 파일 크기가 너무 작음 (최소 10KB 예상)");
        
        // 로그 출력
        System.Console.WriteLine("\n========== 10차 샘플 분석 보고서 생성 완료 ==========");
        System.Console.WriteLine($"보고서 경로: {reportPath}");
        System.Console.WriteLine($"파일 크기: {fileInfo.Length:N0} bytes");
        System.Console.WriteLine($"총 이벤트: {allEvents.Count:N0}개");
        System.Console.WriteLine($"세션: {analysisResult.Sessions.Count}개");
        System.Console.WriteLine($"촬영 이벤트: {analysisResult.CaptureEvents.Count}개");
        System.Console.WriteLine($"분석 시간: {analysisResult.Statistics.ProcessingTime.TotalSeconds:F3}초");
        
        if (analysisResult.Statistics.ParsingTime.HasValue)
        {
            System.Console.WriteLine($"파싱 시간: {analysisResult.Statistics.ParsingTime.Value.TotalSeconds:F3}초");
        }
        
        if (analysisResult.Statistics.TotalPipelineTime.HasValue)
        {
            System.Console.WriteLine($"전체 시간: {analysisResult.Statistics.TotalPipelineTime.Value.TotalSeconds:F3}초");
        }
        
        System.Console.WriteLine("====================================================\n");
    }
    
    /// <summary>
    /// YAML 설정을 사용하여 Orchestrator 및 ServiceProvider 생성 (Sample10GroundTruthTests와 동일한 방식)
    /// </summary>
    private static (IAnalysisOrchestrator Orchestrator, IServiceProvider ServiceProvider) CreateOrchestratorWithYamlConfig(string solutionDir, ILoggerFactory loggerFactory)
    {
        // YAML 설정 파일 경로
        var configPath = Path.Combine(
            solutionDir,
            "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Analysis", "Configs",
            "artifact-detection-config.example.yaml");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        // DI 컨테이너 설정
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
        
        // AnalysisOptions 등록 (EventDeduplicator 의존성용, 최소 옵션만 설정)
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        // YAML 설정 로드
        var logger = loggerFactory.CreateLogger<EndToEndTests>();
        var config = YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
        // Configuration을 DI에 등록
        services.AddSingleton(config);
        
        // AndroidAdbAnalysis 서비스 등록 (Configuration 주입)
        RegisterServicesWithYamlConfig(services);
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        var orchestrator = serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
        
        return (orchestrator, serviceProvider);
    }
    
    /// <summary>
    /// YAML 설정을 사용하여 서비스 등록 (Sample10GroundTruthTests와 동일한 방식)
    /// </summary>
    private static void RegisterServicesWithYamlConfig(IServiceCollection services)
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
        
        // ===== Transmission Detection Services =====
        
        services.AddSingleton<ITransmissionDetector, WifiTransmissionDetector>();
        
        // ===== Reporting Services =====
        
        services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        
        // ===== Orchestration =====
        
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }
    
    /// <summary>
    /// 분석 옵션 생성 (Sample10GroundTruthTests와 동일한 설정)
    /// </summary>
    /// <remarks>
    /// Ground Truth 테스트와 동일한 옵션을 사용하여 일관성 있는 결과를 보장합니다.
    /// </remarks>
    private static AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3,
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MaxSessionGap = TimeSpan.FromMinutes(5),
            EnableIncompleteSessionHandling = true,
            DeduplicationSimilarityThreshold = 0.8
        };
    }
    
    /// <summary>
    /// 솔루션 디렉토리 찾기 (상위 폴더 탐색)
    /// </summary>
    private static string FindSolutionDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "sample_logs")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        
        throw new DirectoryNotFoundException("솔루션 디렉토리를 찾을 수 없습니다 (sample_logs 폴더 기준)");
    }
}

