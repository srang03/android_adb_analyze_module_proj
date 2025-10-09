# Android ADB Analyze Module - API 사용 가이드

## 목차
1. [개요](#개요)
2. [빠른 시작](#빠른-시작)
3. [기본 사용법](#기본-사용법)
4. [설정 파일 작성](#설정-파일-작성)
5. [고급 사용법](#고급-사용법)
6. [API 레퍼런스](#api-레퍼런스)
7. [지원 로그 타입](#지원-로그-타입)
8. [에러 처리](#에러-처리)
9. [예제 코드](#예제-코드)
10. [성능 고려사항](#성능-고려사항)
11. [FAQ (자주 묻는 질문)](#faq-자주-묻는-질문)

---

## 개요

`AndroidAdbAnalyzeModule`은 Android ADB dumpsys 로그를 파싱하고 전처리하여 `NormalizedLogEvent` 형태로 변환하는 C# .NET 8 라이브러리입니다.

### 주요 기능
- ✅ YAML 기반 외부 설정 파일로 파싱 규칙 정의
- ✅ 7가지 로그 타입 지원 (audio, vibrator, usagestats, camera_worker, activity, media.camera, media.metrics)
- ✅ 섹션 기반 파싱 (Section Splitting)
- ✅ Regex 패턴 기반 필드 추출
- ✅ 타임스탬프 정규화 및 UTC 변환 (8가지 포맷 지원)
- ✅ 멀티 버전 안드로이드 지원
- ✅ 에러 처리 및 통계 제공
- ✅ 스레드 안전한 InMemory Repository

### DLL 책임 범위
이 DLL은 **파싱 및 전처리**만 담당합니다:
- 로그 파일 파싱 (Section Splitting, Regex Matching)
- 데이터 전처리 (타임스탬프 정규화, 필드 변환)
- 정규화된 이벤트 저장 (InMemory/DB Repository)

**상위 애플리케이션 책임**:
- 상관관계 분석 (여러 이벤트 간 관계 분석)
- 이벤트 감지 (카메라 촬영, 앱 실행 등)
- 타임라인 생성, 클러스터링, UI 표시

---

## 빠른 시작

### 1. NuGet 패키지 참조

```xml
<ItemGroup>
  <ProjectReference Include="..\AndroidAdbAnalyzeModule\AndroidAdbAnalyzeModule.csproj" />
</ItemGroup>
```

### 2. 필수 NuGet 패키지 설치

```bash
dotnet add package Microsoft.Extensions.Logging.Abstractions
```

### 3. 기본 사용 예제

```csharp
using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;
using Microsoft.Extensions.Logging;

// 1. 설정 파일 로드
var configPath = "configs/adb_audio_config.yaml";
var configLoader = new YamlConfigurationLoader(configPath);
var configuration = await configLoader.LoadAsync(configPath);

// 2. 디바이스 정보 설정
var deviceInfo = new DeviceInfo
{
    TimeZone = "Asia/Seoul",
    CurrentTime = DateTime.Now,
    AndroidVersion = "15",
    Manufacturer = "Samsung",
    Model = "SM-S928N"
};

// 3. 파싱 옵션 설정
var options = new LogParsingOptions
{
    DeviceInfo = deviceInfo,
    ConvertToUtc = true,
    Encoding = "utf-8",
    MaxFileSizeMB = 10
};

// 4. 파서 생성 및 실행
var parser = new AdbLogParser(configuration);
var result = await parser.ParseAsync("logs/audio.txt", options);

// 5. 결과 처리
if (result.Success)
{
    Console.WriteLine($"파싱 성공! 이벤트 수: {result.Events.Count}");
    
    foreach (var evt in result.Events)
    {
        Console.WriteLine($"[{evt.EventType}] {evt.Timestamp} - {evt.SourceSection}");
        foreach (var attr in evt.Attributes)
        {
            Console.WriteLine($"  {attr.Key}: {attr.Value}");
        }
    }
}
else
{
    Console.WriteLine($"파싱 실패: {result.ErrorMessage}");
}
```

---

## 기본 사용법

### ⚠️ 중요: 로그 파일과 설정 파일 매핑

**이 라이브러리는 로그 파일과 설정 파일을 자동으로 매핑하지 않습니다.**

사용자가 직접 다음을 결정해야 합니다:
1. 어떤 로그 파일에 어떤 설정 파일을 사용할지
2. 로그 파일명이나 내용 기반 설정 선택 로직 (상위 앱 책임)

```csharp
// ❌ 자동 매핑 없음
var result = await parser.ParseAsync("unknown_log.txt", options);  // 어떤 설정을 사용?

// ✅ 명시적 매핑 필요
var config = SelectConfigByLogFile("audio.txt");  // 사용자 구현 필요
var parser = new AdbLogParser(config);
var result = await parser.ParseAsync("audio.txt", options);
```

**권장 패턴:**
```csharp
public LogConfiguration SelectConfigByLogFile(string logFilePath)
{
    var fileName = Path.GetFileName(logFilePath).ToLower();
    
    return fileName switch
    {
        "audio.txt" => LoadConfig("adb_audio_config.yaml"),
        "vibrator_manager.txt" => LoadConfig("adb_vibrator_config.yaml"),
        "usagestats.txt" => LoadConfig("adb_usagestats_config.yaml"),
        "activity.txt" => LoadConfig("adb_activity_config.yaml"),
        "media.camera.txt" => LoadConfig("adb_media_camera_config.yaml"),
        "media.camera.worker.txt" => LoadConfig("adb_media_camera_worker_config.yaml"),
        "media.metrics.txt" => LoadConfig("adb_media_metrics_config.yaml"),
        _ => throw new NotSupportedException($"No configuration for log file: {fileName}")
    };
}

private LogConfiguration LoadConfig(string configName)
{
    var configPath = Path.Combine("configs", configName);
    var loader = new YamlConfigurationLoader(configPath);
    return loader.Load(configPath);
}
```

### 1단계: 설정 파일 로드

```csharp
using AndroidAdbAnalyzeModule.Configuration.Loaders;
using Microsoft.Extensions.Logging;

// 로거 설정 (선택사항)
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<YamlConfigurationLoader>();

// 설정 로더 생성
var configPath = "configs/adb_audio_config.yaml";
var configLoader = new YamlConfigurationLoader(configPath, logger);

// 비동기 로드
var configuration = await configLoader.LoadAsync(configPath);

// 또는 동기 로드
// var configuration = configLoader.Load(configPath);
```

### 2단계: 디바이스 정보 설정

```csharp
using AndroidAdbAnalyzeModule.Core.Models;

var deviceInfo = new DeviceInfo
{
    // 필수: 디바이스 타임존 (타임스탬프 정규화에 사용)
    TimeZone = "Asia/Seoul",
    
    // 필수: 현재 시간 (연도 정보가 없는 로그의 연도 추론에 사용)
    CurrentTime = DateTime.Now,
    
    // 필수: 안드로이드 버전 (설정 파일의 supportedVersions와 비교)
    AndroidVersion = "15",
    
    // 선택: 제조사 및 모델
    Manufacturer = "Samsung",
    Model = "SM-S928N"
};
```

### 3단계: 파싱 옵션 설정

```csharp
var options = new LogParsingOptions
{
    // 필수: 디바이스 정보
    DeviceInfo = deviceInfo,
    
    // 선택: UTC 변환 여부 (기본값: true)
    ConvertToUtc = true,
    
    // 선택: 파일 인코딩 (기본값: "utf-8")
    Encoding = "utf-8",
    
    // 선택: 최대 파일 크기 (MB) (기본값: 500)
    MaxFileSizeMB = 10
};
```

### 4단계: 파서 생성 및 실행

```csharp
using AndroidAdbAnalyzeModule.Parsing;

// 로거 설정 (선택사항)
var parserLogger = loggerFactory.CreateLogger<AdbLogParser>();

// 파서 생성
var parser = new AdbLogParser(configuration, parserLogger);

// 비동기 파싱
var result = await parser.ParseAsync("logs/audio.txt", options);

// CancellationToken 지원
// var cts = new CancellationTokenSource();
// var result = await parser.ParseAsync("logs/audio.txt", options, cts.Token);
```

### 5단계: 결과 처리

```csharp
if (result.Success)
{
    // 파싱 성공
    Console.WriteLine($"✅ 파싱 성공");
    Console.WriteLine($"총 이벤트: {result.Events.Count}");
    Console.WriteLine($"처리 시간: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");
    Console.WriteLine($"성공률: {result.Statistics.SuccessRate:P2}");
    
    // 이벤트별 통계
    foreach (var kvp in result.Statistics.EventTypeCounts)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}개");
    }
    
    // 이벤트 처리
    foreach (var evt in result.Events)
    {
        // 이벤트 타입별 처리
        switch (evt.EventType)
        {
            case "PLAYER_CREATED":
                ProcessPlayerCreated(evt);
                break;
            case "PLAYER_STARTED":
                ProcessPlayerStarted(evt);
                break;
            // ... 기타 이벤트 타입
        }
    }
}
else
{
    // 파싱 실패
    Console.WriteLine($"❌ 파싱 실패: {result.ErrorMessage}");
    
    if (result.Exception != null)
    {
        Console.WriteLine($"예외: {result.Exception.Message}");
    }
    
    // 부분 파싱 성공 (일부 라인만 에러)
    if (result.Events.Count > 0)
    {
        Console.WriteLine($"⚠️ 부분 성공: {result.Events.Count}개 이벤트 파싱됨");
        Console.WriteLine($"에러 라인: {result.Errors.Count}개");
    }
}
```

---

## 설정 파일 작성

설정 파일은 YAML 형식으로 작성하며, 로그 파싱 규칙을 정의합니다.

### 핵심 개념

#### 1. LogType (로그 타입)
- **정의 위치**: `metadata.logType` (YAML 파일)
- **용도**: 로그의 종류를 식별 (예: `adb_audio`, `adb_vibrator`)
- **하드코딩 여부**: ❌ 없음 - YAML 파일에서 자유롭게 정의
- **예시**:
  ```yaml
  metadata:
    logType: "adb_audio"  # 사용자 정의 가능
  ```

#### 2. EventType (이벤트 타입)
- **정의 위치**: `linePatterns[].eventType` (YAML 파일)
- **용도**: 파싱된 로그 라인의 이벤트 유형 (예: `PLAYER_CREATED`, `CAMERA_OPENED`)
- **하드코딩 여부**: ❌ 없음 - YAML 파일에서 자유롭게 정의
- **예시**:
  ```yaml
  linePatterns:
    - id: "new_player_pattern"
      eventType: "PLAYER_CREATED"  # 사용자 정의 가능
      pattern: "new player piid:(\\d+)"
  ```
- **결과**: `NormalizedLogEvent.EventType`으로 반환됨

#### 3. FilePatterns (파일 패턴)
- **정의 위치**: `filePatterns` (YAML 파일)
- **용도**: 문서화 및 참고용 (현재 자동 매핑에 사용되지 않음)
- **예시**:
  ```yaml
  filePatterns:
    - "audio.txt"
    - "media.audio_flinger.txt"
  ```

### 기본 구조

```yaml
# 설정 파일 스키마 버전 (필수)
configSchemaVersion: "1.0"

# 로그 타입 (필수)
logType: "adb_audio"

# 메타데이터 (필수)
metadata:
  displayName: "ADB Audio Log Parser"
  description: "Parses dumpsys media.audio_flinger logs"
  author: "Your Name"
  supportedVersions: ["15"]  # 지원하는 안드로이드 버전 (또는 ["*"] for all)

# 파일 패턴 (필수)
filePatterns:
  - "audio.txt"
  - "media.audio_flinger.txt"

# 글로벌 설정 (필수)
globalSettings:
  timestampFormat: "MM-dd HH:mm:ss':'fff"
  timestampField: "timestamp"
  sortOrder: "ascending"  # ascending | descending | none
  timeZone: "local"

# 섹션 정의 (필수)
sections:
  - id: "players_section"
    name: "Players Section"
    startMarker: "Players:"
    markerType: "text"  # text | regex
    endMarker: "^Hardware"
    endMarkerType: "regex"

# 파서 정의 (필수)
parsers:
  - id: "audio_parser"
    name: "Audio Parser"
    enabled: true
    targetSections: ["players_section"]
    linePatterns:
      - id: "new_player_pattern"
        eventType: "PLAYER_CREATED"
        pattern: "new player piid:(\\d+) uid:(\\d+)"
        fields:
          piid:
            group: 1
            type: "int"
          uid:
            group: 2
            type: "int"
```

### 지원하는 필드 타입

- `string`: 문자열 (기본값)
- `int`: 32비트 정수
- `long`: 64비트 정수
- `double`: 부동소수점
- `bool`: 불린 (`true`, `false`, `1`, `0`, `yes`, `no`)
- `hex`: 16진수 → 10진수 변환
- `datetime`: 날짜/시간 파싱

### 멀티 버전 지원

```yaml
metadata:
  supportedVersions: ["11", "12", "14", "15"]  # 특정 버전들
  # 또는
  supportedVersions: ["*"]  # 모든 버전
```

### 설정 파일 버전 관리

#### ConfigSchemaVersion (설정 파일 스키마 버전)

**현재 지원 버전**: `"1.0"` 만 지원

```yaml
configSchemaVersion: "1.0"  # 필수 필드
```

**버전 검증:**
- ✅ 로드 시 자동 검증 (`ConfigurationValidator`)
- ❌ 지원되지 않는 버전: `ConfigurationValidationException` 발생
- ❌ 누락 시: `ConfigurationValidationException` 발생

**버전별 컨버터:**
- ❌ **현재 미구현** - 구버전 설정을 신버전으로 자동 변환하는 기능 없음
- ❌ **Migration Service 없음** - Phase 7 이후로 연기됨
- ⚠️ **해결 방법**: 수동으로 설정 파일을 최신 스키마에 맞게 업데이트

**예제:**
```csharp
try
{
    var config = await configLoader.LoadAsync("old_config_v0.9.yaml");
}
catch (ConfigurationValidationException ex)
{
    // "ConfigSchemaVersion '0.9' is not supported. Supported versions: 1.0"
    Console.WriteLine(ex.Message);
    
    // 해결: 수동으로 설정 파일을 v1.0으로 업데이트
}
```

**향후 계획:**
- Phase 7 이후: `ConfigurationMigrationService` 구현 예정
- 자동 버전 변환 지원

---

## 고급 사용법

### Repository 사용 (선택사항)

파싱된 이벤트를 메모리에 저장하고 쿼리할 수 있습니다.

```csharp
using AndroidAdbAnalyzeModule.Repositories;

// Repository 생성
var repository = new InMemoryLogEventRepository();

// 이벤트 저장
await repository.SaveEventsAsync(result.Events);

// 시간 범위로 조회
var startTime = DateTime.UtcNow.AddHours(-1);
var endTime = DateTime.UtcNow;
var events = await repository.GetEventsByTimeRangeAsync(startTime, endTime);

// 특정 이벤트 타입만 조회
var playerEvents = await repository.GetEventsByTimeRangeAsync(
    startTime, 
    endTime, 
    eventType: "PLAYER_CREATED"
);

// 관련 이벤트 조회 (시간 윈도우 기반)
var eventId = events.First().EventId;
var relatedEvents = await repository.GetRelatedEventsAsync(
    eventId, 
    timeWindow: TimeSpan.FromSeconds(5)
);

// 저장된 이벤트 수 조회
var count = await repository.GetCountAsync();
Console.WriteLine($"저장된 이벤트: {count}개");

// Repository 비우기
await repository.ClearAsync();
```

### 설정 파일 재로드

런타임에 설정 파일을 다시 로드할 수 있습니다.

```csharp
var configLoader = new YamlConfigurationLoader(configPath);

// 초기 로드
var configuration = await configLoader.LoadAsync(configPath);

// 설정 변경 이벤트 구독
configLoader.ConfigurationChanged += (sender, args) =>
{
    Console.WriteLine($"설정 변경됨: {args.NewConfiguration.Metadata.DisplayName}");
    Console.WriteLine($"변경 시간: {args.Timestamp}");
};

// 설정 재로드 (다른 파일)
var newConfigPath = "configs/adb_audio_config_v2.yaml";
await configLoader.ReloadAsync(newConfigPath);

// 현재 설정으로 재로드
await configLoader.ReloadAsync();
```

### 디바이스 호환성 검증

파서는 자동으로 디바이스 호환성을 검증합니다.

```csharp
var deviceInfo = new DeviceInfo
{
    AndroidVersion = "14"  // 설정 파일의 supportedVersions와 비교
};

try
{
    var result = await parser.ParseAsync(logPath, options);
}
catch (ConfigurationValidationException ex)
{
    // "Android version '14' is not supported by this configuration"
    Console.WriteLine($"호환성 오류: {ex.Message}");
}
```

### 로거 통합

`Microsoft.Extensions.Logging`을 사용하여 상세한 로그를 출력할 수 있습니다.

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Debug);
    
    // 특정 카테고리만 로그 레벨 조정
    builder.AddFilter("AndroidAdbAnalyzeModule.Parsing", LogLevel.Information);
    builder.AddFilter("AndroidAdbAnalyzeModule.Configuration", LogLevel.Warning);
});

var parserLogger = loggerFactory.CreateLogger<AdbLogParser>();
var configLogger = loggerFactory.CreateLogger<YamlConfigurationLoader>();

var configLoader = new YamlConfigurationLoader(configPath, configLogger);
var parser = new AdbLogParser(configuration, parserLogger);
```

### 에러 상세 분석

```csharp
var result = await parser.ParseAsync(logPath, options);

// 에러가 있는 경우
if (result.Errors.Count > 0)
{
    Console.WriteLine($"총 에러: {result.Errors.Count}개");
    
    // Severity별 그룹화
    var errorsBySeverity = result.Errors.GroupBy(e => e.Severity);
    foreach (var group in errorsBySeverity)
    {
        Console.WriteLine($"{group.Key}: {group.Count()}개");
    }
    
    // 상세 에러 정보
    foreach (var error in result.Errors.Take(10))
    {
        Console.WriteLine($"라인 {error.LineNumber} [{error.Severity}]:");
        Console.WriteLine($"  메시지: {error.ErrorMessage}");
        Console.WriteLine($"  원본: {error.RawLine}");
        
        if (error.Exception != null)
        {
            Console.WriteLine($"  예외: {error.Exception.Message}");
        }
    }
}

// 통계 분석
Console.WriteLine($"총 라인: {result.Statistics.TotalLines}");
Console.WriteLine($"파싱 성공: {result.Statistics.ParsedLines}");
Console.WriteLine($"스킵된 라인: {result.Statistics.SkippedLines}");
Console.WriteLine($"에러 라인: {result.Statistics.ErrorLines}");
Console.WriteLine($"성공률: {result.Statistics.SuccessRate:P2}");
Console.WriteLine($"처리 시간: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");

// 섹션별 통계
foreach (var kvp in result.Statistics.SectionLineCounts)
{
    Console.WriteLine($"섹션 '{kvp.Key}': {kvp.Value} 라인");
}
```

---

## API 레퍼런스

### 핵심 클래스

#### `AdbLogParser`

로그 파싱의 메인 클래스입니다.

```csharp
public sealed class AdbLogParser : ILogParser
{
    // 생성자
    public AdbLogParser(LogConfiguration configuration, ILogger<AdbLogParser>? logger = null);
    
    // 메서드
    public Task<ParsingResult> ParseAsync(
        string logFilePath, 
        LogParsingOptions options, 
        CancellationToken cancellationToken = default);
}
```

#### `YamlConfigurationLoader`

YAML 설정 파일을 로드합니다.

```csharp
public sealed class YamlConfigurationLoader : IConfigurationLoader<LogConfiguration>
{
    // 생성자
    public YamlConfigurationLoader(string configPath, ILogger<YamlConfigurationLoader>? logger = null);
    
    // 메서드
    public LogConfiguration Load(string configPath);
    public Task<LogConfiguration> LoadAsync(string configPath);
    public void Reload();
    public Task ReloadAsync();
    public Task ReloadAsync(string newConfigPath);
    
    // 이벤트
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}
```

#### `InMemoryLogEventRepository`

이벤트를 메모리에 저장하고 쿼리합니다.

```csharp
public sealed class InMemoryLogEventRepository : ILogEventRepository, IDisposable
{
    // 메서드
    public Task<bool> SaveEventAsync(NormalizedLogEvent logEvent);
    public Task<int> SaveEventsAsync(IEnumerable<NormalizedLogEvent> events);
    public Task<IEnumerable<NormalizedLogEvent>> GetEventsByTimeRangeAsync(
        DateTime start, DateTime end, string? eventType = null);
    public Task<IEnumerable<NormalizedLogEvent>> GetRelatedEventsAsync(
        Guid eventId, TimeSpan timeWindow);
    public Task ClearAsync();
    public Task<int> GetCountAsync();
    public void Dispose();
}
```

### 주요 모델

#### `DeviceInfo`

디바이스 정보를 담는 모델입니다.

```csharp
public sealed class DeviceInfo
{
    public string TimeZone { get; init; } = "Asia/Seoul";
    public DateTime CurrentTime { get; init; } = DateTime.Now;
    public string? AndroidVersion { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
}
```

#### `LogParsingOptions`

파싱 옵션을 담는 모델입니다.

```csharp
public sealed class LogParsingOptions
{
    public DeviceInfo DeviceInfo { get; init; } = new();
    public bool ConvertToUtc { get; init; } = true;
    public string Encoding { get; init; } = "utf-8";
    public int MaxFileSizeMB { get; init; } = 500;
}
```

#### `ParsingResult`

파싱 결과를 담는 모델입니다.

```csharp
public sealed class ParsingResult
{
    public bool Success { get; init; }
    public IReadOnlyList<NormalizedLogEvent> Events { get; init; }
    public ParsingStatistics Statistics { get; init; }
    public IReadOnlyList<ParsingError> Errors { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
}
```

#### `NormalizedLogEvent`

정규화된 로그 이벤트입니다.

```csharp
public sealed class NormalizedLogEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; }
    public string SourceSection { get; init; }
    public IReadOnlyDictionary<string, object> Attributes { get; init; }
    public string? RawLine { get; init; }
    public string? SourceFileName { get; init; }
    public DeviceInfo DeviceInfo { get; internal set; }
}
```

#### `ParsingStatistics`

파싱 통계 정보입니다.

```csharp
public sealed class ParsingStatistics
{
    public int TotalLines { get; init; }
    public int ParsedLines { get; init; }
    public int SkippedLines { get; init; }
    public int ErrorLines { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public IReadOnlyDictionary<string, int> EventTypeCounts { get; init; }
    public IReadOnlyDictionary<string, int> SectionLineCounts { get; init; }
    public double SuccessRate => TotalLines > 0 ? (double)ParsedLines / TotalLines : 0.0;
}
```

### 예외 클래스

#### `ConfigurationException`

설정 관련 예외의 기본 클래스입니다.

```csharp
public class ConfigurationException : Exception
```

**파생 클래스**:
- `ConfigurationNotFoundException`: 설정 파일을 찾을 수 없음
- `ConfigurationValidationException`: 설정 검증 실패
- `ConfigurationLoadException`: 설정 로드 실패

#### `ParsingException`

파싱 관련 예외의 기본 클래스입니다.

```csharp
public class ParsingException : Exception
```

**파생 클래스**:
- `LogFileTooLargeException`: 로그 파일이 최대 크기 초과
- `CriticalParsingException`: 치명적인 파싱 에러

---

## 지원 로그 타입

현재 7가지 로그 타입을 지원합니다:

| 로그 타입 | 파일명 | 설정 파일 | dumpsys 명령 |
|----------|--------|-----------|--------------|
| **Audio** | `audio.txt` | `adb_audio_config.yaml` | `dumpsys media.audio_flinger` |
| **Vibrator** | `vibrator_manager.txt` | `adb_vibrator_config.yaml` | `dumpsys vibrator_manager` |
| **UsageStats** | `usagestats.txt` | `adb_usagestats_config.yaml` | `dumpsys usagestats` |
| **Camera Worker** | `media.camera.worker.txt` | `adb_media_camera_worker_config.yaml` | Camera lifecycle logs |
| **Activity** | `activity.txt` | `adb_activity_config.yaml` | `dumpsys activity` |
| **Media Camera** | `media.camera.txt` | `adb_media_camera_config.yaml` | Camera connect/disconnect |
| **Media Metrics** | `media.metrics.txt` | `adb_media_metrics_config.yaml` | Media extractor/audio track |

### 타임스탬프 포맷 지원

8가지 타임스탬프 포맷을 지원합니다:

1. `MM-dd HH:mm:ss:fff` - Audio (예: `09-04 15:08:25:404`)
2. `MM-dd HH:mm:ss.fff` - Vibrator (예: `09-04 15:08:25.404`)
3. `yyyy-MM-dd HH:mm:ss.fff zzz` - Camera Worker (예: `2025-09-04 15:08:25.432 +0900`)
4. `yyyy-MM-dd HH:mm:ss` - UsageStats, Activity URI (예: `2025-09-06 19:54:46`)
5. `yyyy. M. d. (오전|오후) h:mm:ss` - Activity STARTER (예: `2025. 9. 9. 오후 3:08:30`)
6. `yyyy-MM-dd HH:mm:ss.fff` - Generic with milliseconds
7. `MM-dd HH:mm:ss` - Without milliseconds
8. ISO 8601 formats

---

## 에러 처리

### 일반적인 에러 시나리오

#### 1. 설정 파일 없음

```csharp
try
{
    var configLoader = new YamlConfigurationLoader("nonexistent.yaml");
    var config = await configLoader.LoadAsync("nonexistent.yaml");
}
catch (ConfigurationNotFoundException ex)
{
    Console.WriteLine($"설정 파일을 찾을 수 없음: {ex.Message}");
}
```

#### 2. 설정 파일 검증 실패

```csharp
try
{
    var configLoader = new YamlConfigurationLoader("invalid_config.yaml");
    var config = await configLoader.LoadAsync("invalid_config.yaml");
}
catch (ConfigurationValidationException ex)
{
    Console.WriteLine($"설정 검증 실패: {ex.Message}");
    // 예: "Required field 'sections' is missing"
    // 예: "ConfigSchemaVersion '2.0' is not supported"
    // 예: "Invalid regex pattern in linePattern 'invalid_pattern'"
}
```

#### 3. 호환되지 않는 안드로이드 버전

```csharp
var deviceInfo = new DeviceInfo { AndroidVersion = "10" };
var options = new LogParsingOptions { DeviceInfo = deviceInfo };

try
{
    var result = await parser.ParseAsync(logPath, options);
}
catch (ConfigurationValidationException ex)
{
    Console.WriteLine($"호환성 오류: {ex.Message}");
    // 예: "Android version '10' is not supported by this configuration"
}
```

#### 4. 로그 파일이 너무 큼

```csharp
var options = new LogParsingOptions { MaxFileSizeMB = 10 };

try
{
    var result = await parser.ParseAsync("large_log.txt", options);
}
catch (LogFileTooLargeException ex)
{
    Console.WriteLine($"파일 크기 초과: {ex.FilePath}");
    Console.WriteLine($"파일 크기: {ex.FileSizeBytes / 1024.0 / 1024.0:F2} MB");
    Console.WriteLine($"최대 크기: {ex.MaxSizeBytes / 1024.0 / 1024.0:F2} MB");
}
```

#### 5. 부분 파싱 실패 처리

```csharp
var result = await parser.ParseAsync(logPath, options);

if (!result.Success)
{
    // 완전 실패
    Console.WriteLine($"파싱 실패: {result.ErrorMessage}");
}
else if (result.Errors.Count > 0)
{
    // 부분 성공 (일부 라인만 에러)
    Console.WriteLine($"⚠️ 부분 성공:");
    Console.WriteLine($"  성공: {result.Events.Count}개 이벤트");
    Console.WriteLine($"  실패: {result.Errors.Count}개 라인");
    Console.WriteLine($"  성공률: {result.Statistics.SuccessRate:P2}");
    
    // 에러 라인 처리
    foreach (var error in result.Errors.Where(e => e.Severity == "Critical"))
    {
        Console.WriteLine($"  치명적 에러 (라인 {error.LineNumber}): {error.ErrorMessage}");
    }
}
```

### 권장 에러 처리 패턴

```csharp
public async Task<List<NormalizedLogEvent>> ParseLogSafely(string logPath)
{
    try
    {
        var result = await parser.ParseAsync(logPath, options);
        
        if (result.Success)
        {
            // 에러 로깅 (Warning 수준)
            if (result.Errors.Count > 0)
            {
                _logger.LogWarning(
                    "부분 파싱 성공: {EventCount}개 이벤트, {ErrorCount}개 에러",
                    result.Events.Count, result.Errors.Count);
            }
            
            return result.Events.ToList();
        }
        else
        {
            _logger.LogError("파싱 실패: {ErrorMessage}", result.ErrorMessage);
            return new List<NormalizedLogEvent>();
        }
    }
    catch (ConfigurationException ex)
    {
        _logger.LogError(ex, "설정 오류: {Message}", ex.Message);
        return new List<NormalizedLogEvent>();
    }
    catch (LogFileTooLargeException ex)
    {
        _logger.LogError(ex, "파일 크기 초과: {FilePath} ({SizeMB} MB)", 
            ex.FilePath, ex.FileSizeBytes / 1024.0 / 1024.0);
        return new List<NormalizedLogEvent>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "예상치 못한 오류");
        return new List<NormalizedLogEvent>();
    }
}
```

---

## 예제 코드

### 예제 1: 기본 파싱

```csharp
using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;

public class BasicParsingExample
{
    public static async Task Main()
    {
        // 1. 설정 로드
        var configLoader = new YamlConfigurationLoader("configs/adb_audio_config.yaml");
        var config = await configLoader.LoadAsync("configs/adb_audio_config.yaml");
        
        // 2. 디바이스 정보 및 옵션 설정
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "15"
        };
        
        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        
        // 3. 파싱
        var parser = new AdbLogParser(config);
        var result = await parser.ParseAsync("logs/audio.txt", options);
        
        // 4. 결과 출력
        if (result.Success)
        {
            Console.WriteLine($"✅ {result.Events.Count}개 이벤트 파싱됨");
            
            foreach (var evt in result.Events.Take(5))
            {
                Console.WriteLine($"[{evt.EventType}] {evt.Timestamp:HH:mm:ss.fff}");
            }
        }
    }
}
```

### 예제 2: Repository 사용

```csharp
using AndroidAdbAnalyzeModule.Repositories;

public class RepositoryExample
{
    public static async Task Main()
    {
        // 파싱 (예제 1과 동일)
        var result = await parser.ParseAsync("logs/audio.txt", options);
        
        if (!result.Success) return;
        
        // Repository 생성 및 저장
        var repository = new InMemoryLogEventRepository();
        await repository.SaveEventsAsync(result.Events);
        
        // 시간 범위로 조회
        var now = DateTime.UtcNow;
        var events = await repository.GetEventsByTimeRangeAsync(
            now.AddHours(-1), 
            now,
            eventType: "PLAYER_CREATED"
        );
        
        Console.WriteLine($"최근 1시간 내 PLAYER_CREATED 이벤트: {events.Count()}개");
        
        // 관련 이벤트 조회
        if (events.Any())
        {
            var firstEvent = events.First();
            var relatedEvents = await repository.GetRelatedEventsAsync(
                firstEvent.EventId,
                TimeSpan.FromSeconds(5)
            );
            
            Console.WriteLine($"관련 이벤트 (±5초): {relatedEvents.Count()}개");
        }
    }
}
```

### 예제 3: 다중 로그 파일 파싱

```csharp
public class MultipleLogFilesExample
{
    public static async Task Main()
    {
        var logFiles = new[]
        {
            ("configs/adb_audio_config.yaml", "logs/audio.txt"),
            ("configs/adb_vibrator_config.yaml", "logs/vibrator_manager.txt"),
            ("configs/adb_usagestats_config.yaml", "logs/usagestats.txt")
        };
        
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "15"
        };
        
        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var repository = new InMemoryLogEventRepository();
        
        foreach (var (configPath, logPath) in logFiles)
        {
            try
            {
                // 설정 로드
                var configLoader = new YamlConfigurationLoader(configPath);
                var config = await configLoader.LoadAsync(configPath);
                
                // 파싱
                var parser = new AdbLogParser(config);
                var result = await parser.ParseAsync(logPath, options);
                
                if (result.Success)
                {
                    // Repository에 저장
                    await repository.SaveEventsAsync(result.Events);
                    Console.WriteLine($"✅ {Path.GetFileName(logPath)}: {result.Events.Count}개 이벤트");
                }
                else
                {
                    Console.WriteLine($"❌ {Path.GetFileName(logPath)}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {Path.GetFileName(logPath)}: {ex.Message}");
            }
        }
        
        var totalEvents = await repository.GetCountAsync();
        Console.WriteLine($"\n총 {totalEvents}개 이벤트 저장됨");
    }
}
```

### 예제 4: 상관관계 분석 (상위 앱 책임)

```csharp
public class CorrelationAnalysisExample
{
    // DLL은 파싱만 담당, 상관관계 분석은 상위 앱에서 수행
    public static async Task Main()
    {
        // 파싱
        var result = await parser.ParseAsync("logs/audio.txt", options);
        if (!result.Success) return;
        
        var repository = new InMemoryLogEventRepository();
        await repository.SaveEventsAsync(result.Events);
        
        // 상관관계 분석: "new player" 이후 "player started" 찾기
        var newPlayerEvents = result.Events
            .Where(e => e.EventType == "PLAYER_CREATED")
            .ToList();
        
        foreach (var newPlayer in newPlayerEvents)
        {
            var piid = newPlayer.Attributes["piid"];
            
            // 같은 piid를 가진 PLAYER_STARTED 이벤트 찾기 (5초 이내)
            var relatedEvents = await repository.GetRelatedEventsAsync(
                newPlayer.EventId,
                TimeSpan.FromSeconds(5)
            );
            
            var startedEvent = relatedEvents
                .FirstOrDefault(e => 
                    e.EventType == "PLAYER_STARTED" && 
                    e.Attributes.ContainsKey("piid") &&
                    e.Attributes["piid"].Equals(piid));
            
            if (startedEvent != null)
            {
                var package = newPlayer.Attributes.ContainsKey("package") 
                    ? newPlayer.Attributes["package"] 
                    : "unknown";
                
                Console.WriteLine($"카메라 앱 시작 감지:");
                Console.WriteLine($"  패키지: {package}");
                Console.WriteLine($"  시작 시간: {newPlayer.Timestamp:HH:mm:ss.fff}");
                Console.WriteLine($"  재생 시간: {startedEvent.Timestamp:HH:mm:ss.fff}");
            }
        }
    }
}
```

### 예제 5: 실시간 로그 모니터링

```csharp
public class RealtimeMonitoringExample
{
    private readonly AdbLogParser _parser;
    private readonly LogParsingOptions _options;
    private readonly InMemoryLogEventRepository _repository;
    
    public RealtimeMonitoringExample()
    {
        var configLoader = new YamlConfigurationLoader("configs/adb_audio_config.yaml");
        var config = configLoader.Load("configs/adb_audio_config.yaml");
        
        _parser = new AdbLogParser(config);
        _options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = DateTime.Now,
                AndroidVersion = "15"
            }
        };
        _repository = new InMemoryLogEventRepository();
    }
    
    public async Task MonitorLogDirectory(string directoryPath, CancellationToken ct)
    {
        var processedFiles = new HashSet<string>();
        
        while (!ct.IsCancellationRequested)
        {
            var logFiles = Directory.GetFiles(directoryPath, "*.txt");
            
            foreach (var logFile in logFiles)
            {
                if (processedFiles.Contains(logFile))
                    continue;
                
                try
                {
                    Console.WriteLine($"📄 파싱 중: {Path.GetFileName(logFile)}");
                    
                    var result = await _parser.ParseAsync(logFile, _options, ct);
                    
                    if (result.Success)
                    {
                        await _repository.SaveEventsAsync(result.Events);
                        Console.WriteLine($"✅ {result.Events.Count}개 이벤트 추가됨");
                        
                        processedFiles.Add(logFile);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 오류: {ex.Message}");
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
```

---

## 성능 고려사항

### 최적화 팁

1. **설정 파일 재사용**
   ```csharp
   // ❌ 나쁜 예: 매번 설정 로드
   foreach (var logFile in logFiles)
   {
       var config = await new YamlConfigurationLoader(configPath).LoadAsync(configPath);
       var parser = new AdbLogParser(config);
   }
   
   // ✅ 좋은 예: 설정 재사용
   var config = await new YamlConfigurationLoader(configPath).LoadAsync(configPath);
   var parser = new AdbLogParser(config);
   foreach (var logFile in logFiles)
   {
       var result = await parser.ParseAsync(logFile, options);
   }
   ```

2. **Repository 배치 저장**
   ```csharp
   // ❌ 나쁜 예: 개별 저장
   foreach (var evt in events)
   {
       await repository.SaveEventAsync(evt);
   }
   
   // ✅ 좋은 예: 배치 저장
   await repository.SaveEventsAsync(events);
   ```

3. **RegexLineParser 캐싱**
   - 내부적으로 자동 캐싱됨
   - 파서 인스턴스를 재사용하면 성능 향상

4. **파일 크기 제한**
   ```csharp
   var options = new LogParsingOptions
   {
       MaxFileSizeMB = 10  // 10MB로 제한
   };
   ```

### 성능 지표

- **처리 속도**: 약 1-2MB/s (일반적인 로그 파일)
- **메모리 사용**: 파일 크기의 약 2-3배
- **Regex 캐싱**: 파서 인스턴스당 패턴 미리 컴파일

---

## FAQ (자주 묻는 질문)

### Q1. 로그 파일을 전달하면 자동으로 설정 파일과 매핑되나요?

**A:** ❌ **아니요.** 자동 매핑 기능은 없습니다.

사용자가 직접 다음을 구현해야 합니다:
```csharp
// 로그 파일명 기반 설정 선택 로직 (사용자 구현)
var config = SelectConfigByLogFile("audio.txt");
var parser = new AdbLogParser(config);
var result = await parser.ParseAsync("audio.txt", options);
```

**이유:** 
- 로그 파일명이 다양하고 일관되지 않을 수 있음
- 사용자의 프로젝트 구조에 따라 매핑 규칙이 다를 수 있음
- DLL은 파싱만 담당, 파일 관리는 상위 앱 책임

### Q2. EventType은 하드코딩인가요?

**A:** ❌ **아니요.** YAML 설정 파일에서 자유롭게 정의합니다.

```yaml
linePatterns:
  - id: "custom_pattern"
    eventType: "MY_CUSTOM_EVENT"  # ← 원하는 이름 사용 가능
    pattern: "custom pattern (\\w+)"
```

결과:
```csharp
foreach (var evt in result.Events)
{
    Console.WriteLine(evt.EventType);  // "MY_CUSTOM_EVENT"
}
```

### Q3. 로그 타입(LogType)은 어디서 설정하나요?

**A:** YAML 설정 파일의 `metadata.logType`에서 정의합니다.

```yaml
metadata:
  logType: "adb_audio"  # ← 원하는 이름 사용 가능
  displayName: "ADB Audio Log Parser"
```

하드코딩이 아니므로 새로운 로그 타입을 자유롭게 추가할 수 있습니다.

### Q4. 설정 파일 버전이 맞지 않으면 어떻게 되나요?

**A:** `ConfigurationValidationException` 예외가 발생합니다.

```csharp
try
{
    var config = await configLoader.LoadAsync("old_config.yaml");
}
catch (ConfigurationValidationException ex)
{
    // "ConfigSchemaVersion '0.9' is not supported. Supported versions: 1.0"
}
```

**해결 방법:**
- ❌ 자동 변환 기능 없음 (Phase 7 이후 구현 예정)
- ✅ 수동으로 설정 파일을 최신 스키마(`1.0`)에 맞게 업데이트

### Q5. FilePatterns는 어디에 사용되나요?

**A:** 현재는 **문서화 및 참고용**으로만 사용됩니다.

```yaml
filePatterns:
  - "audio.txt"
  - "media.audio_flinger.txt"
```

자동 매핑에는 사용되지 않으며, 설정 파일이 어떤 로그 파일을 파싱하기 위한 것인지 문서화하는 용도입니다.

### Q6. 새로운 로그 타입을 추가하려면?

**A:** 3단계로 간단히 추가할 수 있습니다.

1. **YAML 설정 파일 작성** (`my_new_log_config.yaml`)
   ```yaml
   configSchemaVersion: "1.0"
   metadata:
     logType: "my_new_log"
     supportedVersions: ["*"]
   sections:
     - id: "main_section"
       startMarker: "START"
       endMarker: "END"
   parsers:
     - id: "main_parser"
       targetSections: ["main_section"]
       linePatterns:
         - id: "event_pattern"
           eventType: "MY_EVENT"
           pattern: "event: (\\w+)"
           fields:
             eventName:
               group: 1
               type: "string"
   ```

2. **설정 로드**
   ```csharp
   var config = await new YamlConfigurationLoader("my_new_log_config.yaml")
       .LoadAsync("my_new_log_config.yaml");
   ```

3. **파싱 실행**
   ```csharp
   var parser = new AdbLogParser(config);
   var result = await parser.ParseAsync("my_new_log.txt", options);
   ```

**코드 수정 불필요** - 설정 파일만으로 새로운 로그 타입 추가 가능!

### Q7. 여러 로그 타입을 동시에 처리하려면?

**A:** `Dictionary`로 설정을 관리하고 로그별로 파서를 생성합니다.

```csharp
var configs = new Dictionary<string, LogConfiguration>
{
    ["audio"] = LoadConfig("adb_audio_config.yaml"),
    ["vibrator"] = LoadConfig("adb_vibrator_config.yaml")
};

var repository = new InMemoryLogEventRepository();

foreach (var (logType, config) in configs)
{
    var parser = new AdbLogParser(config);
    var result = await parser.ParseAsync($"logs/{logType}.txt", options);
    
    if (result.Success)
    {
        await repository.SaveEventsAsync(result.Events);
    }
}
```

### Q8. 상관관계 분석은 어떻게 하나요?

**A:** DLL은 파싱만 담당하며, **상관관계 분석은 상위 앱의 책임**입니다.

```csharp
// DLL의 역할: 파싱 및 전처리
var result = await parser.ParseAsync("audio.txt", options);

// 상위 앱의 역할: 상관관계 분석
var playerCreated = result.Events.Where(e => e.EventType == "PLAYER_CREATED");
var playerStarted = result.Events.Where(e => e.EventType == "PLAYER_STARTED");

foreach (var created in playerCreated)
{
    var started = playerStarted.FirstOrDefault(s => 
        s.Attributes["piid"] == created.Attributes["piid"] &&
        s.Timestamp > created.Timestamp &&
        (s.Timestamp - created.Timestamp).TotalSeconds < 5);
    
    if (started != null)
    {
        Console.WriteLine("Camera capture detected!");
    }
}
```

---

## 문의 및 지원

- **문서 버전**: 1.1
- **최종 업데이트**: 2025-10-04 (수정: 로그 파일 매핑, FAQ 추가)
- **라이브러리 버전**: 1.0.0 (.NET 8)

추가 문의사항이나 버그 리포트는 프로젝트 관리자에게 문의하세요.

---

## 문서 업데이트 이력

### v1.1 (2025-10-04)
**주요 개선사항:**
- ✅ **로그 파일-설정 파일 매핑 명확화**: 자동 매핑이 없으며, 사용자가 직접 구현해야 함을 명시
- ✅ **핵심 개념 섹션 추가**: LogType, EventType, FilePatterns의 정의 위치 및 용도 설명
- ✅ **설정 파일 버전 관리 섹션 추가**: ConfigSchemaVersion 검증 및 Migration Service 현황
- ✅ **FAQ 섹션 추가**: 8가지 자주 묻는 질문과 상세 답변
  - Q1: 로그 파일-설정 파일 자동 매핑
  - Q2: EventType 하드코딩 여부
  - Q3: LogType 설정 위치
  - Q4: 설정 파일 버전 불일치 처리
  - Q5: FilePatterns 용도
  - Q6: 새로운 로그 타입 추가 방법
  - Q7: 여러 로그 타입 동시 처리
  - Q8: 상관관계 분석 구현 방법

### v1.0 (2025-10-04)
**초기 버전:**
- API 사용 가이드 작성
- 기본 사용법, 설정 파일 작성, 고급 사용법
- API 레퍼런스, 에러 처리, 예제 코드

