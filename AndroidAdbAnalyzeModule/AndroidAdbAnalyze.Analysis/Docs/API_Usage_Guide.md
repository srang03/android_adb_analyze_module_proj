# AndroidAdbAnalyze.Analysis - API 사용 가이드

## 📋 문서 정보

**버전**: 1.0  
**작성일**: 2025-10-09  
**대상 독자**: 상위 앱 개발자 (WPF Application)  
**목적**: Analysis DLL API 사용 방법 및 예제 제공

---

## 1. 빠른 시작 (Quick Start)

###  1.1 NuGet 패키지 설치 (프로덕션 배포 시)
```xml
<PackageReference Include="AndroidAdbAnalyze.Parser" Version="1.0.0" />
<PackageReference Include="AndroidAdbAnalyze.Analysis" Version="1.0.0" />
```

### 1.2 의존성 주입 설정
```csharp
using AndroidAdbAnalyze.Analysis.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// 1. ServiceCollection 생성
var services = new ServiceCollection();

// 2. 로깅 설정 (선택사항)
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// 3. Analysis 서비스 등록 (모든 의존성 자동 등록)
services.AddAndroidAdbAnalysis();

// 4. ServiceProvider 빌드
var serviceProvider = services.BuildServiceProvider();
```

### 1.3 기본 사용 예제
```csharp
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Parser;
using AndroidAdbAnalyze.Parser.Core.Models;

// 1. Parser로 로그 파일 파싱
var parserConfigPath = "path/to/configs";
var logDirectory = "path/to/log/files";

var parser = new AdbLogParser(parserConfigPath);
var parsingResult = await parser.ParseAllLogsAsync(logDirectory);

// 2. AnalysisOrchestrator 가져오기
var orchestrator = serviceProvider.GetRequiredService<IAnalysisOrchestrator>();

// 3. 분석 옵션 설정 (선택사항, 기본값 사용 가능)
var options = new AnalysisOptions
{
    MinConfidenceThreshold = 0.3,
    MaxSessionGap = TimeSpan.FromMinutes(5),
    EventCorrelationWindow = TimeSpan.FromSeconds(30)
};

// 4. 분석 실행
var analysisResult = await orchestrator.AnalyzeAsync(
    parsingResult.Events, 
    options);

// 5. 결과 활용
Console.WriteLine($"성공: {analysisResult.Success}");
Console.WriteLine($"세션 수: {analysisResult.Sessions.Count}");
Console.WriteLine($"촬영 수: {analysisResult.CaptureEvents.Count}");

// 6. HTML 보고서 생성 (선택사항)
var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();
var htmlReport = reportGenerator.GenerateReport(analysisResult);
File.WriteAllText("report.html", htmlReport);
```

---

## 2. 핵심 API 참조

### 2.1 IAnalysisOrchestrator (주요 진입점)

#### **인터페이스 정의**
```csharp
public interface IAnalysisOrchestrator
{
    Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions? options = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
```

#### **메서드 설명**
| 메서드 | 설명 | 반환값 |
|--------|------|--------|
| `AnalyzeAsync` | 파싱된 로그 이벤트를 분석하여 세션 및 촬영 감지 | `Task<AnalysisResult>` |

#### **매개변수**
| 이름 | 타입 | 필수 | 설명 |
|------|------|------|------|
| `events` | `IReadOnlyList<NormalizedLogEvent>` | ✅ | Parser DLL이 생성한 파싱된 이벤트 배열 |
| `options` | `AnalysisOptions?` | ❌ | 분석 옵션 (null 시 기본값 사용) |
| `progress` | `IProgress<int>?` | ❌ | 진행률 보고 (0~100%) |
| `cancellationToken` | `CancellationToken` | ❌ | 취소 토큰 |

#### **예제 1: 기본 분석**
```csharp
var orchestrator = serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
var result = await orchestrator.AnalyzeAsync(events);

if (result.Success)
{
    Console.WriteLine($"분석 완료: {result.Sessions.Count}개 세션, {result.CaptureEvents.Count}개 촬영");
}
else
{
    Console.WriteLine($"분석 실패: {string.Join(", ", result.Errors)}");
}
```

