using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Output.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AndroidAdbAnalyze.Console.Executor.Services.Output;

/// <summary>
/// 분석 결과 저장 서비스 구현체
/// </summary>
/// <remarks>
/// 이 서비스는 완전히 캡슐화되어 있으며, 다음 원칙을 준수합니다:
/// 1. 외부 비즈니스 로직 수정 없음 (PipelineService, Analysis 모듈 영향 없음)
/// 2. 기존 구현된 기능 재사용 (IReportGenerator for HTML)
/// 3. 테스트 코드에서 검증된 파일 I/O 패턴 사용
/// 4. 날짜/시간 기반 폴더 자동 생성
/// </remarks>
public sealed class ResultOutputService : IResultOutputService
{
    private readonly IReportGenerator _reportGenerator;
    private readonly OutputConfiguration _config;
    private readonly ILogger<ResultOutputService> _logger;
    
    // JSON 직렬화 옵션
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public ResultOutputService(
        IReportGenerator reportGenerator,
        IOptions<OutputConfiguration> config,
        ILogger<ResultOutputService> logger)
    {
        _reportGenerator = reportGenerator ?? throw new ArgumentNullException(nameof(reportGenerator));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OutputSummary> SaveResultsAsync(
        PipelineResult result,
        string? baseOutputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        try
        {
            _logger.LogInformation("=== 결과 저장 시작 ===");
            
            // 1. 날짜/시간 기반 폴더 생성
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var outputDir = Path.Combine(baseOutputDirectory ?? "./logs", timestamp);
            
            _logger.LogInformation("출력 디렉토리 생성: {OutputDirectory}", outputDir);
            Directory.CreateDirectory(outputDir);
            
            var savedFiles = new List<string>();
            
            // 2. 원본 로그를 raw_logs로 이동
            string? rawLogsDir = null;
            if (result.CollectionSummary != null)
            {
                rawLogsDir = await MoveRawLogsAsync(result.CollectionSummary, outputDir, cancellationToken);
                if (rawLogsDir != null)
                {
                    savedFiles.Add(rawLogsDir);
                }
            }
            
            // 3. JSON 파일 저장
            if (result.AnalysisResult != null)
            {
                var jsonFiles = await SaveJsonFilesAsync(result, outputDir, cancellationToken);
                savedFiles.AddRange(jsonFiles);
            }
            
            // 4. HTML 보고서 생성 (이미 구현된 IReportGenerator 사용)
            string? htmlPath = null;
            if (_config.GenerateHtmlReport && result.AnalysisResult != null)
            {
                htmlPath = await GenerateHtmlReportAsync(result.AnalysisResult, outputDir, cancellationToken);
                if (htmlPath != null)
                {
                    savedFiles.Add(htmlPath);
                }
            }
            
            _logger.LogInformation("=== 결과 저장 완료: 총 {FileCount}개 파일 생성 ===", savedFiles.Count);
            
            return new OutputSummary
            {
                Success = true,
                OutputDirectory = outputDir,
                HtmlReportPath = htmlPath,
                JsonFilePaths = savedFiles.Where(f => f.EndsWith(".json")).ToList(),
                RawLogsDirectory = rawLogsDir,
                TotalFilesCreated = savedFiles.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "결과 저장 중 오류 발생");
            
            return new OutputSummary
            {
                Success = false,
                OutputDirectory = baseOutputDirectory ?? "./logs",
                ErrorMessage = ex.Message,
                TotalFilesCreated = 0
            };
        }
    }

    /// <summary>
    /// 원본 로그를 raw_logs 하위 폴더로 이동
    /// </summary>
    private async Task<string?> MoveRawLogsAsync(
        LogCollectionSummary summary,
        string outputDir,
        CancellationToken cancellationToken)
    {
        try
        {
            var rawLogsDir = Path.Combine(outputDir, "raw_logs");
            Directory.CreateDirectory(rawLogsDir);
            
            _logger.LogDebug("원본 로그 이동: {SourceDir} → {TargetDir}", 
                summary.OutputDirectory, rawLogsDir);
            
            int movedCount = 0;
            
            foreach (var logResult in summary.Results.Where(r => r.Success && r.FilePath != null))
            {
                var sourceFile = logResult.FilePath!;
                if (File.Exists(sourceFile))
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var targetFile = Path.Combine(rawLogsDir, fileName);
                    
                    // 원본 로그 복사 (이동 대신 복사로 안전성 확보)
                    await Task.Run(() => File.Copy(sourceFile, targetFile, overwrite: true), cancellationToken);
                    movedCount++;
                    
                    _logger.LogDebug("  ✓ {FileName} 복사됨", fileName);
                }
            }
            
            _logger.LogInformation("원본 로그 {Count}개 파일 복사 완료", movedCount);
            return rawLogsDir;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "원본 로그 이동 중 오류 발생 (계속 진행)");
            return null;
        }
    }

    /// <summary>
    /// JSON 파일 저장
    /// </summary>
    private async Task<List<string>> SaveJsonFilesAsync(
        PipelineResult result,
        string outputDir,
        CancellationToken cancellationToken)
    {
        var savedFiles = new List<string>();
        
        try
        {
            _logger.LogDebug("JSON 파일 저장 시작");
            
            // 1. device_info.json
            if (result.DeviceInfo != null)
            {
                var deviceInfoPath = Path.Combine(outputDir, "device_info.json");
                await SaveJsonAsync(result.DeviceInfo, deviceInfoPath, cancellationToken);
                savedFiles.Add(deviceInfoPath);
                _logger.LogDebug("  ✓ device_info.json 저장");
            }
            
            // 2. analysis_result.json (전체 분석 결과)
            if (result.AnalysisResult != null && _config.SaveAnalysisResult)
            {
                var analysisPath = Path.Combine(outputDir, "analysis_result.json");
                await SaveJsonAsync(result.AnalysisResult, analysisPath, cancellationToken);
                savedFiles.Add(analysisPath);
                _logger.LogDebug("  ✓ analysis_result.json 저장");
            }
            
            // 3. sessions.json (세션 목록만)
            if (result.AnalysisResult?.Sessions != null && result.AnalysisResult.Sessions.Any())
            {
                var sessionsPath = Path.Combine(outputDir, "sessions.json");
                await SaveJsonAsync(result.AnalysisResult.Sessions, sessionsPath, cancellationToken);
                savedFiles.Add(sessionsPath);
                _logger.LogDebug("  ✓ sessions.json 저장 ({Count}개 세션)", 
                    result.AnalysisResult.Sessions.Count);
            }
            
            // 4. capture_events.json (촬영 이벤트 목록)
            if (result.AnalysisResult?.CaptureEvents != null && result.AnalysisResult.CaptureEvents.Any())
            {
                var capturesPath = Path.Combine(outputDir, "capture_events.json");
                await SaveJsonAsync(result.AnalysisResult.CaptureEvents, capturesPath, cancellationToken);
                savedFiles.Add(capturesPath);
                _logger.LogDebug("  ✓ capture_events.json 저장 ({Count}개 이벤트)", 
                    result.AnalysisResult.CaptureEvents.Count);
            }
            
            // 5. statistics.json
            if (result.AnalysisResult?.Statistics != null)
            {
                var statsPath = Path.Combine(outputDir, "statistics.json");
                await SaveJsonAsync(result.AnalysisResult.Statistics, statsPath, cancellationToken);
                savedFiles.Add(statsPath);
                _logger.LogDebug("  ✓ statistics.json 저장");
            }
            
            // 6. parsed_events.json (선택적)
            if (_config.SaveParsedEvents && result.AnalysisResult?.SourceEvents != null 
                && result.AnalysisResult.SourceEvents.Any())
            {
                var eventsPath = Path.Combine(outputDir, "parsed_events.json");
                await SaveJsonAsync(result.AnalysisResult.SourceEvents, eventsPath, cancellationToken);
                savedFiles.Add(eventsPath);
                _logger.LogDebug("  ✓ parsed_events.json 저장 ({Count}개 이벤트)", 
                    result.AnalysisResult.SourceEvents.Count);
            }
            
            _logger.LogInformation("JSON 파일 {Count}개 저장 완료", savedFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON 파일 저장 중 오류 발생");
        }
        
        return savedFiles;
    }

    /// <summary>
    /// HTML 보고서 생성 (이미 구현된 IReportGenerator 사용)
    /// </summary>
    private async Task<string?> GenerateHtmlReportAsync(
        Analysis.Models.Results.AnalysisResult analysisResult,
        string outputDir,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("HTML 보고서 생성 시작");
            
            // IReportGenerator.GenerateReport() 호출 (이미 완전히 구현됨)
            var htmlReport = _reportGenerator.GenerateReport(analysisResult);
            
            var htmlPath = Path.Combine(outputDir, "report.html");
            await File.WriteAllTextAsync(htmlPath, htmlReport, cancellationToken);
            
            _logger.LogInformation("HTML 보고서 생성 완료: {FilePath} ({Size:F1} KB)", 
                htmlPath, new FileInfo(htmlPath).Length / 1024.0);
            
            return htmlPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTML 보고서 생성 중 오류 발생");
            return null;
        }
    }

    /// <summary>
    /// 객체를 JSON 파일로 저장
    /// </summary>
    private async Task SaveJsonAsync<T>(T obj, string filePath, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}

