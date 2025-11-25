using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
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
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

/// <summary>
/// 정밀한 촬영 시각 결정 메커니즘(GetPreciseCaptureTime) 타당성 검증 테스트 (예비 실험)
/// </summary>
/// <remarks>
/// 목적:
/// - 예비 실험에서 FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 추출
/// - 메커니즘 적용 전후 타임스탬프 차이 측정
/// - CaptureTime과 실제 정밀한 아티팩트 타임스탬프 일치 여부 검증
/// 
/// 논문 반영:
/// - 제4장 제4절: 정밀한 촬영 시각 결정 메커니즘 설계
/// - 부록 3, 3.4절: 예비 실험 기반 방법론 및 측정 데이터
/// - 제5장 제3절: 본 실험 기반 타당성 검증 (예비 실험 데이터 재분석)
/// </remarks>
public sealed class GetPreciseCaptureTimeValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    private readonly string _projectRoot;
    
    private AnalysisResult? _analysisResult;
    private List<NormalizedLogEvent>? _allParsedEvents;

    public GetPreciseCaptureTimeValidationTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        
        var currentDir = Directory.GetCurrentDirectory();
        _projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(_projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(_projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🔬 정밀한 촬영 시각 결정 메커니즘 타당성 검증 테스트 초기화 (예비 실험)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 예비 실험 1-3차 분석 (ArtifactWeights.PreliminaryTimeRanges 공용 상수 사용)
        var allEvents = new List<NormalizedLogEvent>();
        var allSessions = new List<CameraSession>();
        var allCaptures = new List<CameraCaptureEvent>();

        for (int prelimNum = 1; prelimNum <= 3; prelimNum++)
        {
            // ArtifactWeights.PreliminaryTimeRanges 공용 상수 사용
            if (!ArtifactWeights.PreliminaryTimeRanges.TryGetValue(prelimNum, out var timeRange))
            {
                _output.WriteLine($"⚠️ 예비 실험 {prelimNum}차의 시간 범위를 찾을 수 없습니다.");
                continue;
            }
            
            var dir = timeRange.DirectoryName;
            var startTime = timeRange.StartTime;
            var endTime = timeRange.EndTime;
            var samplePath = Path.Combine(_sampleLogsPath, dir);
            
            _output.WriteLine($"📂 예비 실험 {prelimNum}차: {dir}");
            
            // 로그 파싱
            var parsedEvents = await ParseSampleLogsAsync(samplePath, startTime, endTime);
            allEvents.AddRange(parsedEvents);
            
            // 분석 실행
            var orchestrator = CreateOrchestrator();
            var result = await orchestrator.AnalyzeAsync(
                parsedEvents,
                CreateAnalysisOptions());
            
            allSessions.AddRange(result.Sessions);
            allCaptures.AddRange(result.CaptureEvents);
            
            _output.WriteLine($"  세션: {result.Sessions.Count}개, 촬영: {result.CaptureEvents.Count}개\n");
        }

        _allParsedEvents = allEvents;
        _analysisResult = new AnalysisResult
        {
            Sessions = allSessions,
            CaptureEvents = allCaptures
        };

        _output.WriteLine($"✅ 예비 실험 1-3차 분석 완료");
        _output.WriteLine($"   - 총 세션: {allSessions.Count}개");
        _output.WriteLine($"   - 총 촬영: {allCaptures.Count}개\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 추출 및 타임스탬프 차이 측정
    /// </summary>
    [Fact]
    public void Validate_GetPreciseCaptureTime_Mechanism_Preliminary()
    {
        _output.WriteLine("\n📊 정밀한 촬영 시각 결정 메커니즘 타당성 검증 (예비 실험)\n");
        
        // 0. 모든 촬영의 keyArtifact 타입 확인 (디버깅용)
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("모든 촬영의 keyArtifact 타입 확인");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        var allKeyArtifactTypes = _analysisResult!.CaptureEvents
            .Select(c => 
            {
                var keyType = "알 수 없음";
                if (c.Metadata.TryGetValue("key_artifact_type", out var type))
                {
                    keyType = type ?? "알 수 없음";
                }
                else if (c.decisiveArtifact.HasValue)
                {
                    var keyArtifact = _allParsedEvents!.FirstOrDefault(e => e.EventId == c.decisiveArtifact.Value);
                    keyType = keyArtifact?.EventType ?? "알 수 없음";
                }
                return new { Capture = c, KeyType = keyType };
            })
            .ToList();
        
        var keyArtifactTypeGroups = allKeyArtifactTypes
            .GroupBy(x => x.KeyType)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        _output.WriteLine($"총 촬영 수: {_analysisResult.CaptureEvents.Count}개\n");
        _output.WriteLine("keyArtifact 타입별 분포:");
        foreach (var group in keyArtifactTypeGroups)
        {
            _output.WriteLine($"  - {group.Key}: {group.Count()}개");
        }
        _output.WriteLine("");
        
        // 각 촬영의 상세 정보 출력
        foreach (var item in allKeyArtifactTypes.OrderBy(x => x.Capture.CaptureTime))
        {
            var experimentNum = GetExperimentNumber(item.Capture);
            _output.WriteLine($"  [{experimentNum}] {item.Capture.PackageName,-25} | {item.Capture.CaptureTime:HH:mm:ss.fff} | keyArtifact: {item.KeyType}");
        }
        _output.WriteLine("");
        
        // 🔍 디버깅: 카카오톡 촬영의 세션 컨텍스트와 VIBRATION_EVENT 비교 분석
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("🔍 카카오톡 촬영 세션 컨텍스트 및 VIBRATION_EVENT 비교 분석");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        var kakaoCaptures = _analysisResult!.CaptureEvents
            .Where(c => c.PackageName == "com.kakao.talk")
            .OrderBy(c => c.CaptureTime)
            .ToList();
        
        foreach (var capture in kakaoCaptures)
        {
            var experimentNum = GetExperimentNumber(capture);
            var session = _analysisResult!.Sessions.FirstOrDefault(s => s.SessionId == capture.ParentSessionId);
            
            if (session == null)
            {
                _output.WriteLine($"⚠️ [{experimentNum}] 세션을 찾을 수 없습니다.");
                continue;
            }
            
            _output.WriteLine($"\n[{experimentNum}] 카카오톡 촬영 분석");
            _output.WriteLine($"  촬영 시각: {capture.CaptureTime:HH:mm:ss.fff}");
            _output.WriteLine($"  세션 시간: {session.StartTime:HH:mm:ss.fff} ~ {session.EndTime:HH:mm:ss.fff}");
            _output.WriteLine($"  세션 확장 후: {session.StartTime:HH:mm:ss.fff} ~ {session.EndTime?.AddSeconds(10):HH:mm:ss.fff}");
            
            // 세션 시간 범위 내의 모든 VIBRATION_EVENT 찾기
            var sessionStart = session.StartTime;
            var sessionEnd = session.EndTime?.AddSeconds(10) ?? DateTime.MaxValue;
            
            var sessionVibrationEvents = _allParsedEvents!
                .Where(e => e.EventType == "VIBRATION_EVENT")
                .Where(e => e.PackageName == "com.kakao.talk" || e.PackageName == "com.sec.android.app.camera")
                .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
                .OrderBy(e => e.Timestamp)
                .ToList();
            
            _output.WriteLine($"  세션 시간 범위 내 VIBRATION_EVENT: {sessionVibrationEvents.Count}개");
            
            foreach (var vibEvent in sessionVibrationEvents)
            {
                var hapticType = vibEvent.Attributes.TryGetValue("hapticType", out var ht) ? ht?.ToString() : "없음";
                var status = vibEvent.Attributes.TryGetValue("status", out var st) ? st?.ToString() : "없음";
                _output.WriteLine($"    - {vibEvent.Timestamp:HH:mm:ss.fff} | hapticType={hapticType} | status={status} | Package={vibEvent.PackageName}");
            }
            
            // SourceEventIds에 포함된 VIBRATION_EVENT 찾기
            var sourceVibrationEvents = _allParsedEvents!
                .Where(e => capture.SourceEventIds.Contains(e.EventId))
                .Where(e => e.EventType == "VIBRATION_EVENT")
                .OrderBy(e => e.Timestamp)
                .ToList();
            
            _output.WriteLine($"  SourceEventIds에 포함된 VIBRATION_EVENT: {sourceVibrationEvents.Count}개");
            
            foreach (var vibEvent in sourceVibrationEvents)
            {
                var hapticType = vibEvent.Attributes.TryGetValue("hapticType", out var ht) ? ht?.ToString() : "없음";
                var status = vibEvent.Attributes.TryGetValue("status", out var st) ? st?.ToString() : "없음";
                var inSession = vibEvent.Timestamp >= sessionStart && vibEvent.Timestamp <= sessionEnd ? "세션 내" : "세션 밖";
                _output.WriteLine($"    - {vibEvent.Timestamp:HH:mm:ss.fff} | hapticType={hapticType} | status={status} | {inSession}");
            }
            
            // keyArtifact 확인
            if (capture.decisiveArtifact.HasValue)
            {
                var keyArtifact = _allParsedEvents!.FirstOrDefault(e => e.EventId == capture.decisiveArtifact.Value);
                if (keyArtifact != null)
                {
                    _output.WriteLine($"  keyArtifact: {keyArtifact.EventType} ({keyArtifact.Timestamp:HH:mm:ss.fff})");
                    if (keyArtifact.EventType == "VIBRATION_EVENT")
                    {
                        var hapticType = keyArtifact.Attributes.TryGetValue("hapticType", out var ht) ? ht?.ToString() : "없음";
                        _output.WriteLine($"    hapticType={hapticType}");
                    }
                }
            }
        }
        _output.WriteLine("");
        
        // 1. FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 추출
        var foregroundServiceCases = _analysisResult.CaptureEvents
            .Where(c => 
            {
                // Metadata에서 확인
                if (c.Metadata.TryGetValue("key_artifact_type", out var keyType) && 
                    keyType == "FOREGROUND_SERVICE")
                {
                    return true;
                }
                
                // decisiveArtifact에서 직접 확인
                if (c.decisiveArtifact.HasValue)
                {
                    var keyArtifact = _allParsedEvents!.FirstOrDefault(e => e.EventId == c.decisiveArtifact.Value);
                    return keyArtifact?.EventType == "FOREGROUND_SERVICE";
                }
                
                return false;
            })
            .ToList();
        
        _output.WriteLine($"FOREGROUND_SERVICE가 keyArtifact인 케이스: {foregroundServiceCases.Count}개\n");
        
        if (foregroundServiceCases.Count == 0)
        {
            _output.WriteLine("⚠️ FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스가 없습니다.");
            _output.WriteLine("   부록 3에 따르면 예비 실험 2차(10:11:31) 또는 예비 실험 3차에서 1건이 발견되었다고 하는데,");
            _output.WriteLine("   현재 분석 결과에서는 발견되지 않았습니다.");
            _output.WriteLine("   이는 다른 아티팩트(DATABASE_INSERT, VIBRATION_EVENT 등)가 keyArtifact로 사용되었을 가능성이 있습니다.\n");
            return;
        }
        
        // 2. 각 케이스별 타임스탬프 차이 측정
        var timestampDifferences = new List<(CameraCaptureEvent capture, DateTime foregroundTimestamp, DateTime preciseTimestamp, TimeSpan difference, string preciseArtifactType)>();
        
        foreach (var capture in foregroundServiceCases)
        {
            // keyArtifact (FOREGROUND_SERVICE)의 타임스탬프
            var keyArtifactId = capture.decisiveArtifact;
            if (!keyArtifactId.HasValue) continue;
            
            var keyArtifact = _allParsedEvents!
                .FirstOrDefault(e => e.EventId == keyArtifactId.Value);
            
            if (keyArtifact == null || keyArtifact.EventType != "FOREGROUND_SERVICE") continue;
            
            var foregroundTimestamp = keyArtifact.Timestamp;
            
            // 메커니즘으로 결정된 타임스탬프 (CaptureTime)
            var preciseTimestamp = capture.CaptureTime;
            
            // 타임스탬프 차이
            var difference = preciseTimestamp - foregroundTimestamp;
            
            // 사용된 정밀한 아티팩트 타입 확인 (역추적 방식)
            var allArtifactIds = capture.SourceEventIds;
            var allSourceArtifacts = _allParsedEvents!
                .Where(e => allArtifactIds.Contains(e.EventId))
                .ToList();
            
            // 디버깅: SourceEventIds에 포함된 모든 아티팩트 타입 출력
            var sourceArtifactTypes = allSourceArtifacts
                .Select(e => $"{e.EventType} ({e.Timestamp:HH:mm:ss.fff})")
                .ToList();
            
            // FOREGROUND_SERVICE를 제외한 아티팩트 목록
            var allArtifacts = allSourceArtifacts
                .Where(e => e.EventType != "FOREGROUND_SERVICE")
                .ToList();
            
            // CaptureTime과 일치하는 아티팩트 찾기 (1ms 이내 허용)
            var preciseArtifact = allArtifacts
                .Where(e => Math.Abs((e.Timestamp - preciseTimestamp).TotalMilliseconds) < 1.0)
                .OrderByDescending(e => 
                    e.EventType == "DATABASE_INSERT" ? 3 :
                    e.EventType == "VIBRATION_EVENT" ? 2 : 1)
                .ThenBy(e => e.Timestamp)
                .FirstOrDefault();
            
            // 일치하는 아티팩트가 없으면 비즈니스 로직과 동일한 방식으로 추정
            if (preciseArtifact == null)
            {
                preciseArtifact = allArtifacts
                    .OrderByDescending(e => 
                        e.EventType == "DATABASE_INSERT" ? 3 :
                        e.EventType == "VIBRATION_EVENT" ? 2 : 1)
                    .ThenBy(e => e.Timestamp)
                    .FirstOrDefault();
            }
            
            var preciseArtifactType = preciseArtifact?.EventType ?? "NONE";
            
            // 디버깅 정보 출력 (모든 케이스에 대해)
            _output.WriteLine($"\n📋 케이스 분석: {GetExperimentNumber(capture)} - {capture.PackageName}");
            _output.WriteLine($"  FOREGROUND_SERVICE 타임스탬프: {foregroundTimestamp:HH:mm:ss.fff}");
            _output.WriteLine($"  CaptureTime (정밀 타임스탬프): {preciseTimestamp:HH:mm:ss.fff}");
            _output.WriteLine($"  타임스탬프 차이: {difference.TotalMilliseconds:F0}ms");
            _output.WriteLine($"  SourceEventIds에 포함된 아티팩트: {string.Join(", ", sourceArtifactTypes)}");
            _output.WriteLine($"  FOREGROUND_SERVICE를 제외한 아티팩트: {allArtifacts.Count}개");
            if (allArtifacts.Count > 0)
            {
                _output.WriteLine($"    - {string.Join(", ", allArtifacts.Select(e => $"{e.EventType} ({e.Timestamp:HH:mm:ss.fff})"))}");
            }
            if (preciseArtifact != null)
            {
                var artifactDiff = Math.Abs((preciseArtifact.Timestamp - preciseTimestamp).TotalMilliseconds);
                _output.WriteLine($"  추정된 정밀 아티팩트: {preciseArtifact.EventType} ({preciseArtifact.Timestamp:HH:mm:ss.fff})");
                _output.WriteLine($"  CaptureTime과의 차이: {artifactDiff:F2}ms");
            }
            else
            {
                _output.WriteLine($"  추정된 정밀 아티팩트: 없음 (NONE)");
            }
            _output.WriteLine("");
            
            // 디버깅 정보 출력 (문제 진단용)
            if (preciseArtifactType == "NONE" && allArtifacts.Count == 0)
            {
                _output.WriteLine($"  ⚠️ 디버깅: SourceEventIds에 포함된 아티팩트: {string.Join(", ", sourceArtifactTypes)}");
                _output.WriteLine($"  ⚠️ 디버깅: FOREGROUND_SERVICE를 제외한 아티팩트가 없음");
                _output.WriteLine($"  ⚠️ 디버깅: GetPreciseCaptureTime이 FOREGROUND_SERVICE 타임스탬프를 반환한 것으로 추정됨");
                
                // 세션의 모든 이벤트 확인 (EventCorrelationWindow 밖의 아티팩트도 확인)
                var session = _analysisResult!.Sessions.FirstOrDefault(s => s.SessionId == capture.ParentSessionId);
                if (session != null)
                {
                    var sessionStart = session.StartTime;
                    var sessionEnd = session.EndTime;
                    var correlationWindow = TimeSpan.FromSeconds(ArtifactWeights.EventCorrelationWindowSeconds);
                    var windowStart = foregroundTimestamp - correlationWindow;
                    var windowEnd = foregroundTimestamp + correlationWindow;
                    
                    var sessionEvents = _allParsedEvents!
                        .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
                        .Where(e => e.EventType != "FOREGROUND_SERVICE")
                        .Where(e => e.EventType == "DATABASE_INSERT" || e.EventType == "VIBRATION_EVENT" || 
                                   e.EventType == "PLAYER_EVENT" || e.EventType == "URI_PERMISSION_GRANT")
                        .ToList();
                    
                    var eventsInWindow = sessionEvents
                        .Where(e => e.Timestamp >= windowStart && e.Timestamp <= windowEnd)
                        .ToList();
                    
                    var eventsOutOfWindow = sessionEvents
                        .Where(e => e.Timestamp < windowStart || e.Timestamp > windowEnd)
                        .ToList();
                    
                    _output.WriteLine($"  ⚠️ 디버깅: 세션 시간 범위: {sessionStart:HH:mm:ss.fff} ~ {sessionEnd:HH:mm:ss.fff}");
                    _output.WriteLine($"  ⚠️ 디버깅: EventCorrelationWindow: {correlationWindow.TotalSeconds}초 (±{correlationWindow.TotalSeconds}초)");
                    _output.WriteLine($"  ⚠️ 디버깅: 윈도우 범위: {windowStart:HH:mm:ss.fff} ~ {windowEnd:HH:mm:ss.fff}");
                    _output.WriteLine($"  ⚠️ 디버깅: 세션 내 정밀 아티팩트 후보 (윈도우 내): {eventsInWindow.Count}개");
                    if (eventsInWindow.Count > 0)
                    {
                        _output.WriteLine($"  ⚠️ 디버깅:   - {string.Join(", ", eventsInWindow.Select(e => $"{e.EventType} ({e.Timestamp:HH:mm:ss.fff})"))}");
                    }
                    _output.WriteLine($"  ⚠️ 디버깅: 세션 내 정밀 아티팩트 후보 (윈도우 밖): {eventsOutOfWindow.Count}개");
                    if (eventsOutOfWindow.Count > 0)
                    {
                        _output.WriteLine($"  ⚠️ 디버깅:   - {string.Join(", ", eventsOutOfWindow.Select(e => $"{e.EventType} ({e.Timestamp:HH:mm:ss.fff})"))}");
                        _output.WriteLine($"  ⚠️ 디버깅:   → EventCorrelationWindow 밖에 있어서 allArtifacts에 포함되지 않음");
                    }
                }
            }
            else if (preciseArtifactType == "NONE" && allArtifacts.Count > 0)
            {
                _output.WriteLine($"  ⚠️ 디버깅: SourceEventIds에 포함된 아티팩트: {string.Join(", ", sourceArtifactTypes)}");
                _output.WriteLine($"  ⚠️ 디버깅: FOREGROUND_SERVICE를 제외한 아티팩트: {string.Join(", ", allArtifacts.Select(e => $"{e.EventType} ({e.Timestamp:HH:mm:ss.fff})"))}");
                _output.WriteLine($"  ⚠️ 디버깅: CaptureTime({preciseTimestamp:HH:mm:ss.fff})과 일치하는 아티팩트를 찾지 못함");
            }
            
            timestampDifferences.Add((capture, foregroundTimestamp, preciseTimestamp, difference, preciseArtifactType));
        }
        
        // 3. 결과 출력
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("[표] FOREGROUND_SERVICE keyArtifact 케이스별 타임스탬프 차이 (예비 실험)");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        _output.WriteLine($"| {"실험",-15} | {"패키지명",-25} | {"FOREGROUND 타임스탬프",-25} | {"정밀 타임스탬프",-25} | {"차이 (ms)",-12} | {"정밀 아티팩트",-20} |");
        _output.WriteLine($"|{new string('-', 17)}|{new string('-', 27)}|{new string('-', 27)}|{new string('-', 27)}|{new string('-', 14)}|{new string('-', 22)}|");
        
        foreach (var (capture, foregroundTs, preciseTs, diff, preciseType) in timestampDifferences)
        {
            var experimentNum = GetExperimentNumber(capture);
            var diffMs = $"{diff.TotalMilliseconds:F0}ms";
            _output.WriteLine($"| {experimentNum,-15} | {capture.PackageName,-25} | {foregroundTs:HH:mm:ss.fff,-25} | {preciseTs:HH:mm:ss.fff,-25} | {diffMs,-12} | {preciseType,-20} |");
        }
        
        _output.WriteLine("");
        
        // 4. 통계 분석 및 검증
        if (timestampDifferences.Count > 0)
        {
            var differences = timestampDifferences.Select(t => t.difference.TotalMilliseconds).ToList();
            var avgDifference = differences.Average();
            var minDifference = differences.Min();
            var maxDifference = differences.Max();
            
            _output.WriteLine("─────────────────────────────────────────────────────────────────────");
            _output.WriteLine("통계 분석");
            _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
            
            _output.WriteLine($"평균 타임스탬프 차이: {avgDifference:F2}ms");
            _output.WriteLine($"최소 타임스탬프 차이: {minDifference:F2}ms");
            _output.WriteLine($"최대 타임스탬프 차이: {maxDifference:F2}ms");
            _output.WriteLine($"케이스 수: {timestampDifferences.Count}개\n");
            
            // 정밀한 아티팩트 타입별 분포
            var preciseArtifactDistribution = timestampDifferences
                .GroupBy(t => t.preciseArtifactType)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            _output.WriteLine("정밀한 아티팩트 타입별 분포:");
            foreach (var group in preciseArtifactDistribution)
            {
                _output.WriteLine($"  - {group.Key}: {group.Count()}개");
            }
            _output.WriteLine("");
            
            // 검증: FOREGROUND_SERVICE 타임스탬프가 1초 단위로 반올림되었는지 확인
            _output.WriteLine("─────────────────────────────────────────────────────────────────────");
            _output.WriteLine("검증: FOREGROUND_SERVICE 타임스탬프 정밀도 확인");
            _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
            
            var foregroundTimestamps = foregroundServiceCases
                .Select(c => 
                {
                    var keyArtifactId = c.decisiveArtifact;
                    if (!keyArtifactId.HasValue) return null;
                    var keyArtifact = _allParsedEvents!.FirstOrDefault(e => e.EventId == keyArtifactId.Value);
                    return keyArtifact?.Timestamp;
                })
                .Where(ts => ts.HasValue)
                .Select(ts => ts!.Value)
                .ToList();
            
            var allRoundedToSecond = foregroundTimestamps.All(ts => ts.Millisecond == 0);
            if (allRoundedToSecond)
            {
                _output.WriteLine("✅ 모든 FOREGROUND_SERVICE 타임스탬프가 1초 단위로 반올림됨 (밀리초 = 0)");
            }
            else
            {
                var nonRoundedCount = foregroundTimestamps.Count(ts => ts.Millisecond != 0);
                _output.WriteLine($"⚠️ {nonRoundedCount}개의 FOREGROUND_SERVICE 타임스탬프가 밀리초 단위를 포함함");
            }
            _output.WriteLine("");
            
            // 검증: CaptureTime과 실제 아티팩트 타임스탬프 일치 확인
            _output.WriteLine("─────────────────────────────────────────────────────────────────────");
            _output.WriteLine("검증: CaptureTime과 실제 아티팩트 타임스탬프 일치 확인");
            _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
            
            var verificationResults = new List<(string experimentNum, string packageName, DateTime captureTime, DateTime artifactTimestamp, double diffMs, string artifactType)>();
            
            foreach (var (capture, _, preciseTs, _, preciseType) in timestampDifferences)
            {
                var experimentNum = GetExperimentNumber(capture);
                var allArtifactIds = capture.SourceEventIds;
                var allArtifacts = _allParsedEvents!
                    .Where(e => allArtifactIds.Contains(e.EventId))
                    .Where(e => e.EventType != "FOREGROUND_SERVICE")
                    .ToList();
                
                // CaptureTime과 일치하는 아티팩트 찾기
                var matchingArtifact = allArtifacts
                    .Where(e => Math.Abs((e.Timestamp - preciseTs).TotalMilliseconds) < 1.0)
                    .OrderByDescending(e => 
                        e.EventType == "DATABASE_INSERT" ? 3 :
                        e.EventType == "VIBRATION_EVENT" ? 2 : 1)
                    .ThenBy(e => e.Timestamp)
                    .FirstOrDefault();
                
                if (matchingArtifact != null)
                {
                    var diffMs = Math.Abs((matchingArtifact.Timestamp - preciseTs).TotalMilliseconds);
                    verificationResults.Add((experimentNum, capture.PackageName, preciseTs, matchingArtifact.Timestamp, diffMs, matchingArtifact.EventType));
                }
            }
            
            if (verificationResults.Count == timestampDifferences.Count)
            {
                _output.WriteLine($"✅ 모든 케이스({verificationResults.Count}개)에서 CaptureTime과 실제 아티팩트 타임스탬프가 일치함 (1ms 이내)");
                var maxDiff = verificationResults.Max(r => r.diffMs);
                _output.WriteLine($"   최대 타임스탬프 차이: {maxDiff:F2}ms\n");
                
                // 논문 작성용 요약
                _output.WriteLine("─────────────────────────────────────────────────────────────────────");
                _output.WriteLine("📝 논문 작성용 요약 (제5장 제3절 표 26)");
                _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
                
                _output.WriteLine($"예비 실험:");
                _output.WriteLine($"  - FOREGROUND_SERVICE가 keyArtifact인 케이스: {timestampDifferences.Count}개");
                _output.WriteLine($"  - FOREGROUND_SERVICE와 정밀한 아티팩트 간 타임스탬프 차이: {avgDifference:F0}ms (범위: {minDifference:F0}ms ~ {maxDifference:F0}ms)");
                _output.WriteLine($"  - 사용된 정밀한 아티팩트 타입: {string.Join(", ", preciseArtifactDistribution.Select(g => $"{g.Key} ({g.Count()}개)"))}");
                _output.WriteLine($"  - CaptureTime과 실제 정밀한 아티팩트 타임스탬프 일치: 100% ({verificationResults.Count}/{timestampDifferences.Count}, 최대 차이 {maxDiff:F2}ms)\n");
            }
            else
            {
                var matchedCount = verificationResults.Count;
                _output.WriteLine($"⚠️ {matchedCount}/{timestampDifferences.Count}개 케이스에서만 CaptureTime과 아티팩트 타임스탬프가 일치함\n");
            }
        }
    }
    
    #region Helper Methods
    
    private string GetExperimentNumber(CameraCaptureEvent capture)
    {
        // CaptureTime을 기반으로 예비 실험 번호 추정 (ArtifactWeights.PreliminaryTimeRanges 사용)
        var captureTime = capture.CaptureTime;
        
        foreach (var (prelimNum, timeRange) in ArtifactWeights.PreliminaryTimeRanges)
        {
            if (captureTime >= timeRange.StartTime && captureTime <= timeRange.EndTime)
            {
                return $"예비 실험 {prelimNum}차";
            }
        }
        
        return "알 수 없음";
    }
    
    /// <summary>
    /// 샘플 로그 파싱
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string samplePath,
        DateTime startTime,
        DateTime endTime)
    {
        var allEvents = new List<NormalizedLogEvent>();
        
        if (!Directory.Exists(samplePath))
        {
            _output.WriteLine($"  ⚠️  경로가 존재하지 않습니다: {samplePath}");
            return allEvents;
        }
        
        // 로그 파일 설정 맵핑
        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera.log"] = "adb_media_camera_config.yaml",
            ["media_camera_worker.log"] = "adb_media_camera_worker_config.yaml",
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
    
    /// <summary>
    /// 개별 로그 파일 파싱
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string samplePath,
        string logFileName,
        string configFileName,
        DateTime startTime,
        DateTime endTime)
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
        
        // YAML 설정 로드 (Parser 네임스페이스 사용)
        var configLoader = new AndroidAdbAnalyze.Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);
        
        // DeviceInfo 생성
        var deviceInfo = ArtifactWeights.CreateTestDeviceInfo();
        
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
            EventCorrelationWindow = TimeSpan.FromSeconds(ArtifactWeights.EventCorrelationWindowSeconds),
            DeduplicationSimilarityThreshold = ArtifactWeights.DeduplicationSimilarityThreshold,
            SameCameraUsageTimeThreshold = TimeSpan.FromSeconds(ArtifactWeights.SameCameraUsageTimeThreshold),
            CaptureDeduplicationWindow = TimeSpan.FromMilliseconds(ArtifactWeights.CaptureDeduplicationWindowMs)
        };
    }
    
    /// <summary>
    /// Orchestrator 생성
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // YAML 설정 로드
        var configPath = Path.Combine(_projectRoot, "AndroidAdbAnalyze.Analysis", "Configs", "artifact-detection-config.example.yaml");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        var artifactConfig = AndroidAdbAnalyze.Analysis.Configuration.YamlConfigurationLoader.LoadFromFile(configPath);
        
        // 설정 등록
        services.AddSingleton(artifactConfig);
        services.AddSingleton(CreateAnalysisOptions());
        
        // 서비스 등록
        RegisterServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }
    
    /// <summary>
    /// 서비스 등록
    /// </summary>
    private void RegisterServices(IServiceCollection services)
    {
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies
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
        
        // Confidence Calculator
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
        
        // Analysis Orchestrator
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }
    
    #endregion
}