#### **예제 2: 진행률 보고**
```csharp
var progress = new Progress<int>(percent =>
{
    Console.WriteLine($"분석 진행률: {percent}%");
    // WPF: ProgressBar.Value = percent;
});

var result = await orchestrator.AnalyzeAsync(events, null, progress);
```

#### **예제 3: 취소 지원**
```csharp
using var cts = new CancellationTokenSource();

// 5초 후 자동 취소
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    var result = await orchestrator.AnalyzeAsync(
        events, 
        null, 
        null, 
        cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("분석이 취소되었습니다.");
}
```

---

### 2.2 AnalysisOptions (분석 옵션)

#### **클래스 정의**
```csharp
public sealed class AnalysisOptions
{
    // 패키지 필터링
    public IReadOnlyList<string>? PackageWhitelist { get; init; }
    public IReadOnlyList<string>? PackageBlacklist { get; init; }
    
    // 시간 윈도우
    public TimeSpan MaxSessionGap { get; init; }              // 기본값: 5분
    public TimeSpan EventCorrelationWindow { get; init; }     // 기본값: 30초
    
    // 신뢰도
    public double MinConfidenceThreshold { get; init; }       // 기본값: 0.3
    
    // 경로 패턴
    public IReadOnlyList<string> ScreenshotPathPatterns { get; init; }
    public IReadOnlyList<string> DownloadPathPatterns { get; init; }
    
    // 옵션
    public bool EnableIncompleteSessionHandling { get; init; } // 기본값: true
}
```

#### **기본값 생성**
```csharp
var options = new AnalysisOptions
{
    MinConfidenceThreshold = 0.3,
    MaxSessionGap = TimeSpan.FromMinutes(5),
    EventCorrelationWindow = TimeSpan.FromSeconds(30),
    ScreenshotPathPatterns = new[] { "screenshot", "Screenshot" },
    DownloadPathPatterns = new[] { "download", "Download" },
    EnableIncompleteSessionHandling = true
};
```

#### **예제 1: 특정 패키지만 분석**
```csharp
var options = new AnalysisOptions
{
    PackageWhitelist = new[] 
    { 
        "com.sec.android.app.camera",
        "com.kakao.talk" 
    }
};

var result = await orchestrator.AnalyzeAsync(events, options);
```

#### **예제 2: 시스템 패키지 제외**
```csharp
var options = new AnalysisOptions
{
    PackageBlacklist = new[] 
    { 
        "android",
        "com.android.systemui",
        "com.samsung.android" 
    }
};
```

#### **예제 3: 신뢰도 임계값 조정**
```csharp
// 높은 신뢰도 결과만 (오탐 최소화)
var options = new AnalysisOptions
{
    MinConfidenceThreshold = 0.7
};

// 낮은 신뢰도 포함 (누락 최소화)
var options = new AnalysisOptions
{
    MinConfidenceThreshold = 0.1
};
```

---

### 2.3 AnalysisResult (분석 결과)

#### **클래스 정의**
```csharp
public sealed class AnalysisResult
{
    public bool Success { get; init; }
    public IReadOnlyList<CameraSession> Sessions { get; init; }
    public IReadOnlyList<CameraCaptureEvent> CaptureEvents { get; init; }
    public IReadOnlyList<NormalizedLogEvent> OriginalEvents { get; init; }
    public IReadOnlyList<DeduplicationInfo> DeduplicationDetails { get; init; }
    public AnalysisStatistics? Statistics { get; init; }
    public IReadOnlyList<string> Errors { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }
}
```

