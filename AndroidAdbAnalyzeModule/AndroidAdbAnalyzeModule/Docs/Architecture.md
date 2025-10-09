# AndroidAdbAnalyzeModule 아키텍처

## 문서 개요

본 문서는 AndroidAdbAnalyzeModule의 전체 아키텍처 구조, 핵심 컴포넌트, 데이터 흐름, 그리고 설계 원칙을 설명합니다.

**버전**: 1.0  
**최종 업데이트**: 2025-10-04

---

## 목차

1. [전체 아키텍처](#전체-아키텍처)
2. [레이어별 구조](#레이어별-구조)
3. [핵심 컴포넌트](#핵심-컴포넌트)
4. [데이터 흐름](#데이터-흐름)
5. [클래스 다이어그램](#클래스-다이어그램)
6. [설계 원칙](#설계-원칙)
7. [확장성](#확장성)

---

## 전체 아키텍처

### 시스템 개요

```
┌─────────────────────────────────────────────────────────────┐
│                   External Applications                      │
│  (WPF UI, Console App, Web API, Other Analyzers)           │
└─────────────────────────────────────────────────────────────┘
                            ▲
                            │
                            │ ILogParser, ILogEventRepository
                            │
┌─────────────────────────────────────────────────────────────┐
│              AndroidAdbAnalyzeModule (DLL)                   │
│                                                              │
│  ┌────────────┐  ┌──────────────┐  ┌─────────────┐        │
│  │   Core     │  │Configuration │  │   Parsing   │        │
│  │  Models &  │  │   Loaders &  │  │   Engine    │        │
│  │Interfaces  │  │  Validators  │  │             │        │
│  └────────────┘  └──────────────┘  └─────────────┘        │
│         │               │                   │               │
│         └───────────────┼───────────────────┘               │
│                         │                                    │
│  ┌────────────┐  ┌──────────────┐  ┌─────────────┐        │
│  │Preprocessing│ │  Repositories│  │   Parsing   │        │
│  │(Timestamp) │  │  (InMemory)  │  │  Splitters  │        │
│  └────────────┘  └──────────────┘  └─────────────┘        │
└─────────────────────────────────────────────────────────────┘
                            │
                            │ YAML Configuration
                            ▼
                   ┌─────────────────┐
                   │ External Config │
                   │    Files        │
                   └─────────────────┘
```

### 3-Tier 아키텍처

```
┌─────────────────────────────────────────┐
│        Presentation Layer               │  ← 상위 앱 책임
│  (상관관계 분석, 이벤트 감지, UI)           │
└─────────────────────────────────────────┘
                    ▲
                    │ NormalizedLogEvent
                    │
┌─────────────────────────────────────────┐
│         Business Logic Layer            │  ← 이 DLL
│  (파싱, 전처리, 정규화)                    │
└─────────────────────────────────────────┘
                    ▲
                    │ Raw Log Files + YAML Config
                    │
┌─────────────────────────────────────────┐
│          Data Source Layer              │
│  (로그 파일, 설정 파일)                    │
└─────────────────────────────────────────┘
```

---

## 레이어별 구조

### Layer 1: Core (핵심 모델 & 인터페이스)

**위치**: `Core/Models/`, `Core/Interfaces/`, `Core/Exceptions/`

**책임:**
- 데이터 모델 정의 (불변 객체)
- 인터페이스 정의 (확장 포인트)
- 예외 정의 (에러 처리)

**주요 컴포넌트:**
```
Core/
├── Models/
│   ├── DeviceInfo.cs                # 디바이스 메타데이터
│   ├── LogParsingOptions.cs         # 파싱 옵션
│   ├── NormalizedLogEvent.cs        # 정규화된 이벤트 (핵심!)
│   ├── ParsedLogEntry.cs            # 중간 파싱 결과
│   ├── ParsingResult.cs             # 파싱 최종 결과
│   ├── ParsingStatistics.cs         # 파싱 통계
│   └── ParsingError.cs              # 파싱 에러
│
├── Interfaces/
│   ├── ILogParser.cs                # 파서 인터페이스
│   ├── ILogEventRepository.cs       # 저장소 인터페이스
│   ├── IConfigurationLoader.cs      # 설정 로더 인터페이스
│   ├── ISectionSplitter.cs          # 섹션 분할 인터페이스
│   └── ILineParser.cs               # 라인 파싱 인터페이스
│
└── Exceptions/
    ├── ConfigurationException.cs    # 설정 예외 (기본)
    ├── ConfigurationNotFoundException.cs
    ├── ConfigurationValidationException.cs
    ├── ConfigurationLoadException.cs
    ├── ParsingException.cs          # 파싱 예외 (기본)
    ├── LogFileTooLargeException.cs
    └── CriticalParsingException.cs
```

---

### Layer 2: Configuration (설정 관리)

**위치**: `Configuration/Loaders/`, `Configuration/Models/`, `Configuration/Validators/`

**책임:**
- YAML 설정 파일 로드
- 설정 검증 (스키마 버전, 디바이스 호환성)
- 설정 변경 알림 (이벤트)

**주요 컴포넌트:**
```
Configuration/
├── Loaders/
│   └── YamlConfigurationLoader.cs   # YAML 로더
│       - Load(configPath)
│       - LoadAsync(configPath)
│       - Reload()
│       - event ConfigurationChanged
│
├── Models/
│   └── LogConfiguration.cs          # 설정 모델 (YAML → C#)
│       - ConfigSchemaVersion
│       - ConfigMetadata
│       - FilePatterns
│       - GlobalSettings
│       - PerformanceSettings
│       - ErrorHandlingSettings
│       - Sections[] (SectionConfig)
│       - Parsers[] (ParserConfig)
│           - LinePatterns[] (LinePatternConfig)
│               - Fields{} (FieldDefinition)
│
└── Validators/
    └── ConfigurationValidator.cs    # 설정 검증
        - Validate(config)
        - ValidateDeviceCompatibility(deviceInfo, config)
        - ValidateSchemaVersion()
        - ValidateMetadata()
        - ValidateSections()
        - ValidateParsers()
```

---

### Layer 3: Parsing (파싱 엔진)

**위치**: `Parsing/`, `Parsing/LineParsers/`, `Parsing/SectionSplitters/`

**책임:**
- 로그 파일 → 섹션 분할
- 섹션 → 라인별 파싱 (Regex)
- 중간 결과 → 정규화된 이벤트

**주요 컴포넌트:**
```
Parsing/
├── AdbLogParser.cs                  # 메인 파서 (Orchestrator)
│   - ParseAsync(logFilePath, options)
│   - InitializeParsers()            # RegexLineParser 캐싱
│   - ParseSection(section)
│   - CreateNormalizedEvent(entry)
│
├── SectionSplitters/
│   └── LogSectionSplitter.cs        # 섹션 분할기
│       - Split(logContent, sectionConfigs)
│       - MatchMarker(line, marker, markerType)
│
└── LineParsers/
    └── RegexLineParser.cs           # Regex 기반 라인 파서
        - CanParse(line, context)
        - Parse(line, context)
        - ExtractFields(match, fields)
        - ConvertFieldValue(value, type)
```

---

### Layer 4: Preprocessing (전처리)

**위치**: `Preprocessing/`

**책임:**
- 타임스탬프 정규화 (다양한 포맷 → DateTime)
- UTC 변환
- 연도 정보 보완 (MM-dd 포맷)

**주요 컴포넌트:**
```
Preprocessing/
└── TimestampNormalizer.cs           # 타임스탬프 정규화
    - Normalize(timestampString)
    - NormalizeLogEntry(entry)
    - TryParseWithFormat(string, format)
    - AddYearInformation(parsedTime)
    - ConvertToUtc(localTime)
    
    지원 포맷:
    1. MM-dd HH:mm:ss:fff
    2. MM-dd HH:mm:ss.fff
    3. yyyy-MM-dd HH:mm:ss.fff zzz
    4. yyyy-MM-dd HH:mm:ss
    5. yyyy-MM-dd HH:mm:ss.fff
    6. MM-dd HH:mm:ss
```

---

### Layer 5: Repositories (저장소)

**위치**: `Repositories/`

**책임:**
- 파싱된 이벤트 저장
- 이벤트 쿼리 (시간 범위, 타입)
- 스레드 안전성 (ReaderWriterLockSlim)

**주요 컴포넌트:**
```
Repositories/
└── InMemoryLogEventRepository.cs    # 메모리 저장소
    - SaveEventAsync(logEvent)
    - SaveEventsAsync(events)
    - GetEventsByTimeRangeAsync(start, end, eventType?)
    - GetRelatedEventsAsync(eventId, timeWindow)
    - ClearAsync()
    - GetCountAsync()
```

---

## 핵심 컴포넌트

### 1. ILogParser (파서 인터페이스)

**역할**: 로그 파일 파싱의 진입점

```
┌──────────────────────────────────────────────────────┐
│                    ILogParser                        │
├──────────────────────────────────────────────────────┤
│ + ParseAsync(logFilePath, options, ct)              │
│   → Task<ParsingResult>                             │
└──────────────────────────────────────────────────────┘
                        ▲
                        │ implements
                        │
┌──────────────────────────────────────────────────────┐
│                  AdbLogParser                        │
├──────────────────────────────────────────────────────┤
│ - LogConfiguration _configuration                    │
│ - ISectionSplitter _sectionSplitter                 │
│ - Dictionary<string, List<ILineParser>> _cachedParsers │
│                                                      │
│ + ParseAsync(logFilePath, options, ct)              │
│ - InitializeParsers()                               │
│ - ParseSection(section, entries, errors)            │
│ - NormalizeEvents(entries, options, errors)         │
└──────────────────────────────────────────────────────┘
```

**핵심 기능:**
1. 설정 파일 로드 및 검증
2. 로그 파일 → 섹션 분할
3. 섹션 → 라인별 파싱
4. ParsedLogEntry → NormalizedLogEvent 변환
5. 통계 및 에러 수집

---

### 2. NormalizedLogEvent (정규화된 이벤트)

**역할**: 파싱 결과의 표준 형식

```
┌──────────────────────────────────────────────────────┐
│            NormalizedLogEvent (불변)                 │
├──────────────────────────────────────────────────────┤
│ + Guid EventId { get; init; }                       │
│ + DateTime Timestamp { get; init; }                 │
│ + string EventType { get; init; }                   │
│ + string SourceSection { get; init; }               │
│ + IReadOnlyDictionary<string, object> Attributes { get; init; } │
│ + string? RawLine { get; init; }                    │
│ + string? SourceFileName { get; init; }             │
│ + DeviceInfo DeviceInfo { get; internal set; }     │
└──────────────────────────────────────────────────────┘
```

**특징:**
- ✅ **불변 객체** (`init` 프로퍼티)
- ✅ **다중 이벤트 타입** (한 로그 파일 → 여러 EventType)
- ✅ **동적 속성** (Attributes Dictionary)
- ✅ **추적 가능** (EventId, RawLine, SourceSection)

---

### 3. LogConfiguration (설정 모델)

**역할**: YAML 설정 파일의 C# 표현

```
┌──────────────────────────────────────────────────────┐
│              LogConfiguration                        │
├──────────────────────────────────────────────────────┤
│ + string ConfigSchemaVersion                        │
│ + ConfigMetadata Metadata                           │
│ + List<string> FilePatterns                         │
│ + GlobalSettings GlobalSettings                     │
│ + PerformanceSettings Performance                   │
│ + ErrorHandlingSettings ErrorHandling               │
│ + List<SectionConfig> Sections                      │
│ + List<ParserConfig> Parsers                        │
└──────────────────────────────────────────────────────┘
                │
                ├─────────────────┐
                │                 │
                ▼                 ▼
┌─────────────────────┐  ┌─────────────────────┐
│   SectionConfig     │  │   ParserConfig      │
├─────────────────────┤  ├─────────────────────┤
│ + string Id         │  │ + string Id         │
│ + string Name       │  │ + string Name       │
│ + string StartMarker│  │ + List<string>      │
│ + string EndMarker  │  │   TargetSections    │
│ + string MarkerType │  │ + List<LinePattern> │
└─────────────────────┘  │   LinePatterns      │
                         └─────────────────────┘
                                 │
                                 ▼
                         ┌─────────────────────┐
                         │ LinePatternConfig   │
                         ├─────────────────────┤
                         │ + string Id         │
                         │ + string EventType  │
                         │ + string Regex      │
                         │ + Dictionary<string,│
                         │   FieldDefinition>  │
                         │   Fields            │
                         └─────────────────────┘
```

---

## 데이터 흐름

### 전체 파싱 파이프라인

```
1. 로그 파일 입력
        │
        ▼
┌──────────────────────┐
│  YamlConfigLoader    │  ← YAML 설정 로드
│  (Load config)       │
└──────────────────────┘
        │
        ▼
┌──────────────────────┐
│ ConfigurationValidator│ ← 설정 검증
│ (Validate schema)    │    - 스키마 버전
└──────────────────────┘    - 디바이스 호환성
        │
        ▼
┌──────────────────────┐
│  AdbLogParser        │
│  (ParseAsync)        │
└──────────────────────┘
        │
        ├───────────────────────────────┐
        │                               │
        ▼                               ▼
┌──────────────────────┐    ┌──────────────────────┐
│ LogSectionSplitter   │    │ TimestampNormalizer  │
│ (Split by markers)   │    │ (Normalize time)     │
└──────────────────────┘    └──────────────────────┘
        │                               │
        ▼                               │
┌──────────────────────┐               │
│ RegexLineParser      │               │
│ (Parse lines)        │               │
└──────────────────────┘               │
        │                               │
        ▼                               │
┌──────────────────────┐               │
│ ParsedLogEntry[]     │ ──────────────┘
│ (Intermediate)       │
└──────────────────────┘
        │
        ▼
┌──────────────────────┐
│ NormalizedLogEvent[] │  ← 최종 출력
│ (Final result)       │
└──────────────────────┘
        │
        ▼
┌──────────────────────┐
│ ParsingResult        │  ← 통계 및 에러 포함
│ (Success, Stats,     │
│  Errors)             │
└──────────────────────┘
```

### 상세 데이터 변환 과정

```
Raw Log Line:
"09-04 15:08:25:404 new player piid:123 uid:10001 package:com.sec.android.app.camera"

        │ 1. Section Splitting
        ▼
LogSection {
  Id: "playback_activity",
  Lines: ["09-04 15:08:25:404 new player piid:123..."]
}

        │ 2. Regex Line Parsing
        ▼
ParsedLogEntry {
  EventType: "PLAYER_CREATED",
  Timestamp: null,
  Fields: {
    "timestamp": "09-04 15:08:25:404",
    "piid": 123,
    "uid": 10001,
    "package": "com.sec.android.app.camera"
  },
  RawLine: "09-04 15:08:25:404 new player...",
  SectionId: "playback_activity"
}

        │ 3. Timestamp Normalization
        ▼
ParsedLogEntry {
  EventType: "PLAYER_CREATED",
  Timestamp: DateTime(2025, 9, 4, 15, 8, 25, 404),  ← 정규화됨
  Fields: { ... },
  RawLine: "...",
  SectionId: "playback_activity"
}

        │ 4. NormalizedLogEvent 생성
        ▼
NormalizedLogEvent {
  EventId: Guid.NewGuid(),
  Timestamp: DateTime(2025, 9, 4, 6, 8, 25, 404, DateTimeKind.Utc),  ← UTC 변환
  EventType: "PLAYER_CREATED",
  SourceSection: "playback_activity",
  Attributes: {
    "piid": 123,
    "uid": 10001,
    "package": "com.sec.android.app.camera"
  },
  RawLine: "09-04 15:08:25:404 new player...",
  SourceFileName: "audio.txt",
  DeviceInfo: { TimeZone: "Asia/Seoul", ... }
}
```

---

## 클래스 다이어그램

### 핵심 모델 관계

```
┌────────────────────┐
│   DeviceInfo       │
├────────────────────┤
│ + TimeZone         │
│ + CurrentTime      │
│ + AndroidVersion   │
└────────────────────┘
         ▲
         │ contains
         │
┌────────────────────┐       ┌────────────────────┐
│ LogParsingOptions  │       │  ParsingResult     │
├────────────────────┤       ├────────────────────┤
│ + DeviceInfo       │       │ + bool Success     │
│ + ConvertToUtc     │       │ + Events[]         │
│ + Encoding         │       │ + Statistics       │
│ + MaxFileSizeMB    │       │ + Errors[]         │
└────────────────────┘       └────────────────────┘
         │                            ▲
         │ input                      │ output
         │                            │
         └──────────────┬─────────────┘
                        │
                        ▼
          ┌──────────────────────────┐
          │      ILogParser          │
          │  (AdbLogParser)          │
          └──────────────────────────┘
                        │
                        │ uses
                        │
         ┌──────────────┼──────────────┐
         │              │              │
         ▼              ▼              ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ISectionSplit│ │ ILineParser │ │ Timestamp   │
│  ter        │ │             │ │ Normalizer  │
└─────────────┘ └─────────────┘ └─────────────┘
```

### Configuration 모델 계층

```
┌────────────────────────────────┐
│     LogConfiguration           │
│  (Root of YAML structure)      │
└────────────────────────────────┘
         │
         ├───── Metadata (ConfigMetadata)
         │      ├─ LogType
         │      ├─ SupportedVersions[]
         │      └─ DisplayName
         │
         ├───── GlobalSettings
         │      ├─ TimestampFormat
         │      ├─ TimeSeriesOrder
         │      └─ Encoding
         │
         ├───── Sections[] (SectionConfig)
         │      ├─ Id
         │      ├─ StartMarker
         │      ├─ EndMarker
         │      └─ MarkerType
         │
         └───── Parsers[] (ParserConfig)
                ├─ Id
                ├─ TargetSections[]
                └─ LinePatterns[] (LinePatternConfig)
                   ├─ Id
                   ├─ EventType
                   ├─ Regex
                   └─ Fields{} (FieldDefinition)
                      ├─ Group
                      ├─ Type
                      └─ Format
```

---

## 설계 원칙

### 1. 단순함 우선 (Simplicity First)

**원칙:**
- 오버 엔지니어링 금지
- 당장 필요한 기능만 구현
- 복잡한 디자인 패턴 지양

**적용 예:**
- ✅ Regex 기반 파싱 (단순하고 효과적)
- ✅ YAML 설정 (JSON보다 읽기 쉬움)
- ✅ InMemory Repository (초기 단계)
- ❌ Plugin 시스템 (Phase 7 이후로 연기)
- ❌ Migration Service (Phase 7 이후로 연기)

---

### 2. 불변성 (Immutability)

**원칙:**
- 모든 모델은 불변 객체
- `init` 프로퍼티 사용 (`set` 금지)
- `IReadOnly*` 컬렉션 반환

**적용 예:**
```csharp
// ✅ 좋은 예: 불변 객체
public sealed class NormalizedLogEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public IReadOnlyDictionary<string, object> Attributes { get; init; }
}

// ❌ 나쁜 예: 가변 객체
public class NormalizedLogEvent
{
    public Guid EventId { get; set; }
    public Dictionary<string, object> Attributes { get; set; }
}
```

---

### 3. 책임 분리 (Separation of Concerns)

**DLL 책임:**
- ✅ 로그 파일 파싱
- ✅ 타임스탬프 정규화
- ✅ 이벤트 추출 및 저장

**상위 앱 책임:**
- ✅ 상관관계 분석
- ✅ 이벤트 감지 (카메라 촬영 등)
- ✅ 타임라인 생성
- ✅ UI 표시

---

### 4. 인터페이스 기반 설계 (Interface-Based Design)

**주요 인터페이스:**
- `ILogParser` - 파서 추상화
- `ILogEventRepository` - 저장소 추상화
- `IConfigurationLoader` - 설정 로더 추상화
- `ISectionSplitter` - 섹션 분할 추상화
- `ILineParser` - 라인 파싱 추상화

**이점:**
- 테스트 용이 (Mock 가능)
- 구현체 교체 가능
- 확장 가능

---

### 5. 성능 최적화

**적용된 최적화:**
1. **RegexLineParser 캐싱**
   - 파서 인스턴스당 Regex 미리 컴파일
   - `Dictionary<string, List<ILineParser>>` 캐시

2. **스레드 안전성**
   - `ReaderWriterLockSlim` 사용 (Repository)
   - 읽기 동시성 최대화

3. **파일 크기 검증**
   - 파싱 전 파일 크기 체크
   - `MaxFileSizeMB` 초과 시 예외

---

## 확장성

### 현재 확장 포인트

#### 1. 새로운 로그 타입 추가

**방법: YAML 설정 파일만 작성**
```yaml
configSchemaVersion: "1.0"
metadata:
  logType: "adb_battery"
  supportedVersions: ["*"]
# ... 섹션 및 파서 정의
```

**코드 수정 불필요!**

---

#### 2. 새로운 저장소 구현

**방법: ILogEventRepository 구현**
```csharp
public class SqliteLogEventRepository : ILogEventRepository
{
    public async Task<bool> SaveEventAsync(NormalizedLogEvent logEvent)
    {
        // SQLite에 저장
    }
    // ... 기타 메서드
}
```

---

#### 3. 커스텀 파서 추가 (Phase 7 이후)

**방법: ICustomLogParser 구현**
```csharp
public class ComplexPatternParser : ICustomLogParser
{
    public bool CanParse(string line, ParsingContext context)
    {
        // 복잡한 조건 체크
    }
    
    public ParsedLogEntry Parse(string line, ParsingContext context)
    {
        // 복잡한 파싱 로직
    }
}
```

---

### 미래 확장 계획

#### Phase 7: Plugin System
- `ICustomLogParser` 인터페이스 구현
- AssemblyLoadContext 기반 플러그인 로드
- 플러그인 격리 및 예외 처리

#### Phase 8: Database Integration
- `ILogEventRepository` 구현체 추가
  - `SqliteLogEventRepository`
  - `SqlServerLogEventRepository`

#### Phase 9: 스트리밍 파서
- 대용량 파일 (> 50MB) 처리
- 청크 단위 파싱
- 메모리 효율성 개선

---

## 의존성 다이어그램

```
┌─────────────────────────────────────────────────────────┐
│              External Dependencies                       │
├─────────────────────────────────────────────────────────┤
│ - YamlDotNet                (YAML 파싱)                 │
│ - Microsoft.Extensions.Logging.Abstractions             │
└─────────────────────────────────────────────────────────┘
                          ▲
                          │
┌─────────────────────────────────────────────────────────┐
│         AndroidAdbAnalyzeModule (Self-Contained)        │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Core ◄─────┬───────► Configuration                     │
│             │                                            │
│             ├───────► Parsing                            │
│             │                                            │
│             ├───────► Preprocessing                      │
│             │                                            │
│             └───────► Repositories                       │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

**외부 의존성 최소화**:
- ✅ YamlDotNet (YAML 파싱 필수)
- ✅ Microsoft.Extensions.Logging (표준 로깅)
- ❌ UI 프레임워크 의존성 없음
- ❌ 데이터베이스 의존성 없음 (인터페이스만)

---

## 테스트 아키텍처

```
┌─────────────────────────────────────────────────────────┐
│           AndroidAdbAnalyzeModule.Tests                  │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  AdbLogParserEndToEndTests.cs      (34 tests)           │
│   ├─ 7가지 로그 타입별 통합 테스트                         │
│   └─ 에러 케이스 테스트 (13 tests)                        │
│                                                          │
│  ActivityLogParserTests.cs         (5 tests)            │
│  MediaCameraLogParserTests.cs      (5 tests)            │
│  MediaMetricsLogParserTests.cs     (5 tests)            │
│                                                          │
│  TestData/                                               │
│   ├─ audio.txt, vibrator_manager.txt, ...               │
│   ├─ adb_audio_config.yaml, ...                         │
│   └─ invalid_*.yaml (에러 테스트용)                       │
│                                                          │
└─────────────────────────────────────────────────────────┘

Total: 47 tests (100% passing)
```

---

## 성능 특성

| 항목 | 값 |
|------|-----|
| **처리 속도** | 1-2 MB/s |
| **메모리 사용** | 파일 크기의 2-3배 |
| **Regex 컴파일** | 파서 인스턴스당 1회 (캐싱) |
| **최대 파일 크기** | 기본 500MB (설정 가능) |
| **동시성** | 읽기 다중 스레드 지원 (Repository) |

---

## 요약

### 핵심 컴포넌트 5개
1. **AdbLogParser** - 파싱 오케스트레이터
2. **YamlConfigurationLoader** - 설정 관리
3. **RegexLineParser** - Regex 기반 파싱
4. **TimestampNormalizer** - 시간 정규화
5. **InMemoryLogEventRepository** - 이벤트 저장

### 설계 원칙 5개
1. 단순함 우선 (Simplicity First)
2. 불변성 (Immutability)
3. 책임 분리 (Separation of Concerns)
4. 인터페이스 기반 설계 (Interface-Based Design)
5. 성능 최적화

### 확장 포인트 3개
1. YAML 설정 파일 (새 로그 타입)
2. ILogEventRepository (새 저장소)
3. ICustomLogParser (커스텀 파서, Phase 7+)

---

## 참고 문서

- [API 사용 가이드](API_Usage_Guide.md)
- [설정 파일 작성 가이드](Configuration_Guide.md)
- [개발 계획](DevelopmentPlan.md)
- [개발 가이드라인](AI_Development_Guidelines.md)
- [플러그인 아키텍처](PluginArchitecture.md)

**문서 버전**: 1.0  
**최종 업데이트**: 2025-10-04
