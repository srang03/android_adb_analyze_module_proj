using System.Diagnostics;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Device;
using AndroidAdbAnalyze.Console.Executor.Services.LogCollection;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AndroidAdbAnalyze.Console.Executor.Services.Pipeline;

/// <summary>
/// 전체 파이프라인 서비스 구현체
/// </summary>
public sealed class PipelineService : IPipelineService
{
    private readonly IDeviceManager _deviceManager;
    private readonly ILogCollector _logCollector;
    private readonly IAnalysisOrchestrator _analysisOrchestrator;
    private readonly AnalysisConfiguration _analysisConfig;
    private readonly ILogger<PipelineService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public PipelineService(
        IDeviceManager deviceManager,
        ILogCollector logCollector,
        IAnalysisOrchestrator analysisOrchestrator,
        IOptions<AnalysisConfiguration> analysisConfig,
        ILogger<PipelineService> logger,
        ILoggerFactory loggerFactory)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _logCollector = logCollector ?? throw new ArgumentNullException(nameof(logCollector));
        _analysisOrchestrator = analysisOrchestrator ?? throw new ArgumentNullException(nameof(analysisOrchestrator));
        _analysisConfig = analysisConfig?.Value ?? throw new ArgumentNullException(nameof(analysisConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public async Task<PipelineResult> ExecuteAsync(
        string? outputDirectory = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("=== 파이프라인 실행 시작 ===");
            
            // ========================================
            // Step 1: 디바이스 확인 및 정보 추출
            // ========================================
            progress?.Report("디바이스 연결 확인 중...");
            _logger.LogInformation("[Step 1/4] 디바이스 연결 확인 중...");
            
            var device = await _deviceManager.EnsureSingleDeviceAsync(cancellationToken);
            
            progress?.Report($"디바이스 연결됨: {device.Serial}");
            _logger.LogInformation("디바이스 연결 확인: {Serial} ({Type})", 
                device.Serial, device.ConnectionType);
            
            var deviceInfo = await _deviceManager.ExtractDeviceInfoAsync(device, cancellationToken);
            
            _logger.LogInformation(
                "디바이스 정보: Android {AndroidVersion}, {Manufacturer} {Model}",
                deviceInfo.AndroidVersion, deviceInfo.Manufacturer, deviceInfo.Model);
            
            // ========================================
            // Step 2: 로그 수집
            // ========================================
            progress?.Report("로그 수집 중...");
            _logger.LogInformation("[Step 2/4] 로그 수집 시작...");
            
            var collectionSummary = await _logCollector.CollectAllLogsAsync(
                outputDirectory, 
                cancellationToken);
            
            progress?.Report($"로그 수집 완료: {collectionSummary.SuccessCount}/{collectionSummary.TotalLogs}");
            _logger.LogInformation(
                "로그 수집 완료: {SuccessCount}/{TotalLogs} 성공",
                collectionSummary.SuccessCount, collectionSummary.TotalLogs);
            
            // ========================================
            // Step 3: 로그 파싱
            // ========================================
            progress?.Report("로그 파싱 중...");
            _logger.LogInformation("[Step 3/4] 로그 파싱 시작...");
            
            var parsingStopwatch = Stopwatch.StartNew();
            var parsingResults = new Dictionary<string, ParsingResult>();
            var allEvents = new List<NormalizedLogEvent>();
            
            var successfulLogs = collectionSummary.Results.Where(r => r.Success).ToList();
            
            foreach (var logResult in successfulLogs)
            {
                var logName = logResult.LogDefinition.Name;
                var logFilePath = logResult.FilePath!;
                var parserConfigPath = logResult.LogDefinition.ParserConfig;
                
                _logger.LogDebug("파싱 중: {LogName} - {FilePath} (Config: {ConfigPath})", 
                    logName, logFilePath, parserConfigPath);
                
                try
                {
                    // 각 로그 파일마다 YAML 설정 파일을 로드하여 AdbLogParser 생성
                    var configLoader = new YamlConfigurationLoader(
                        parserConfigPath, 
                        _loggerFactory.CreateLogger<YamlConfigurationLoader>());
                    
                    var logConfig = await configLoader.LoadAsync(parserConfigPath);
                    
                    var logParser = new AdbLogParser(
                        logConfig, 
                        _loggerFactory.CreateLogger<AdbLogParser>());
                    
                    var parsingOptions = new LogParsingOptions
                    {
                        DeviceInfo = deviceInfo,
                        StartTime = startTime,
                        EndTime = endTime,
                        ConvertToUtc = true
                    };
                    
                    var parsingResult = await logParser.ParseAsync(
                        logFilePath, 
                        parsingOptions, 
                        cancellationToken);
                    
                    parsingResults[logName] = parsingResult;
                    
                    if (parsingResult.Success)
                    {
                        allEvents.AddRange(parsingResult.Events);
                        
                        _logger.LogInformation(
                            "파싱 성공: {LogName} - {EventCount}개 이벤트",
                            logName, parsingResult.Events.Count);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "파싱 실패: {LogName} - {ErrorMessage}",
                            logName, parsingResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "파싱 중 예외 발생: {LogName}", logName);
                    
                    parsingResults[logName] = new ParsingResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Exception = ex
                    };
                }
            }
            
            parsingStopwatch.Stop();
            _logger.LogInformation(
                "총 파싱된 이벤트: {TotalEvents}개 (파싱 소요 시간: {ParsingTime:F3}초)", 
                allEvents.Count, parsingStopwatch.Elapsed.TotalSeconds);
            
            // ========================================
            // Step 4: 분석
            // ========================================
            progress?.Report("로그 분석 중...");
            _logger.LogInformation("[Step 4/4] 로그 분석 시작...");
            
            var analysisOptions = new AnalysisOptions
            {
                MinConfidenceThreshold = _analysisConfig.MinConfidenceThreshold,
                EventCorrelationWindow = TimeSpan.FromSeconds(_analysisConfig.EventCorrelationWindowSeconds),
                MaxSessionGap = TimeSpan.FromMinutes(_analysisConfig.MaxSessionGapMinutes),
                DeduplicationSimilarityThreshold = _analysisConfig.DeduplicationSimilarityThreshold
            };
            
            var analysisResult = await _analysisOrchestrator.AnalyzeAsync(
                allEvents,
                analysisOptions,
                cancellationToken: cancellationToken);
            
            _logger.LogInformation(
                "분석 완료: {SessionCount}개 세션, {CaptureCount}개 촬영 이벤트 (분석 소요 시간: {AnalysisTime:F3}초)",
                analysisResult.Sessions.Count, analysisResult.CaptureEvents.Count,
                analysisResult.Statistics.ProcessingTime.TotalSeconds);
            
            // ========================================
            // 결과 반환 (Statistics에 파싱/전체 파이프라인 시간 추가)
            // ========================================
            stopwatch.Stop();
            
            // AnalysisResult는 immutable이므로 Statistics를 업데이트한 새 객체 생성
            var enrichedStatistics = new AndroidAdbAnalyze.Analysis.Models.Results.AnalysisStatistics
            {
                TotalSourceEvents = analysisResult.Statistics.TotalSourceEvents,
                TotalSessions = analysisResult.Statistics.TotalSessions,
                CompleteSessions = analysisResult.Statistics.CompleteSessions,
                IncompleteSessions = analysisResult.Statistics.IncompleteSessions,
                TotalCaptureEvents = analysisResult.Statistics.TotalCaptureEvents,
                DeduplicatedEvents = analysisResult.Statistics.DeduplicatedEvents,
                ProcessingTime = analysisResult.Statistics.ProcessingTime,
                ParsingTime = parsingStopwatch.Elapsed,
                TotalPipelineTime = stopwatch.Elapsed,
                AnalysisStartTime = analysisResult.Statistics.AnalysisStartTime,
                AnalysisEndTime = analysisResult.Statistics.AnalysisEndTime
            };
            
            var enrichedAnalysisResult = new AndroidAdbAnalyze.Analysis.Models.Results.AnalysisResult
            {
                Success = analysisResult.Success,
                Sessions = analysisResult.Sessions,
                CaptureEvents = analysisResult.CaptureEvents,
                SourceEvents = analysisResult.SourceEvents,
                DeduplicationDetails = analysisResult.DeduplicationDetails,
                DeviceInfo = analysisResult.DeviceInfo,
                Statistics = enrichedStatistics,
                Errors = analysisResult.Errors,
                Warnings = analysisResult.Warnings
            };
            
            progress?.Report("파이프라인 실행 완료!");
            _logger.LogInformation(
                "=== 파이프라인 실행 완료 ===\n" +
                "  - 전체 시간: {TotalTime:F2}초\n" +
                "  - 파싱 시간: {ParsingTime:F3}초\n" +
                "  - 분석 시간: {AnalysisTime:F3}초",
                stopwatch.Elapsed.TotalSeconds,
                parsingStopwatch.Elapsed.TotalSeconds,
                analysisResult.Statistics.ProcessingTime.TotalSeconds);
            
            return new PipelineResult
            {
                Success = true,
                DeviceInfo = deviceInfo,
                CollectionSummary = collectionSummary,
                ParsingResults = parsingResults,
                TotalEventCount = allEvents.Count,
                AnalysisResult = enrichedAnalysisResult,
                TotalExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "파이프라인 실행 중 오류 발생");
            
            return new PipelineResult
            {
                Success = false,
                TotalExecutionTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }
}