#### **속성 설명**
| 속성 | 타입 | 설명 |
|------|------|------|
| `Success` | `bool` | 분석 성공 여부 |
| `Sessions` | `IReadOnlyList<CameraSession>` | 감지된 카메라 세션 목록 |
| `CaptureEvents` | `IReadOnlyList<CameraCaptureEvent>` | 감지된 촬영 이벤트 목록 |
| `OriginalEvents` | `IReadOnlyList<NormalizedLogEvent>` | 원본 이벤트 (참조용) |
| `DeduplicationDetails` | `IReadOnlyList<DeduplicationInfo>` | 중복 제거 상세 정보 |
| `Statistics` | `AnalysisStatistics?` | 분석 통계 |
| `Errors` | `IReadOnlyList<string>` | 에러 메시지 목록 |
| `Warnings` | `IReadOnlyList<string>` | 경고 메시지 목록 |

#### **예제: 결과 활용**
```csharp
var result = await orchestrator.AnalyzeAsync(events);

if (!result.Success)
{
    Console.WriteLine("분석 실패:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
    return;
}

// 세션 정보 출력
Console.WriteLine($"\n=== 카메라 세션 ({result.Sessions.Count}개) ===");
foreach (var session in result.Sessions)
{
    Console.WriteLine($"패키지: {session.PackageName}");
    Console.WriteLine($"시작: {session.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"종료: {session.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"지속시간: {session.Duration?.TotalSeconds:F1}초");
    Console.WriteLine($"신뢰도: {session.ConfidenceScore:P0}");
    Console.WriteLine($"촬영 횟수: {session.CaptureEventIds.Count}개");
    Console.WriteLine();
}

// 촬영 정보 출력
Console.WriteLine($"\n=== 촬영 이벤트 ({result.CaptureEvents.Count}개) ===");
foreach (var capture in result.CaptureEvents)
{
    Console.WriteLine($"시간: {capture.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}");
    Console.WriteLine($"패키지: {capture.PackageName}");
    Console.WriteLine($"파일: {capture.FilePath ?? "N/A"}");
    Console.WriteLine($"신뢰도: {capture.ConfidenceScore:P0}");
    Console.WriteLine($"추정: {(capture.IsEstimated ? "예" : "아니오")}");
    Console.WriteLine();
}

// 통계 출력
if (result.Statistics != null)
{
    Console.WriteLine($"\n=== 통계 ===");
    Console.WriteLine($"처리 시간: {result.Statistics.ProcessingDuration.TotalSeconds:F2}초");
    Console.WriteLine($"처리 이벤트: {result.Statistics.ProcessedEvents}개");
    Console.WriteLine($"세션: 완전 {result.Statistics.CompleteSessions}개, 불완전 {result.Statistics.IncompleteSessions}개");
    Console.WriteLine($"촬영: 확인 {result.Statistics.ConfirmedCaptures}개, 추정 {result.Statistics.EstimatedCaptures}개");
    Console.WriteLine($"평균 신뢰도: {result.Statistics.AverageConfidenceScore:P0}");
}
```

---

### 2.4 CameraSession (카메라 세션)

#### **클래스 정의**
```csharp
public sealed class CameraSession
{
    public Guid SessionId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public TimeSpan? Duration { get; init; }
    public string PackageName { get; init; }
    public int? ProcessId { get; init; }
    public bool IsIncomplete { get; init; }
    public SessionIncompleteReason? IncompleteReason { get; init; }
    public double ConfidenceScore { get; init; }
    public IReadOnlyList<Guid> SourceEventIds { get; init; }
    public IReadOnlyList<Guid> CaptureEventIds { get; init; }
    public IReadOnlyList<string> SourceLogTypes { get; init; }
}
```

#### **예제: 세션 필터링**
```csharp
// 특정 앱의 세션만
var kakaoSessions = result.Sessions
    .Where(s => s.PackageName.Contains("kakao.talk"))
    .ToList();

// 완전한 세션만
var completeSessions = result.Sessions
    .Where(s => !s.IsIncomplete)
    .ToList();

// 높은 신뢰도 세션만
var highConfidenceSessions = result.Sessions
    .Where(s => s.ConfidenceScore >= 0.8)
    .ToList();

// 촬영이 있는 세션만
var sessionsWithCaptures = result.Sessions
    .Where(s => s.CaptureEventIds.Count > 0)
    .ToList();
```

