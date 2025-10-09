using AndroidAdbAnalyzeModule.Configuration.Models;
using AndroidAdbAnalyzeModule.Configuration.Validators;
using AndroidAdbAnalyzeModule.Core.Exceptions;
using AndroidAdbAnalyzeModule.Core.Interfaces;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing.Interfaces;
using AndroidAdbAnalyzeModule.Parsing.LineParsers;
using AndroidAdbAnalyzeModule.Parsing.MultilinePatterns;
using AndroidAdbAnalyzeModule.Parsing.SectionSplitters;
using AndroidAdbAnalyzeModule.Preprocessing;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AndroidAdbAnalyzeModule.Parsing;

/// <summary>
/// ADB 로그 파서 구현체
/// 전체 파싱 파이프라인 통합
/// </summary>
public sealed class AdbLogParser : ILogParser
{
    private readonly LogConfiguration _configuration;
    private readonly ISectionSplitter _sectionSplitter;
    private readonly ILogger<AdbLogParser>? _logger;
    
    // 성능 최적화: RegexLineParser 인스턴스 캐싱
    private readonly Dictionary<string, List<ILineParser>> _cachedParsers;
    
    // 복잡한 패턴 파서 (2줄 이상): 하드코딩으로 관리
    // TODO: 3개 이상 반복되면 플러그인 시스템으로 리팩토링
    private readonly List<IMultilinePatternParser> _multilineParsers;

    /// <summary>
    /// AdbLogParser 생성자
    /// </summary>
    /// <param name="configuration">로그 설정</param>
    /// <param name="logger">로거 (선택사항)</param>
    public AdbLogParser(LogConfiguration configuration, ILogger<AdbLogParser>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _sectionSplitter = new LogSectionSplitter(); // AdbLogParser에서 이미 섹션 관련 로깅 수행
        _logger = logger;
        
        // RegexLineParser 인스턴스를 미리 생성하여 캐싱 (성능 최적화)
        _cachedParsers = InitializeParsers();
        
        // Multiline pattern parsers 초기화 (하드코딩)
        _multilineParsers = InitializeMultilineParsers();
    }
    
    /// <summary>
    /// 복잡한 패턴(2줄 이상) 파서 초기화
    /// 현재는 하드코딩으로 관리하며, 3개 이상 반복 시 플러그인 시스템으로 전환 예정
    /// </summary>
    private List<IMultilinePatternParser> InitializeMultilineParsers()
    {
        var parsers = new List<IMultilinePatternParser>
        {
            new SilentCameraCaptureParser(_logger),  // Priority 0 (무음 카메라 5줄 패턴)
            new ActivityRefreshRateParser(_logger)   // Priority 1 (일반 RefreshRate 2줄 패턴)
            // TODO: 새로운 multiline 패턴 파서는 여기에 추가
        };
        
        _logger?.LogDebug("Initialized {MultilineParserCount} multiline pattern parsers",
            parsers.Count);
        
        return parsers;
    }
    
    /// <summary>
    /// 모든 파서 인스턴스를 미리 생성하여 캐싱
    /// 이를 통해 매 라인마다 파서를 재생성하는 오버헤드를 제거
    /// </summary>
    private Dictionary<string, List<ILineParser>> InitializeParsers()
    {
        var cache = new Dictionary<string, List<ILineParser>>();
        
        foreach (var parserConfig in _configuration.Parsers.Where(p => p.Enabled))
        {
            var parserInstances = new List<ILineParser>();
            
            foreach (var linePattern in parserConfig.LinePatterns)
            {
                try
                {
                    var lineParser = new RegexLineParser(linePattern);
                    parserInstances.Add(lineParser);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, 
                        "Failed to initialize parser for pattern '{PatternId}' in parser '{ParserId}'",
                        linePattern.Id, parserConfig.Id);
                }
            }
            
            if (parserInstances.Count > 0)
            {
                cache[parserConfig.Id] = parserInstances;
            }
        }
        
        _logger?.LogDebug("Initialized {ParserCount} parsers with {TotalPatterns} patterns",
            cache.Count, cache.Values.Sum(list => list.Count));
        
