using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Configuration;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.MainExperiment;

/// <summary>
/// 중복 제거 필터링 단계별 측정 테스트 (본 실험)
/// </summary>
public sealed class DeduplicationFilteringBreakdownTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private List<NormalizedLogEvent>? _allEvents;

    public DeduplicationFilteringBreakdownTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🔬 중복 제거 필터링 단계별 측정 테스트 초기화 (본 실험 Sample 1-10)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // Sample 1-10 로그 파싱
        // ArtifactWeights.SampleTimeRanges 공용 상수 사용 (다른 테스트와 동일)
        _allEvents = new List<NormalizedLogEvent>();
        
        for (int i = 1; i <= 10; i++)
        {
            if (!ArtifactWeights.SampleTimeRanges.TryGetValue(i, out var timeRange))
            {
                _output.WriteLine($"⚠️ Sample {i}의 시간 범위를 찾을 수 없습니다.");
                continue;
            }
            
            var sampleDir = timeRange.DirectoryName;
            var startTime = timeRange.StartTime;
            var endTime = timeRange.EndTime;
            _output.WriteLine($"📂 Sample {i}: {sampleDir}");
            
            var events = await ParseSampleLogsAsync(sampleDir, startTime, endTime);
            _allEvents.AddRange(events);
            
            _output.WriteLine($"  파싱된 이벤트: {events.Count:N0}개\n");
        }
        
        _output.WriteLine($"📊 총 이벤트 수: {_allEvents.Count:N0}개 (Sample 1-10 합계)");
        _output.WriteLine("✅ 본 실험 10회 이벤트 파싱 완료\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Measure_DeduplicationFilteringBreakdown_MainExperiment()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 중복 제거 필터링 단계별 측정 (본 실험 Sample 1~10)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine($"최초 전체 파싱된 이벤트 수: {_allEvents!.Count}개\n");

        // 1차 필터링: 타임스탬프 임계값만 통과한 쌍 수
        var pairsPassingTimeThreshold = CountPairsPassingTimeThreshold(_allEvents);
        _output.WriteLine($"1차 필터 (타임스탬프 임계값) 통과 쌍 수: {pairsPassingTimeThreshold.Count}개");

        // 2차 필터링: 타임스탬프 임계값 통과 + Jaccard Similarity >= 0.55
        var options = CreateAnalysisOptions();
        var pairsPassingBothFilters = pairsPassingTimeThreshold
            .Where(p => CalculateJaccardSimilarity(p.Event1.Attributes, p.Event2.Attributes) >= options.DeduplicationSimilarityThreshold)
            .ToList();
        _output.WriteLine($"2차 필터 (Jaccard Similarity >= 0.55) 통과 쌍 수: {pairsPassingBothFilters.Count}개\n");

        // 실제 중복 제거 실행
        var deduplicator = new EventDeduplicator(NullLogger<EventDeduplicator>.Instance, options);
        var deduplicatedEvents = deduplicator.Deduplicate(_allEvents, out var _);
        var totalRemoved = _allEvents.Count - deduplicatedEvents.Count;

        _output.WriteLine($"최종 중복 제거 후 이벤트 수: {deduplicatedEvents.Count}개");
        _output.WriteLine($"총 제거된 중복 이벤트 수: {totalRemoved}개\n");

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 결과 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        _output.WriteLine($"최초 전체 파싱된 이벤트 수: {_allEvents.Count}개");
        _output.WriteLine($"1차 필터 (타임스탬프 임계값) 통과 쌍 수: {pairsPassingTimeThreshold.Count}개");
        _output.WriteLine($"2차 필터 (Jaccard Similarity >= 0.55) 통과 쌍 수: {pairsPassingBothFilters.Count}개");
        _output.WriteLine($"최종 중복 제거 후 이벤트 수: {deduplicatedEvents.Count}개");
        _output.WriteLine($"총 제거된 중복 이벤트 수: {totalRemoved}개");
    }

    private List<(NormalizedLogEvent Event1, NormalizedLogEvent Event2, double TimeDiffMs)> CountPairsPassingTimeThreshold(List<NormalizedLogEvent> events)
    {
        var pairs = new List<(NormalizedLogEvent Event1, NormalizedLogEvent Event2, double TimeDiffMs)>();

        var eventsByType = events
            .GroupBy(e => e.EventType)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in eventsByType)
        {
            var eventType = group.Key;
            var eventList = group.OrderBy(e => e.Timestamp).ToList();
            var timeThreshold = ArtifactWeights.GetTimeThreshold(eventType); // 본 실험은 현재 설정값 사용

            for (int i = 0; i < eventList.Count - 1; i++)
            {
                for (int j = i + 1; j < eventList.Count; j++)
                {
                    var event1 = eventList[i];
                    var event2 = eventList[j];

                    var timeDiff = Math.Abs((event1.Timestamp - event2.Timestamp).TotalMilliseconds);
                    if (timeDiff > timeThreshold)
                        break;

                    if (!string.Equals(event1.PackageName, event2.PackageName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    pairs.Add((event1, event2, timeDiff));
                }
            }
        }

        return pairs;
    }

    private double CalculateJaccardSimilarity(
        IReadOnlyDictionary<string, object> attrs1,
        IReadOnlyDictionary<string, object> attrs2)
    {
        if (attrs1.Count == 0 && attrs2.Count == 0)
            return 1.0;

        var keys1 = attrs1.Keys.ToHashSet();
        var keys2 = attrs2.Keys.ToHashSet();

        var intersection = keys1.Intersect(keys2).Count(key =>
            attrs1[key]?.ToString() == attrs2[key]?.ToString());
        var union = keys1.Union(keys2).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            DeduplicationSimilarityThreshold = 0.55
        };
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string sampleDirectory, 
        DateTime? startTime, 
        DateTime? endTime)
    {
        var samplePath = Path.Combine(_sampleLogsPath, sampleDirectory);
        var allEvents = new List<NormalizedLogEvent>();
        
        // 로그 파일 설정 맵핑 (DeduplicationEffectValidationTests와 동일)
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
        
        foreach (var (logFileName, configFileName) in logConfigs)
        {
            var events = await ParseLogFileAsync(samplePath, logFileName, configFileName, startTime, endTime);
            allEvents.AddRange(events);
        }
        
        return allEvents.OrderBy(e => e.Timestamp).ToList();
    }

    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string samplePath,
        string logFileName,
        string configFileName,
        DateTime? startTime,
        DateTime? endTime)
    {
        var logPath = Path.Combine(samplePath, logFileName);
        
        if (!File.Exists(logPath))
        {
            return new List<NormalizedLogEvent>();
        }
        
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }
        
        // YAML 설정 로드
        var configLoader = new YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);
        
        // DeviceInfo 생성
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "15",
            Manufacturer = "Samsung",
            Model = "SM-G991N"
        };
        
        // Parser 생성 및 파싱
        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);
        var options = new LogParsingOptions
        {
            MaxFileSizeMB = 50,
            DeviceInfo = deviceInfo,
            ConvertToUtc = false,
            StartTime = startTime,
            EndTime = endTime
        };
        
        try
        {
            var result = await parser.ParseAsync(logPath, options);
            return result.Success ? result.Events.ToList() : new List<NormalizedLogEvent>();
        }
        catch
        {
            return new List<NormalizedLogEvent>();
        }
    }
}