---

### 2.5 CameraCaptureEvent (촬영 이벤트)

#### **클래스 정의**
```csharp
public sealed class CameraCaptureEvent
{
    public Guid CaptureId { get; init; }
    public Guid ParentSessionId { get; init; }
    public DateTime CaptureTime { get; init; }
    public string PackageName { get; init; }
    public string? FilePath { get; init; }
    public string? FileUri { get; init; }
    public Guid PrimaryEvidenceId { get; init; }
    public IReadOnlyList<Guid> SupportingEvidenceIds { get; init; }
    public bool IsEstimated { get; init; }
    public double ConfidenceScore { get; init; }
    public IReadOnlyList<string> EvidenceTypes { get; init; }
    public IReadOnlyList<Guid> SourceEventIds { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}
```

#### **예제: 촬영 분석**
```csharp
// 확인된 촬영만 (추정 제외)
var confirmedCaptures = result.CaptureEvents
    .Where(c => !c.IsEstimated)
    .ToList();

// 파일 경로가 있는 촬영만
var capturesWithFile = result.CaptureEvents
    .Where(c => !string.IsNullOrEmpty(c.FilePath))
    .ToList();

// 특정 시간 범위의 촬영
var capturesInRange = result.CaptureEvents
    .Where(c => c.CaptureTime >= DateTime.Parse("2025-10-05 22:00:00") &&
                c.CaptureTime <= DateTime.Parse("2025-10-05 23:00:00"))
    .ToList();

// 증거 타입별 그룹화
var groupedByEvidence = result.CaptureEvents
    .GroupBy(c => string.Join(", ", c.EvidenceTypes.OrderBy(e => e)))
    .Select(g => new { Evidence = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count)
    .ToList();
```

---

## 3. HTML 보고서 생성

### 3.1 IReportGenerator

#### **인터페이스 정의**
```csharp
public interface IReportGenerator
{
    string Format { get; }
    string GenerateReport(AnalysisResult result);
}
```

#### **예제: 보고서 생성 및 저장**
```csharp
var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();

// HTML 보고서 생성
var htmlReport = reportGenerator.GenerateReport(analysisResult);

// 파일로 저장
var reportPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    $"analysis_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");

File.WriteAllText(reportPath, htmlReport, Encoding.UTF8);

// 브라우저로 열기 (선택사항)
System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = reportPath,
    UseShellExecute = true
});

Console.WriteLine($"보고서 생성 완료: {reportPath}");
```

---

## 4. WPF 통합 예제