        return cache;
    }

    /// <summary>
    /// 로그 파일 파싱
    /// </summary>
    public async Task<ParsingResult> ParseAsync(
        string logFilePath,
        LogParsingOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
            throw new ArgumentException("Log file path cannot be null or empty", nameof(logFilePath));

        if (!File.Exists(logFilePath))
            throw new FileNotFoundException($"Log file not found: {logFilePath}", logFilePath);

        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // 파일 크기 검증
        var fileInfo = new FileInfo(logFilePath);
        var fileSizeBytes = fileInfo.Length;
        var maxSizeBytes = (long)(options.MaxFileSizeMB * 1024 * 1024);
        if (fileSizeBytes > maxSizeBytes)
        {
            throw new LogFileTooLargeException(logFilePath, fileSizeBytes, maxSizeBytes);
        }

        // 디바이스 버전 호환성 검증
        _logger?.LogDebug("Validating device compatibility: Android {AndroidVersion}",
            options.DeviceInfo.AndroidVersion ?? "not specified");
        
        var validator = new ConfigurationValidator(null); // 로깅은 AdbLogParser에서 수행
        validator.ValidateDeviceCompatibility(options.DeviceInfo, _configuration);
        
        _logger?.LogDebug("Device compatibility validated successfully");
        _logger?.LogInformation("Starting log parsing: {FilePath}", logFilePath);
        
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<ParsingError>();
        var parsedEntries = new List<ParsedLogEntry>();

        try
        {
            // 1. 섹션 분리
            _logger?.LogDebug("Splitting log file into sections");
            var sections = await SplitSectionsAsync(logFilePath, cancellationToken);
            _logger?.LogInformation("Found {SectionCount} sections", sections.Count);

            // 2. 라인 파싱
            _logger?.LogDebug("Parsing sections");
            foreach (var section in sections.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger?.LogDebug("Parsing section: {SectionId} ({LineCount} lines)", 
                    section.Id, section.Lines.Count);
                ParseSection(section, parsedEntries, errors);
            }
            _logger?.LogInformation("Parsed {EntryCount} entries from {SectionCount} sections", 
                parsedEntries.Count, sections.Count);

            // 3. 정규화 및 NormalizedLogEvent 생성
            _logger?.LogDebug("Normalizing events");
            var normalizedEvents = NormalizeEvents(parsedEntries, options, errors);
            _logger?.LogInformation("Normalized {EventCount} events", normalizedEvents.Count);

            // 4. 통계 생성
            var statistics = CreateStatistics(
                parsedEntries.Count,
                normalizedEvents.Count,
                errors.Count,
                stopwatch.Elapsed,
                normalizedEvents);

            stopwatch.Stop();

            _logger?.LogInformation(
                "Parsing completed: {EventCount} events, {ErrorCount} errors, {ElapsedMs}ms",
                normalizedEvents.Count, errors.Count, stopwatch.ElapsedMilliseconds);

            return new ParsingResult
            {
                Success = true,
                Events = normalizedEvents,
                Statistics = statistics,
                Errors = errors
            };
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Parsing was cancelled for file: {FilePath}", logFilePath);
            return new ParsingResult
            {
                Success = false,
                ErrorMessage = "Parsing was cancelled",
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Parsing failed for file: {FilePath}", logFilePath);
            return new ParsingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex,
                Errors = errors
            };
        }
    }

    /// <summary>
    /// 섹션 분리
    /// </summary>
    private async Task<IReadOnlyDictionary<string, LogSection>> SplitSectionsAsync(
        string logFilePath,
        CancellationToken cancellationToken)
    {
        var sectionDefinitions = _configuration.Sections
            .Where(s => s.Enabled)
            .Select(s => new SectionDefinition
            {
                Id = s.Id,
                Name = s.Name,
                Enabled = s.Enabled,
                StartMarker = s.StartMarker,
                EndMarker = s.EndMarker,
                MarkerType = s.MarkerType
            })
            .ToList();

        return await _sectionSplitter.SplitAsync(logFilePath, sectionDefinitions);
    }

    /// <summary>
    /// 섹션 파싱
    /// </summary>
    private void ParseSection(
        LogSection section,
        List<ParsedLogEntry> parsedEntries,
        List<ParsingError> errors)
    {
        // 이 섹션을 처리하는 파서 설정 찾기
        var parserConfigs = _configuration.Parsers
            .Where(p => p.Enabled)
            .Where(p => p.TargetSections.Contains(section.Id))
            .OrderBy(p => p.Priority)
            .ToList();

        // 이 섹션을 타겟으로 하는 multiline parser가 있는지 확인
        var hasMultilineParser = _multilineParsers.Any(p => p.TargetSectionId == section.Id);

        // 일반 파서도 없고 multiline parser도 없으면 스킵
        if (parserConfigs.Count == 0 && !hasMultilineParser)
            return;

        // 각 라인 파싱
        for (int i = 0; i < section.Lines.Count; i++)
        {
            var line = section.Lines[i];
            var lineNumber = section.StartLine + i + 1;

            // 빈 라인 스킵
            if (_configuration.GlobalSettings.SkipEmptyLines && 
                string.IsNullOrWhiteSpace(line))
                continue;

            // 주석 스킵
            if (_configuration.GlobalSettings.SkipComments && 
                line.TrimStart().StartsWith(_configuration.GlobalSettings.CommentPrefix))
                continue;

            try
            {
                // 1단계: Multiline pattern parser 먼저 시도 (캡슐화)
                bool multilineParsed = false;
                var multilineParser = _multilineParsers
                    .Where(p => p.TargetSectionId == section.Id)
                    .OrderBy(p => p.Priority)
                    .FirstOrDefault(p => p.CanParse(section, i));
                
                if (multilineParser != null)
                {
                    if (multilineParser.TryParse(section, i, out var multilineEntry, out int linesToSkip))
                    {
                        if (multilineEntry != null)
                        {
                            parsedEntries.Add(multilineEntry);
                            i += linesToSkip; // 파싱된 라인 수만큼 스킵
                            multilineParsed = true;
                        }
                    }
                }
                
                if (multilineParsed)
                    continue; // 다음 라인으로 이동
                
                // 2단계: 일반 라인 파서 시도 (기존 로직)
                var context = new ParsingContext
                {
                    SectionId = section.Id,
                    LineNumber = lineNumber,
                    LastTimestamp = parsedEntries.LastOrDefault()?.Timestamp,
                    SharedState = new Dictionary<string, object>()
                };

                // 캐시된 파서들을 순서대로 시도 (성능 최적화)
                bool parsed = false;
                foreach (var parserConfig in parserConfigs)
                {
                    // 캐시에서 파서 인스턴스 가져오기
                    if (!_cachedParsers.TryGetValue(parserConfig.Id, out var lineParsers))
                        continue;
                    
                    foreach (var lineParser in lineParsers)
                    {
                        if (lineParser.CanParse(line, context))
                        {
                            var entry = lineParser.Parse(line, context);
                            if (entry != null)
                            {
                                parsedEntries.Add(entry);
                                parsed = true;
                                break;
                            }
                        }
                    }

                    if (parsed)
                        break;
                }

                // 파싱 실패 시 에러 기록 (설정에 따라)
                if (!parsed && _configuration.ErrorHandling.OnInvalidLine == "log")
                {
                    _logger?.LogWarning(
                        "No matching parser found for line {LineNumber} in section {SectionId}: {Line}",
                        lineNumber, section.Id, line.Length > 100 ? line.Substring(0, 100) + "..." : line);
                    
                    errors.Add(new ParsingError
                    {
                        LineNumber = lineNumber,
                        Line = line,
                        ErrorMessage = "No matching parser found",
                        Severity = ErrorSeverity.Warning,
                        SectionId = section.Id
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error parsing line {LineNumber} in section {SectionId}",
                    lineNumber, section.Id);
                
                errors.Add(new ParsingError
                {
                    LineNumber = lineNumber,
                    Line = line,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    Severity = ErrorSeverity.Error,
                    SectionId = section.Id
                });
            }
        }
    }

    /// <summary>
    /// ParsedLogEntry를 NormalizedLogEvent로 변환
    /// </summary>
    private List<NormalizedLogEvent> NormalizeEvents(
        List<ParsedLogEntry> parsedEntries,
        LogParsingOptions options,
        List<ParsingError> errors)
    {
        var normalizer = new TimestampNormalizer(
            options.DeviceInfo,
            options.ConvertToUtc);

        var normalizedEvents = new List<NormalizedLogEvent>();
        int filteredByTimeRange = 0;

        foreach (var entry in parsedEntries)
        {
            try
            {
                var normalizedTimestamp = normalizer.NormalizeLogEntry(entry);

                // 타임스탬프가 없는 경우 처리
                if (!normalizedTimestamp.HasValue)
                {
                    if (_configuration.ErrorHandling.OnMissingTimestamp == "skip")
                        continue;

                    if (_configuration.ErrorHandling.OnMissingTimestamp == "log")
                    {
                        _logger?.LogWarning(
                            "Missing timestamp for line {LineNumber} in section {SectionId}",
                            entry.LineNumber, entry.SectionId);
                        
                        errors.Add(new ParsingError
                        {
                            LineNumber = entry.LineNumber,
                            Line = entry.RawLine,
                            ErrorMessage = "Missing timestamp",
                            Severity = ErrorSeverity.Warning,
                            SectionId = entry.SectionId
                        });
                        continue;
                    }
                }

                // 시간 범위 필터링 (StartTime 이상, EndTime 이하)
                if (options.StartTime.HasValue && normalizedTimestamp.Value < options.StartTime.Value)
                {
                    filteredByTimeRange++;
                    continue;
                }

                if (options.EndTime.HasValue && normalizedTimestamp.Value > options.EndTime.Value)
                {
                    filteredByTimeRange++;
                    continue;
                }

                var normalizedEvent = new NormalizedLogEvent
                {
                    EventId = Guid.NewGuid(),
                    Timestamp = normalizedTimestamp ?? DateTime.UtcNow,
                    EventType = entry.EventType,
                    SourceSection = entry.SectionId,
                    PackageName = ExtractPackageName(entry.Fields),
                    Attributes = entry.Fields,
                    RawLine = entry.RawLine,
                    DeviceInfo = options.DeviceInfo
                };

                normalizedEvents.Add(normalizedEvent);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Normalization failed for line {LineNumber} in section {SectionId}",
                    entry.LineNumber, entry.SectionId);
                
                errors.Add(new ParsingError
                {
                    LineNumber = entry.LineNumber,
                    Line = entry.RawLine,
                    ErrorMessage = $"Normalization failed: {ex.Message}",
                    Exception = ex,
                    Severity = ErrorSeverity.Error,
                    SectionId = entry.SectionId
                });
            }
        }

        // 시계열 정렬 (설정에 따라)
        if (_configuration.GlobalSettings.TimeSeriesOrder == "ascending")
        {
            normalizedEvents = normalizedEvents.OrderBy(e => e.Timestamp).ToList();
        }
        else if (_configuration.GlobalSettings.TimeSeriesOrder == "descending")
        {
            normalizedEvents = normalizedEvents.OrderByDescending(e => e.Timestamp).ToList();
        }

        // 시간 범위 필터링 통계 로깅
        if (filteredByTimeRange > 0)
        {
            _logger?.LogInformation(
                "Filtered {Count} events by time range (Start: {Start}, End: {End})",
                filteredByTimeRange,
                options.StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "none",
                options.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "none");
        }

        return normalizedEvents;
    }

    /// <summary>
    /// 통계 생성
    /// </summary>
    private ParsingStatistics CreateStatistics(
        int parsedLines,
        int normalizedEvents,
        int errorLines,
        TimeSpan elapsed,
        List<NormalizedLogEvent> events)
    {
        var eventTypeCounts = events
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        var sectionLineCounts = events
            .GroupBy(e => e.SourceSection)
            .ToDictionary(g => g.Key, g => g.Count());

        return new ParsingStatistics
        {
            TotalLines = parsedLines + errorLines,
            ParsedLines = parsedLines,
            SkippedLines = parsedLines - normalizedEvents,
            ErrorLines = errorLines,
            ElapsedTime = elapsed,
            EventTypeCounts = eventTypeCounts,
            SectionLineCounts = sectionLineCounts
        };
    }

    /// <summary>
    /// Fields에서 패키지명 추출
    /// </summary>
    /// <param name="fields">ParsedLogEntry의 Fields</param>
    /// <returns>추출된 패키지명 또는 null</returns>
    private string? ExtractPackageName(IReadOnlyDictionary<string, object> fields)
    {
        if (fields == null || fields.Count == 0)
            return null;

        // 1. "package" 필드에서 직접 추출
        if (fields.TryGetValue("package", out var packageObj))
        {
            var packageName = packageObj?.ToString();
            if (!string.IsNullOrWhiteSpace(packageName))
                return packageName;
        }

        // 2. "packageName" 필드에서 추출 (대소문자 구분)
        if (fields.TryGetValue("packageName", out var packageNameObj))
        {
            var packageName = packageNameObj?.ToString();
            if (!string.IsNullOrWhiteSpace(packageName))
                return packageName;
        }

        // 3. "callingPackage" 필드에서 추출
        if (fields.TryGetValue("callingPackage", out var callingPkgObj))
        {
            var packageName = callingPkgObj?.ToString();
            if (!string.IsNullOrWhiteSpace(packageName))
                return packageName;
        }

        // 4. "uri" 필드에서 패키지명 추출 (예: content://com.android.providers.media.documents/...)
        if (fields.TryGetValue("uri", out var uriObj))
        {
            var uri = uriObj?.ToString();
            if (!string.IsNullOrWhiteSpace(uri))
            {
                // content://package.name/... 형식에서 패키지명 추출
                var match = System.Text.RegularExpressions.Regex.Match(uri, @"^content://([^/]+)");
                if (match.Success)
                {
                    var authority = match.Groups[1].Value;
                    // authority가 패키지명 형식인지 확인 (최소 2개의 점으로 구분)
                    if (authority.Contains('.'))
                        return authority;
                }
            }
        }

        // 5. "targetPackage" 필드에서 추출
        if (fields.TryGetValue("targetPackage", out var targetPkgObj))
        {
            var packageName = targetPkgObj?.ToString();
            if (!string.IsNullOrWhiteSpace(packageName))
                return packageName;
        }

        return null;
    }
}

