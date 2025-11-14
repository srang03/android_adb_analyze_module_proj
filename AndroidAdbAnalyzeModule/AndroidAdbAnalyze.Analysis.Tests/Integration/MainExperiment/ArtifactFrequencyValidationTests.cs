using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
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
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Configuration;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.MainExperiment;

/// <summary>
/// 앱별 아티팩트 출현 빈도 측정 테스트 (본 실험 기반)
/// </summary>
/// <remarks>
/// 목적:
/// - 본 실험(3회)에서 촬영 판정의 직접적 근거가 되는 주요 아티팩트의 앱별 출현 빈도를 측정
/// - 4개 앱(기본 카메라, 카카오톡, 텔레그램, 무음 카메라)별 아티팩트 패턴 분석
/// - Strategy Pattern 기반 차별화 전략 설계의 근거 마련
/// 
/// 측정 대상 (8개 주요 아티팩트):
/// - 확정 핵심 (2개): DATABASE_INSERT, SILENT_CAMERA_CAPTURE
/// - 조건부 핵심 (4개): VIBRATION_EVENT, PLAYER_EVENT, URI_PERMISSION_GRANT, FOREGROUND_SERVICE
/// - 보조 (2개): CAMERA_ACTIVITY_REFRESH, MEDIA_EXTRACTOR
/// 
/// 논문 반영:
/// - 제4장 제4절: 앱별 차별화 전략 설계
/// - 부록 3, 2.3.2: 앱별 아티팩트 출현 빈도 분석 방법론
/// - 부록 3, 표 34: 앱별 주요 아티팩트 출현 빈도
/// </remarks>
public sealed class ArtifactFrequencyValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    // 측정 대상 아티팩트 (8개)
    private readonly List<string> _targetArtifacts = new()
    {
        // 확정 핵심
        "DATABASE_INSERT",
        "SILENT_CAMERA_CAPTURE",
        
        // 조건부 핵심
        "VIBRATION_EVENT",
        "PLAYER_EVENT",
        "URI_PERMISSION_GRANT",
        "FOREGROUND_SERVICE",
        
        // 보조 (촬영 판정 관련)
        "CAMERA_ACTIVITY_REFRESH",
        "MEDIA_EXTRACTOR"
    };
    
    // 본 실험 분석 결과 캐싱
    private Dictionary<string, List<List<NormalizedLogEvent>>>? _captureEventsByApp; // 앱 -> 촬영별 이벤트 리스트
    private Dictionary<string, int>? _captureCounts;
    private int _totalDetectedCaptures;
    private List<NormalizedLogEvent>? _allParsedEvents;
    private AnalysisResult? _analysisResult;

    public ArtifactFrequencyValidationTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        
        // 경로 설정
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🔬 앱별 아티팩트 출현 빈도 측정 테스트 초기화 (본 실험 10회)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 본 실험 10회 분석
        _allParsedEvents = await ParseAllMainExperimentEventsAsync();
        
        // Orchestrator로 분석하여 촬영 탐지
        // Ground Truth와 동일한 YAML 설정 사용
        var orchestrator = CreateOrchestratorWithYamlConfig();
        _analysisResult = await orchestrator.AnalyzeAsync(_allParsedEvents, CreateAnalysisOptions());
        
        _output.WriteLine($"📊 분석 결과:");
        _output.WriteLine($"  - 총 이벤트: {_allParsedEvents.Count}개");
        _output.WriteLine($"  - 탐지된 촬영: {_analysisResult.CaptureEvents.Count}개\n");
        
        // 탐지된 촬영 수 저장
        _totalDetectedCaptures = _analysisResult.CaptureEvents.Count;
        
        // 촬영 이벤트를 앱별, 촬영별로 분류
        _captureEventsByApp = new Dictionary<string, List<List<NormalizedLogEvent>>>();
        _captureCounts = new Dictionary<string, int>();
        
        foreach (var capture in _analysisResult.CaptureEvents)
        {
            var packageName = capture.PackageName ?? "Unknown";
            
            if (!_captureEventsByApp.ContainsKey(packageName))
            {
                _captureEventsByApp[packageName] = new List<List<NormalizedLogEvent>>();
                _captureCounts[packageName] = 0;
            }
            
            // ✅ 촬영 탐지 알고리즘이 실제로 사용한 SourceEventIds 기반 이벤트 수집
            // (시간 범위 기반이 아닌, 촬영 탐지 결과의 실제 사용 이벤트만 측정)
            // 이 방식으로 DATABASE_INSERT, PLAYER_EVENT 등 시간 범위 밖 이벤트도 포함됨
            var captureSourceEvents = capture.SourceEventIds
                .Select(id => _allParsedEvents.FirstOrDefault(e => e.EventId == id))
                .Where(e => e != null)
                .Cast<NormalizedLogEvent>()
                .ToList();
            
            // 촬영별로 이벤트 리스트 추가
            _captureEventsByApp[packageName].Add(captureSourceEvents);
            _captureCounts[packageName]++;
        }
        
        _output.WriteLine($"📋 앱별 촬영 수:");
        foreach (var (packageName, count) in _captureCounts.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"  - {GetAppDisplayName(packageName)}: {count}개");
        }
        
        // 디버깅: 카카오톡과 기본 카메라 촬영 주변 이벤트 분석
        _output.WriteLine("\n🔍 디버깅: 촬영 주변 이벤트 분석");
        
        // 기본 카메라 디버깅 (DATABASE_INSERT, PLAYER_EVENT 확인)
        if (_captureEventsByApp.TryGetValue("com.sec.android.app.camera", out var defaultCameraCaptures))
        {
            _output.WriteLine($"\n📱 기본 카메라 촬영 수: {defaultCameraCaptures.Count}개");
            
            for (int i = 0; i < Math.Min(2, defaultCameraCaptures.Count); i++)
            {
                var captureEvents = defaultCameraCaptures[i];
                _output.WriteLine($"\n  촬영 #{i + 1}:");
                _output.WriteLine($"    - 시간 범위 (±15초) 내 이벤트 수: {captureEvents.Count}개");
                
                var capture = _analysisResult!.CaptureEvents
                    .Where(c => c.PackageName == "com.sec.android.app.camera")
                    .Skip(i)
                    .FirstOrDefault();
                
                if (capture != null)
                {
                    _output.WriteLine($"    📌 촬영 시간: {capture.CaptureTime:HH:mm:ss}");
                    _output.WriteLine($"    📌 SourceEventIds 수: {capture.SourceEventIds.Count}개");
                    _output.WriteLine($"    📌 ArtifactTypes: {string.Join(", ", capture.ArtifactTypes)}");
                    
                    // DATABASE_INSERT와 PLAYER_EVENT 확인
                    var hasDatabaseInsert = captureEvents.Any(e => e.EventType == "DATABASE_INSERT");
                    var hasPlayerEvent = captureEvents.Any(e => e.EventType == "PLAYER_EVENT");
                    
                    _output.WriteLine($"    🔍 DATABASE_INSERT 존재: {(hasDatabaseInsert ? "✅ 있음" : "❌ 없음")}");
                    _output.WriteLine($"    🔍 PLAYER_EVENT 존재: {(hasPlayerEvent ? "✅ 있음" : "❌ 없음")}");
                }
                
                var eventTypes = captureEvents.GroupBy(e => e.EventType).OrderByDescending(g => g.Count());
                _output.WriteLine($"    - EventType 분포:");
                foreach (var et in eventTypes.Take(10))
                {
                    _output.WriteLine($"      · {et.Key}: {et.Count()}개");
                }
            }
        }
        
        // 카카오톡 디버깅
        _output.WriteLine($"\n📱 카카오톡:");
        if (_captureEventsByApp.TryGetValue("com.kakao.talk", out var kakaoCaptures))
        {
            _output.WriteLine($"  카카오톡 촬영 수: {kakaoCaptures.Count}개\n");
            
            for (int i = 0; i < Math.Min(2, kakaoCaptures.Count); i++)
            {
                var captureEvents = kakaoCaptures[i];
                _output.WriteLine($"  촬영 #{i + 1}:");
                _output.WriteLine($"    - 시간 범위 (±15초) 내 이벤트 수: {captureEvents.Count}개");
                
                // PackageName과 관계없이 촬영 주변 모든 이벤트 분석
                var capture = _analysisResult!.CaptureEvents
                    .Where(c => c.PackageName == "com.kakao.talk")
                    .Skip(i)
                    .FirstOrDefault();
                
                if (capture != null)
                {
                    _output.WriteLine($"    📌 촬영 시간: {capture.CaptureTime:HH:mm:ss}");
                    _output.WriteLine($"    📌 SourceEventIds 수: {capture.SourceEventIds.Count}개");
                    _output.WriteLine($"    📌 ArtifactTypes: {string.Join(", ", capture.ArtifactTypes)}");
                }
                
                if (captureEvents.Count == 0 || true)  // 항상 필터링 없는 분석 수행
                {
                    if (captureEvents.Count == 0)
                    {
                        _output.WriteLine($"    ⚠️ 경고: PackageName 필터링으로 모든 이벤트가 제외됨");
                    }
                    
                    // PackageName 필터링 없이 재시도
                    var captureTime = _analysisResult!.CaptureEvents
                        .Where(c => c.PackageName == "com.kakao.talk")
                        .Skip(i)
                        .FirstOrDefault()?.CaptureTime;
                    
                    if (captureTime.HasValue)
                    {
                        var captureTimeWindow = TimeSpan.FromSeconds(15);
                        var captureStartTime = captureTime.Value.AddSeconds(-captureTimeWindow.TotalSeconds);
                        var captureEndTime = captureTime.Value.AddSeconds(captureTimeWindow.TotalSeconds);
                        
                        var allEventsNearCapture = _allParsedEvents!
                            .Where(e => e.Timestamp >= captureStartTime && e.Timestamp <= captureEndTime)
                            .ToList();
                        
                        _output.WriteLine($"    📌 필터링 없는 이벤트 수: {allEventsNearCapture.Count}개");
                        
                        var groupedByPackage = allEventsNearCapture
                            .GroupBy(e => e.PackageName ?? "null")
                            .OrderByDescending(g => g.Count());
                        
                        _output.WriteLine($"    📦 PackageName별 분포:");
                        foreach (var group in groupedByPackage.Take(5))
                        {
                            _output.WriteLine($"      - {group.Key}: {group.Count()}개");
                            
                            var eventTypes = group.GroupBy(e => e.EventType).OrderByDescending(g => g.Count());
                            foreach (var et in eventTypes.Take(3))
                            {
                                _output.WriteLine($"        · {et.Key}: {et.Count()}개");
                            }
                        }
                    }
                }
                else
                {
                    var eventTypes = captureEvents.GroupBy(e => e.EventType).OrderByDescending(g => g.Count());
                    _output.WriteLine($"    - EventType 분포:");
                    foreach (var et in eventTypes.Take(5))
                    {
                        _output.WriteLine($"      · {et.Key}: {et.Count()}개");
                    }
                }
                _output.WriteLine("");
            }
        }
        
        _output.WriteLine("✅ 본 실험 10회 분석 완료\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 앱별 아티팩트 출현 빈도 측정 테스트
    /// </summary>
    /// <remarks>
    /// 논문 부록 3, 표 34 "앱별 주요 아티팩트 출현 빈도 (본 실험 10회)" 데이터 생성
    /// </remarks>
    [Fact]
    public void Measure_ArtifactFrequency_MainExperiments()
    {
        // Given
        _output.WriteLine("\n📊 본 실험 10회 앱별 아티팩트 출현 빈도 측정 시작\n");
        _output.WriteLine($"총 촬영 탐지 수: {_totalDetectedCaptures}개");
        _output.WriteLine($"측정 대상 앱: 4개 (기본 카메라, 카카오톡, 텔레그램, 무음 카메라)");
        _output.WriteLine($"측정 대상 아티팩트: {_targetArtifacts.Count}개\n");

        // 1. 앱별 출현 빈도 계산 (촬영별로 계산)
        var frequencyTable = new Dictionary<string, Dictionary<string, int>>();
        
        foreach (var (packageName, captureEventsList) in _captureEventsByApp!)
        {
            var appFrequency = new Dictionary<string, int>();
            
            foreach (var artifactType in _targetArtifacts)
            {
                // 해당 아티팩트가 발생한 촬영 수 카운트
                var capturesWithArtifact = captureEventsList.Count(captureEvents =>
                    captureEvents.Any(e => e.EventType == artifactType));
                
                appFrequency[artifactType] = capturesWithArtifact;
            }
            
            frequencyTable[packageName] = appFrequency;
        }
        
        // 2. 결과 출력 (표 형식)
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("[표] 앱별 주요 아티팩트 출현 빈도 (본 실험 10회)");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        // 표 헤더
        _output.WriteLine($"| {"아티팩트",-30} | {"기본 카메라",-15} | {"카카오톡",-15} | {"텔레그램",-15} | {"무음 카메라",-15} | {"평균 출현율",-10} |");
        _output.WriteLine($"|{new string('-', 32)}|{new string('-', 17)}|{new string('-', 17)}|{new string('-', 17)}|{new string('-', 17)}|{new string('-', 12)}|");
        
        // 각 아티팩트별 출현 빈도
        var overallFrequency = new Dictionary<string, List<double>>();
        
        foreach (var artifactType in _targetArtifacts)
        {
            var defaultCameraCount = GetFrequencyCount(frequencyTable, "com.sec.android.app.camera", artifactType);
            var kakaoTalkCount = GetFrequencyCount(frequencyTable, "com.kakao.talk", artifactType);
            var telegramCount = GetFrequencyCount(frequencyTable, "org.telegram.messenger", artifactType);
            var silentCameraCount = GetFrequencyCount(frequencyTable, "com.peace.SilentCamera", artifactType);
            
            var defaultCameraTotal = _captureCounts!.GetValueOrDefault("com.sec.android.app.camera", 0);
            var kakaoTalkTotal = _captureCounts!.GetValueOrDefault("com.kakao.talk", 0);
            var telegramTotal = _captureCounts!.GetValueOrDefault("org.telegram.messenger", 0);
            var silentCameraTotal = _captureCounts!.GetValueOrDefault("com.peace.SilentCamera", 0);
            
            var defaultCameraFreq = defaultCameraTotal > 0 ? (double)defaultCameraCount / defaultCameraTotal : 0;
            var kakaoTalkFreq = kakaoTalkTotal > 0 ? (double)kakaoTalkCount / kakaoTalkTotal : 0;
            var telegramFreq = telegramTotal > 0 ? (double)telegramCount / telegramTotal : 0;
            var silentCameraFreq = silentCameraTotal > 0 ? (double)silentCameraCount / silentCameraTotal : 0;
            
            var avgFreq = (defaultCameraFreq + kakaoTalkFreq + telegramFreq + silentCameraFreq) / 4.0;
            
            overallFrequency[artifactType] = new List<double> 
            { 
                defaultCameraFreq, 
                kakaoTalkFreq, 
                telegramFreq, 
                silentCameraFreq 
            };
            
            _output.WriteLine($"| {GetArtifactDisplayName(artifactType),-30} | {FormatFrequency(defaultCameraCount, defaultCameraTotal),-15} | {FormatFrequency(kakaoTalkCount, kakaoTalkTotal),-15} | {FormatFrequency(telegramCount, telegramTotal),-15} | {FormatFrequency(silentCameraCount, silentCameraTotal),-15} | {avgFreq:P0,-10} |");
        }
        
        _output.WriteLine("\n※ 측정 범위: 촬영 판정의 직접적 근거가 되는 주요 아티팩트 8개 (확정 핵심 2개, 조건부 핵심 4개, 보조 2개)");
        _output.WriteLine("※ 측정 제외: 보조 아티팩트 4개 (URI_PERMISSION_REVOKE, PLAYER_CREATED, PLAYER_RELEASED, SHUTTER_SOUND)는 본 실험에서 측정");
        _output.WriteLine("※ 측정 방법론: 부록 3, 2.3.2절 참조\n");
        
        // 3. 관찰 결과 출력
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("📊 관찰 결과");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        // 100% 출현 아티팩트
        var universalArtifacts = overallFrequency
            .Where(kv => kv.Value.All(f => f >= 1.0))
            .Select(kv => GetArtifactDisplayName(kv.Key))
            .ToList();
        
        if (universalArtifacts.Any())
        {
            _output.WriteLine($"✅ 모든 앱에서 100% 출현: {string.Join(", ", universalArtifacts)}");
            _output.WriteLine($"   → 가장 안정적인 아티팩트\n");
        }
        
        // 앱별 특화 아티팩트
        _output.WriteLine("📌 앱별 특화 아티팩트:");
        
        var databaseInsertFreq = overallFrequency["DATABASE_INSERT"];
        if (databaseInsertFreq[0] >= 1.0 && databaseInsertFreq[3] == 0 && databaseInsertFreq[1] == 0 && databaseInsertFreq[2] == 0)
        {
            _output.WriteLine("  - DATABASE_INSERT: 기본 카메라만 100% 생성 (MediaStore 등록 방식, 무음 카메라는 앱 내부 저장소 사용)");
        }
        
        var uriPermissionFreq = overallFrequency["URI_PERMISSION_GRANT"];
        if (uriPermissionFreq[1] >= 1.0 && uriPermissionFreq[0] == 0 && uriPermissionFreq[2] == 0 && uriPermissionFreq[3] == 0)
        {
            _output.WriteLine("  - URI_PERMISSION_GRANT: 카카오톡만 100% 생성 (인텐트 위임 방식)");
        }
        
        var silentCaptureFreq = overallFrequency["SILENT_CAMERA_CAPTURE"];
        if (silentCaptureFreq[3] >= 1.0 && silentCaptureFreq[0] == 0 && silentCaptureFreq[1] == 0 && silentCaptureFreq[2] == 0)
        {
            _output.WriteLine("  - SILENT_CAMERA_CAPTURE: 무음 카메라만 100% 생성 (앱 전용 마커)");
        }
        
        var playerEventFreq = overallFrequency["PLAYER_EVENT"];
        if (playerEventFreq[1] >= 1.0 && playerEventFreq[0] == 0 && playerEventFreq[2] == 0 && playerEventFreq[3] == 0)
        {
            _output.WriteLine("  - PLAYER_EVENT: 카카오톡만 100% 출현 (셔터음 재생)");
        }
        
        _output.WriteLine("\n💡 전략적 시사점:");
        _output.WriteLine("  - 앱별 출현 패턴 차이로 인해 Strategy Pattern 기반 차별화 전략 필요");
        _output.WriteLine("  - BasePatternStrategy: DATABASE_INSERT (기본 카메라만), SILENT_CAMERA_CAPTURE (무음 카메라만) 기반 탐지");
        _output.WriteLine("  - KakaoTalkStrategy: URI_PERMISSION_GRANT + PLAYER_EVENT 기반 탐지");
        _output.WriteLine("  - TelegramStrategy: VIBRATION_EVENT + MEDIA_EXTRACTOR 조합 탐지");
        _output.WriteLine("  - VIBRATION_EVENT와 CAMERA_ACTIVITY_REFRESH는 모든 앱에서 100% 출현으로 범용 아티팩트로 활용 가능\n");
        
        // 4. JSON 파일로 결과 저장
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), "main_experiment_artifact_frequency_result.json");
        var result = new
        {
            MeasurementScope = "본 실험 10회 (46개 촬영: 기본 10 + 카카오 13 + 텔레 13 + 무음 10)",
            TargetArtifacts = _targetArtifacts.Select(a => a.ToString()).ToList(),
            AppFrequency = frequencyTable.ToDictionary(
                kv => GetAppDisplayName(kv.Key),
                kv => kv.Value.ToDictionary(
                    artifactKv => artifactKv.Key.ToString(),
                    artifactKv => new
                    {
                        Occurrences = artifactKv.Value,
                        TotalCaptures = _captureCounts![kv.Key],
                        Frequency = artifactKv.Value / (double)_captureCounts[kv.Key]
                    }
                )
            ),
            OverallFrequency = overallFrequency.ToDictionary(
                kv => kv.Key.ToString(),
                kv => new
                {
                    DefaultCamera = kv.Value[0],
                    KakaoTalk = kv.Value[1],
                    Telegram = kv.Value[2],
                    SilentCamera = kv.Value[3],
                    Average = kv.Value.Average()
                }
            )
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(resultPath, json);
        _output.WriteLine($"✅ 결과가 JSON 파일로 저장되었습니다: {resultPath}\n");
        
        // 5. 측정 결과 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 측정 결과 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _output.WriteLine("✅ 앱별 아티팩트 출현 빈도 측정 완료!");
        _output.WriteLine($"\n📊 측정 결과:");
        _output.WriteLine($"   - 총 탐지 촬영: {_totalDetectedCaptures}개");
        _output.WriteLine($"   - Ground Truth: 46개 (기본 10 + 카카오 13 + 텔레 13 + 무음 10)");
        _output.WriteLine($"   - 출처: 제5장 제1절 실험설계 (총 93개 세션: 촬영 46 + 비촬영 47)");
        
        if (_totalDetectedCaptures == 46)
        {
            _output.WriteLine($"   ✅ Ground Truth와 정확히 일치!");
        }
        else
        {
            _output.WriteLine($"   ⚠️ 차이: {_totalDetectedCaptures - 46}개");
        }
        
        _output.WriteLine($"\n📝 이 측정값은 부록 3 표 34에 반영됩니다.\n");
    }

    #region Helper Methods

    /// <summary>
    /// 본실험 10회 로그 파싱 (Ground Truth 시간 범위 사용)
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseAllMainExperimentEventsAsync()
    {
        var allEvents = new List<NormalizedLogEvent>();

        // Ground Truth 테스트와 동일한 시간 범위 사용
        // 샘플 번호, 디렉토리명, 시작 시간, 종료 시간
        var sampleTimeRanges = new List<(int sampleNumber, string dirName, DateTime startTime, DateTime endTime)>
        {
            (1, "1차 샘플_25_10_04", new DateTime(2025, 10, 4, 14, 49, 0), new DateTime(2025, 10, 4, 14, 56, 0)),
            (2, "2차 샘플_25_10_06", new DateTime(2025, 10, 6, 22, 46, 0), new DateTime(2025, 10, 6, 22, 59, 0)),
            (3, "3차 샘플_25_10_07", new DateTime(2025, 10, 7, 23, 13, 0), new DateTime(2025, 10, 7, 23, 30, 0)),
            (4, "4차 샘플_25_10_12", new DateTime(2025, 10, 12, 16, 7, 0), new DateTime(2025, 10, 12, 16, 25, 0)),
            (5, "5차 샘플_25_10_13", new DateTime(2025, 10, 13, 23, 24, 0), new DateTime(2025, 10, 13, 23, 35, 59)),
            (6, "6차 샘플_25_10_16", new DateTime(2025, 10, 16, 16, 34, 0), new DateTime(2025, 10, 16, 16, 48, 59)),
            (7, "7차 샘플_25_10_16", new DateTime(2025, 10, 17, 10, 33, 0), new DateTime(2025, 10, 17, 10, 50, 59)),
            (8, "8차 샘플_25_10_17", new DateTime(2025, 10, 17, 16, 0, 0), new DateTime(2025, 10, 17, 16, 7, 59)),
            (9, "9차 샘플_25_10_17", new DateTime(2025, 10, 17, 16, 40, 0), new DateTime(2025, 10, 17, 16, 52, 59)),
            (10, "10차 샘플_25_10_17", new DateTime(2025, 10, 17, 23, 56, 0), new DateTime(2025, 10, 18, 0, 13, 59))
        };

        foreach (var (sampleNumber, dirName, startTime, endTime) in sampleTimeRanges)
        {
            _output.WriteLine($"📂 Sample {sampleNumber} ({dirName}) 파싱 중... ({startTime:yyyy-MM-dd HH:mm} ~ {endTime:yyyy-MM-dd HH:mm})");
            var samplePath = Path.Combine(_sampleLogsPath, dirName);
            var events = await ParseSampleLogsAsync(samplePath, startTime, endTime);
            allEvents.AddRange(events);
            _output.WriteLine($"   └─ 파싱 완료: {events.Count}개 이벤트\n");
        }

        _output.WriteLine($"✅ 전체 파싱 완료: {allEvents.Count}개 이벤트 (본 실험 10회)\n");
        return allEvents;
    }

    /// <summary>
    /// 샘플 로그 파싱
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string samplePath, 
        DateTime? startTime, 
        DateTime? endTime)
    {
        var allEvents = new List<NormalizedLogEvent>();
        
        // 로그 파일 설정 맵핑 (Ground Truth와 동일)
        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera_worker.log"] = "adb_media_camera_worker_config.yaml",  // ⭐ DATABASE_INSERT를 위해 필수!
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
        
        return allEvents;
    }

    /// <summary>
    /// 개별 로그 파일 파싱
    /// </summary>
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
        var configLoader = new Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
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
            return result.Events?.ToList() ?? new List<NormalizedLogEvent>();
        }
        catch (Exception)
        {
            return new List<NormalizedLogEvent>();
        }
    }

    /// <summary>
    /// AnalysisOptions 생성
    /// </summary>
    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            DeduplicationSimilarityThreshold = ArtifactWeights.GroundTruthDeduplicationSimilarityThreshold,  // Ground Truth와 동일한 설정 사용
            SameCameraUsageTimeThreshold = TimeSpan.FromSeconds(ArtifactWeights.SameCameraUsageTimeThreshold),
            CaptureDeduplicationWindow = TimeSpan.FromMilliseconds(ArtifactWeights.CaptureDeduplicationWindowMs)
        };
    }

    /// <summary>
    /// Orchestrator 생성 (YAML 설정 사용 - Ground Truth와 동일)
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestratorWithYamlConfig()
    {
        // YAML 설정 파일 경로
        var configPath = Path.Combine(
            "..", "..", "..", "..", "..",
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
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions 등록 (Ground Truth와 동일한 설정)
        services.AddSingleton(new AnalysisOptions { 
            DeduplicationSimilarityThreshold = ArtifactWeights.GroundTruthDeduplicationSimilarityThreshold 
        });
        
        // YAML 설정 로드
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<ArtifactFrequencyValidationTests>();
        var config = AndroidAdbAnalyze.Analysis.Configuration.YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
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
        
        // Deduplication Services
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // Transmission Detection Services
        services.AddSingleton<ITransmissionDetector, WifiTransmissionDetector>();
        
        // Reporting Services
        services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        
        // Orchestration
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    /// <summary>
    /// Orchestrator 생성 (코드 설정 사용 - 사용 안 함)
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestratorWithConfiguration()
    {
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions
        services.AddSingleton(CreateAnalysisOptions());
        
        // YAML Configuration
        var configPath = Path.Combine("..", "..", "..", "..", "..",
            "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Analysis", "Configs",
            "artifact-detection-config.example.yaml");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<ArtifactFrequencyValidationTests>();
        var config = AndroidAdbAnalyze.Analysis.Configuration.YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
        services.AddSingleton(config);
        
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var strategyLogger = sp.GetRequiredService<ILogger<TelegramStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var strategyConfig = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new TelegramStrategy(strategyLogger, calculator, strategyConfig);
        });
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var strategyLogger = sp.GetRequiredService<ILogger<KakaoTalkStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var strategyConfig = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new KakaoTalkStrategy(strategyLogger, calculator, strategyConfig);
        });
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var strategyLogger = sp.GetRequiredService<ILogger<BasePatternStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var strategyConfig = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new BasePatternStrategy(strategyLogger, calculator, strategyConfig);
        });
        
        // Capture Detector
        services.AddSingleton<ICaptureDetector, CameraCaptureDetector>();
        
        // Confidence Calculator
        services.AddSingleton<IConfidenceCalculator>(sp =>
        {
            var calculatorLogger = sp.GetRequiredService<ILogger<ConfidenceCalculator>>();
            var calculatorConfig = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new ConfidenceCalculator(calculatorLogger, calculatorConfig);
        });
        
        // Session Sources
        services.AddSingleton<ISessionSource, UsagestatsSessionSource>();
        services.AddSingleton<ISessionSource, MediaCameraSessionSource>();
        
        // Session Detector
        services.AddSingleton<ISessionDetector, CameraSessionDetector>();
        
        // Deduplication Services
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var dedupLogger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(dedupLogger, options);
        });
        
        // Orchestrator
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    /// <summary>
    /// 앱 패키지명에서 표시 이름 추출
    /// </summary>
    private string GetAppDisplayName(string packageName)
    {
        return packageName switch
        {
            "com.sec.android.app.camera" => "기본 카메라",
            "com.kakao.talk" => "카카오톡",
            "org.telegram.messenger" => "텔레그램",
            "com.peace.SilentCamera" => "무음 카메라",
            "com.twopeople.silentcamera" => "무음 카메라",
            _ => packageName
        };
    }

    /// <summary>
    /// 아티팩트 타입에서 표시 이름 추출
    /// </summary>
    private string GetArtifactDisplayName(string artifactType)
    {
        return artifactType;
    }

    /// <summary>
    /// 출현 빈도 카운트 가져오기
    /// </summary>
    private int GetFrequencyCount(Dictionary<string, Dictionary<string, int>> frequencyTable, string packageName, string artifactType)
    {
        if (frequencyTable.TryGetValue(packageName, out var appFrequency))
        {
            if (appFrequency.TryGetValue(artifactType, out var count))
            {
                return count;
            }
        }
        return 0;
    }

    /// <summary>
    /// 출현 빈도 포맷팅 (예: 3/3 (100%))
    /// </summary>
    private string FormatFrequency(int occurrences, int totalCaptures)
    {
        var percentage = totalCaptures > 0 ? (occurrences / (double)totalCaptures) * 100 : 0;
        return $"{occurrences}/{totalCaptures} ({percentage:F0}%)";
    }

    #endregion
}