### 4.1 ViewModel 구현
```csharp
public class AnalysisViewModel : INotifyPropertyChanged
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly IReportGenerator _reportGenerator;
    
    private int _progressPercentage;
    private bool _isAnalyzing;
    private string _statusMessage;
    
    public AnalysisViewModel(
        IAnalysisOrchestrator orchestrator,
        IReportGenerator reportGenerator)
    {
        _orchestrator = orchestrator;
        _reportGenerator = reportGenerator;
        
        AnalyzeCommand = new RelayCommand(async () => await AnalyzeAsync(), 
                                         () => !IsAnalyzing);
        GenerateReportCommand = new RelayCommand(GenerateReport, 
                                                () => AnalysisResult != null);
    }
    
    public ICommand AnalyzeCommand { get; }
    public ICommand GenerateReportCommand { get; }
    
    public int ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }
    
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => SetProperty(ref _isAnalyzing, value);
    }
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    
    public AnalysisResult? AnalysisResult { get; private set; }
    
    private async Task AnalyzeAsync()
    {
        try
        {
            IsAnalyzing = true;
            StatusMessage = "로그 파싱 중...";
            
            // 1. 로그 파일 파싱
            var parserConfigPath = "Configs";
            var logDirectory = "SampleLogs";
            
            var parser = new AdbLogParser(parserConfigPath);
            var parsingResult = await parser.ParseAllLogsAsync(logDirectory);
            
            // 2. 분석 실행 (진행률 보고)
            StatusMessage = "분석 중...";
            var progress = new Progress<int>(percent =>
            {
                ProgressPercentage = percent;
            });
            
            var options = new AnalysisOptions
            {
                MinConfidenceThreshold = 0.3
            };
            
            AnalysisResult = await _orchestrator.AnalyzeAsync(
                parsingResult.Events,
                options,
                progress);
            
            // 3. 결과 표시
            if (AnalysisResult.Success)
            {
                StatusMessage = $"분석 완료: {AnalysisResult.Sessions.Count}개 세션, " +
                               $"{AnalysisResult.CaptureEvents.Count}개 촬영";
            }
            else
            {
                StatusMessage = $"분석 실패: {string.Join(", ", AnalysisResult.Errors)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            ProgressPercentage = 0;
        }
    }
    
    private void GenerateReport()
    {
        if (AnalysisResult == null)
            return;
        
        try
        {
            StatusMessage = "보고서 생성 중...";
            
            var htmlReport = _reportGenerator.GenerateReport(AnalysisResult);
            
            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"analysis_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            
            File.WriteAllText(reportPath, htmlReport, Encoding.UTF8);
            
            StatusMessage = $"보고서 생성 완료: {reportPath}";
            
            // 브라우저로 열기
            Process.Start(new ProcessStartInfo
            {
                FileName = reportPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"보고서 생성 실패: {ex.Message}";
        }
    }
    
    // INotifyPropertyChanged 구현...
}
```

### 4.2 XAML 바인딩
```xml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Android ADB 로그 분석기" Height="600" Width="800">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- 제목 -->
        <TextBlock Grid.Row="0" Text="Android ADB 로그 분석기"
                   FontSize="24" FontWeight="Bold" Margin="0,0,0,20"/>
        
        <!-- 버튼 -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <Button Content="분석 시작" Command="{Binding AnalyzeCommand}"
                    Width="120" Height="35" Margin="0,0,10,0"/>
            <Button Content="HTML 보고서 생성" Command="{Binding GenerateReportCommand}"
                    Width="150" Height="35"/>
        </StackPanel>
        
        <!-- 진행률 -->
        <ProgressBar Grid.Row="2" Height="20" Margin="0,0,0,10"
                     Value="{Binding ProgressPercentage}" Maximum="100"/>
        
        <!-- 상태 메시지 -->
        <TextBlock Grid.Row="3" Text="{Binding StatusMessage}"
                   TextWrapping="Wrap" VerticalAlignment="Top"/>
        
        <!-- 푸터 -->
        <TextBlock Grid.Row="4" Text="AndroidAdbAnalyze v1.0"
                   HorizontalAlignment="Right" Foreground="Gray"/>
    </Grid>
</Window>
```

---

## 5. 고급 시나리오

### 5.1 배치 처리
```csharp
public async Task<Dictionary<string, AnalysisResult>> AnalyzeBatchAsync(
    string[] logDirectories,
    IProgress<(int completed, int total)> progress = null)
{
    var results = new Dictionary<string, AnalysisResult>();
    var completed = 0;
    var total = logDirectories.Length;
    
    foreach (var logDir in logDirectories)
    {
        var parser = new AdbLogParser("Configs");
        var parsingResult = await parser.ParseAllLogsAsync(logDir);
        
        var analysisResult = await _orchestrator.AnalyzeAsync(
            parsingResult.Events);
        
        results[logDir] = analysisResult;
        completed++;
        progress?.Report((completed, total));
    }
    
    return results;
}
```

### 5.2 실시간 모니터링
```csharp
public async Task MonitorLogsAsync(
    string logDirectory,
    Action<AnalysisResult> onAnalysisComplete,
    CancellationToken cancellationToken)
{
    var watcher = new FileSystemWatcher(logDirectory)
    {
        Filter = "*.log",
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
    };
    
    watcher.Changed += async (sender, e) =>
    {
        await Task.Delay(1000); // Debounce
        
        var parser = new AdbLogParser("Configs");
        var parsingResult = await parser.ParseAllLogsAsync(logDirectory);
        
        var analysisResult = await _orchestrator.AnalyzeAsync(
            parsingResult.Events,
            cancellationToken: cancellationToken);
        
        onAnalysisComplete(analysisResult);
    };
    
    watcher.EnableRaisingEvents = true;
    
    while (!cancellationToken.IsCancellationRequested)
    {
        await Task.Delay(100, cancellationToken);
    }
}
```

### 5.3 커스텀 필터링
```csharp
public class CustomAnalysisService
{
    private readonly IAnalysisOrchestrator _orchestrator;
    
    public async Task<AnalysisResult> AnalyzeWithCustomFiltersAsync(
        IReadOnlyList<NormalizedLogEvent> events)
    {
        // 1. 특정 시간 범위 필터링
        var startTime = DateTime.Parse("2025-10-05 22:00:00");
        var endTime = DateTime.Parse("2025-10-05 23:00:00");
        
        var filteredEvents = events
            .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
            .ToList();
        
        // 2. 특정 패키지만 분석
        var options = new AnalysisOptions
        {
            PackageWhitelist = new[]
            {
                "com.sec.android.app.camera",
                "com.kakao.talk",
                "org.telegram.messenger"
            }
        };
        
        // 3. 분석 실행
        return await _orchestrator.AnalyzeAsync(filteredEvents, options);
    }
}
```

---

## 6. 에러 처리 모범 사례

### 6.1 예외 처리
```csharp
public async Task<AnalysisResult?> SafeAnalyzeAsync(
    IReadOnlyList<NormalizedLogEvent> events)
{
    try
    {
        return await _orchestrator.AnalyzeAsync(events);
    }
    catch (ArgumentNullException ex)
    {
        _logger.LogError(ex, "이벤트 목록이 null입니다.");
        return null;
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("분석이 사용자에 의해 취소되었습니다.");
        return null;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "분석 중 예상치 못한 오류가 발생했습니다.");
        return null;
    }
}
```

### 6.2 결과 검증
```csharp
public bool ValidateAnalysisResult(AnalysisResult result)
{
    if (result == null)
        return false;
    
    if (!result.Success)
    {
        foreach (var error in result.Errors)
        {
            _logger.LogError("분석 오류: {Error}", error);
        }
        return false;
    }
    
    if (result.Warnings.Any())
    {
        foreach (var warning in result.Warnings)
        {
            _logger.LogWarning("분석 경고: {Warning}", warning);
        }
    }
    
    if (result.Sessions.Count == 0)
    {
        _logger.LogWarning("세션이 감지되지 않았습니다.");
    }
    
    return true;
}
```

---

## 7. 성능 최적화

### 7.1 대용량 로그 처리
```csharp
public async Task<AnalysisResult> AnalyzeLargeLogsAsync(
    IReadOnlyList<NormalizedLogEvent> events)
{
    // 1. 이벤트 수 확인
    _logger.LogInformation("처리할 이벤트 수: {Count}개", events.Count);
    
    // 2. 메모리 사용량 체크
    var beforeMemory = GC.GetTotalMemory(false);
    
    // 3. 분석 실행
    var stopwatch = Stopwatch.StartNew();
    var result = await _orchestrator.AnalyzeAsync(events);
    stopwatch.Stop();
    
    // 4. 성능 측정
    var afterMemory = GC.GetTotalMemory(false);
    var memoryUsed = (afterMemory - beforeMemory) / 1024.0 / 1024.0; // MB
    
    _logger.LogInformation("처리 시간: {Elapsed}초, 메모리: {Memory}MB",
        stopwatch.Elapsed.TotalSeconds, memoryUsed);
    
    return result;
}
```

### 7.2 캐싱
```csharp
public class CachedAnalysisService
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly Dictionary<string, AnalysisResult> _cache = new();
    
    public async Task<AnalysisResult> GetOrAnalyzeAsync(
        string cacheKey,
        IReadOnlyList<NormalizedLogEvent> events)
    {
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            _logger.LogInformation("캐시에서 결과 반환: {Key}", cacheKey);
            return cachedResult;
        }
        
        var result = await _orchestrator.AnalyzeAsync(events);
        _cache[cacheKey] = result;
        
        return result;
    }
    
    public void ClearCache()
    {
        _cache.Clear();
    }
}
```

---

## 8. 문제 해결 (Troubleshooting)

### 8.1 흔한 문제

#### **문제 1: 세션이 감지되지 않음**
```
원인: 로그 파일에 CAMERA_CONNECT/DISCONNECT 또는 ACTIVITY 이벤트 없음
해결: 
1. 로그 파일 경로 확인
2. 파싱 설정 파일 (adb_*_config.yaml) 확인
3. 로그 수집 시간 범위 확인
```

#### **문제 2: 촬영이 감지되지 않음**
```
원인: 주 증거 이벤트 (DATABASE_INSERT, MEDIA_EXTRACTOR 등) 부재
해결:
1. MinConfidenceThreshold를 낮춰서 재시도 (0.1 ~ 0.3)
2. 로그 파일에 촬영 관련 이벤트가 있는지 확인
3. Strategy 로직 검토 (BasePatternStrategy, KakaoTalkStrategy 등)
```

#### **문제 3: 메모리 부족**
```
원인: 대용량 로그 파일 (> 50MB)
해결:
1. 로그 파일을 시간 범위로 분할
2. Parser의 LogParsingOptions.StartTime/EndTime 사용
3. 배치 크기 조정
```

### 8.2 로깅 활성화
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Debug); // 상세 로깅
    
    // 특정 카테고리만 상세 로깅
    builder.AddFilter("AndroidAdbAnalyze.Analysis", LogLevel.Debug);
    builder.AddFilter("Microsoft", LogLevel.Warning);
});
```

---

## 9. 자주 묻는 질문 (FAQ)

**Q1: Parser DLL과 Analysis DLL의 차이는?**
```
- Parser DLL: 로그 파일을 읽고 NormalizedLogEvent로 변환
- Analysis DLL: NormalizedLogEvent를 분석하여 세션 및 촬영 감지
```

**Q2: AnalysisOptions를 null로 전달하면?**
```
기본값이 사용됩니다:
- MinConfidenceThreshold = 0.3
- MaxSessionGap = 5분
- EventCorrelationWindow = 30초
```

**Q3: 비동기 메서드를 동기로 실행할 수 있나요?**
```csharp
// ⚠️ 비권장 (데드락 가능)
var result = _orchestrator.AnalyzeAsync(events).Result;

// ✅ 권장: 비동기 컨텍스트 유지
var result = await _orchestrator.AnalyzeAsync(events);
```

**Q4: 여러 Strategy를 동시에 사용할 수 있나요?**
```
예, 자동으로 PackageNamePattern과 Priority에 따라 선택됩니다.
예: KakaoTalkStrategy (Priority 200) > BasePatternStrategy (Priority 100)
```

**Q5: HTML 보고서를 커스터마이징할 수 있나요?**
```
현재 버전: HtmlReportGenerator는 고정 템플릿
향후 버전: IReportGenerator 구현하여 커스텀 가능
```

---

## 10. 추가 리소스

### 10.1 관련 문서
- **Architecture_Overview.md**: 전체 아키텍처 및 설계 구조
- **CoreAnalysis_DevelopmentPlan.md**: 개발 계획 및 Phase별 진행 상황
- **Phase8_Integration_Testing_Report.md**: 통합 테스트 및 Ground Truth 검증
- **Technical_Debt_Report.md**: 기술적 부채 및 TODO 항목

### 10.2 지원
- 📧 이메일: dev@example.com
- 📚 문서: [GitHub Wiki]
- 🐛 이슈: [GitHub Issues]

---

**문서 버전**: 1.0  
**최종 업데이트**: 2025-10-09  
**작성자**: AI Development Team  
**상태**: ✅ API 문서화 완료

