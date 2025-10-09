# usagestats 기반 세션 탐지 전환 - 정확한 수정 범위 분석

## 📋 개요

- **분석 일자**: 2025-10-08
- **목적**: usagestats 기반 세션 탐지로 전환 시 정확한 수정 범위 분석
- **핵심 전략**: 기존 인터페이스 유지, 내부 구현만 변경 → **외부 코드 영향 최소화**

---

## 🎯 **핵심 원칙**

### **1. 외부 코드 수정 없음**
- ✅ 기존 `ISessionDetector` 인터페이스 유지
- ✅ 기존 `CameraSession` 모델 유지
- ✅ DI 등록 방식 유지
- ✅ `AnalysisOrchestrator` 수정 없음

### **2. 내부 구현만 변경**
- 📝 `CameraSessionDetector` 내부 로직 수정
- ✅ 신규 클래스 추가 (인터페이스 기반)
- ✅ 테스트 추가 (기존 테스트 유지)

### **3. 하위 호환성 유지**
- ✅ 기존 기능 모두 동작
- ✅ 기존 테스트 통과
- ✅ 점진적 개선 (단계별 배포 가능)

---

## 📊 **수정 범위 상세 분석**

### **Phase 1: SessionSource 추상화**

#### **✅ 신규 파일 (3개)**

##### **1. `ISessionSource.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Interfaces/ISessionSource.cs
// 크기: ~50 lines
// 의존성: None
// 영향: None (신규 인터페이스)

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 세션 소스 인터페이스
/// </summary>
public interface ISessionSource
{
    /// <summary>
    /// 우선순위 (높을수록 우선)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// 소스 이름 (로깅용)
    /// </summary>
    string SourceName { get; }
    
    /// <summary>
    /// 세션 추출
    /// </summary>
    IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options);
}
```

**영향도**: ✅ **없음** (신규 인터페이스)

---

##### **2. `UsagestatsSessionSource.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Services/Sessions/UsagestatsSessionSource.cs
// 크기: ~300 lines
// 의존성: ISessionSource, ILogger, IConfidenceCalculator
// 영향: None (신규 구현체)

namespace AndroidAdbAnalyze.Analysis.Services.Sessions;

/// <summary>
/// usagestats.log 기반 세션 소스 (ACTIVITY_RESUMED/PAUSED)
/// </summary>
/// <remarks>
/// 장점:
/// - 24시간 보존 (재부팅 후 분석 가능)
/// - taskRootPackage 기반 정확한 앱 구분
/// 
/// 단점:
/// - Telegram 등 자체 카메라 앱은 탐지 불가
/// </remarks>
public sealed class UsagestatsSessionSource : ISessionSource
{
    private readonly ILogger<UsagestatsSessionSource> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // 카메라 앱 패키지 목록
    private static readonly HashSet<string> CameraPackages = new()
    {
        "com.sec.android.app.camera",    // 기본 카메라
        "com.peace.SilentCamera",        // 무음 카메라
        // 추가 카메라 앱...
    };
    
    // 카메라 사용 앱 목록 (taskRootPackage 기반)
    private static readonly HashSet<string> CameraUsingApps = new()
    {
        "com.kakao.talk",                // 카카오톡
        "com.samsung.android.messaging", // 메시지
        // 추가 앱...
    };
    
    public int Priority => 100; // Primary (usagestats 우선)
    public string SourceName => "usagestats";
    
    public IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        // 1. ACTIVITY_RESUMED → ACTIVITY_PAUSED/STOPPED 매칭
        // 2. package가 CameraPackages OR taskRootPackage가 CameraUsingApps
        // 3. 세션 생성 (패키지는 taskRootPackage 우선)
    }
}
```

**주요 로직**:
1. **ACTIVITY_RESUMED** → **ACTIVITY_PAUSED/STOPPED** 매칭
2. **패키지 판단**:
   - `package in CameraPackages` → 카메라 앱 세션
   - `taskRootPackage in CameraUsingApps` → 앱 내 카메라 사용 세션
   - **패키지 이름**: `taskRootPackage` 우선 (카카오톡, 텔레그램 등 구분)
3. **신뢰도 계산**: Activity 기반 세션 = 높은 신뢰도

**영향도**: ✅ **없음** (신규 구현체, DI로 주입)

---

##### **3. `MediaCameraSessionSource.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Services/Sessions/MediaCameraSessionSource.cs
// 크기: ~250 lines
// 의존성: ISessionSource, ILogger, IConfidenceCalculator
// 영향: None (기존 로직 이동)

namespace AndroidAdbAnalyze.Analysis.Services.Sessions;

/// <summary>
/// media_camera.log 기반 세션 소스 (CAMERA_CONNECT/DISCONNECT)
/// </summary>
/// <remarks>
/// 장점:
/// - Telegram 등 자체 카메라 앱 탐지 가능
/// 
/// 단점:
/// - 휘발성 (재부팅 시 소실)
/// - taskRootPackage 없음 (카카오톡 등 구분 불가)
/// </remarks>
public sealed class MediaCameraSessionSource : ISessionSource
{
    private readonly ILogger<MediaCameraSessionSource> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    public int Priority => 50; // Secondary (usagestats 보완)
    public string SourceName => "media_camera";
    
    public IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        // ✅ 기존 CameraSessionDetector.ExtractRawSessions 로직 이동
        // CAMERA_CONNECT → CAMERA_DISCONNECT 매칭
    }
}
```

**주요 로직**:
- ✅ **기존 로직 그대로 이동** (CameraSessionDetector.ExtractRawSessions)
- CAMERA_CONNECT → CAMERA_DISCONNECT 매칭
- 패키지별 그룹화

**영향도**: ✅ **없음** (기존 로직 이동, 동작 동일)

---

#### **📝 수정 파일 (1개)**

##### **4. `CameraSessionDetector.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Services/Sessions/CameraSessionDetector.cs
// 크기: ~400 lines → ~350 lines
// 의존성: ISessionSource[] 추가
// 영향: ⚠️ 내부 로직만 변경 (외부 인터페이스 유지)

public sealed class CameraSessionDetector : ISessionDetector
{
    private readonly ILogger<CameraSessionDetector> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly IReadOnlyList<ISessionSource> _sessionSources; // ✅ 추가
    
    public CameraSessionDetector(
        ILogger<CameraSessionDetector> _logger,
        IConfidenceCalculator confidenceCalculator,
        IEnumerable<ISessionSource> sessionSources) // ✅ 추가
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
        _sessionSources = sessionSources?.OrderByDescending(s => s.Priority).ToList() 
                         ?? throw new ArgumentNullException(nameof(sessionSources));
        
        _logger.LogInformation(
            "CameraSessionDetector 초기화: SessionSource {Count}개 등록 ({Sources})",
            _sessionSources.Count,
            string.Join(", ", _sessionSources.Select(s => $"{s.SourceName}(Priority={s.Priority})")));
    }
    
    /// <inheritdoc/>
    public IReadOnlyList<CameraSession> DetectSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        // ... (기존 1단계 패키지 필터링 유지)
        
        // 2단계: 원시 세션 추출 (✅ 수정)
        var allRawSessions = new List<CameraSession>();
        foreach (var source in _sessionSources)
        {
            var sourceSessions = source.ExtractSessions(filteredEvents, options);
            _logger.LogDebug(
                "SessionSource '{Source}': {Count}개 세션 추출",
                source.SourceName, sourceSessions.Count);
            allRawSessions.AddRange(sourceSessions);
        }
        
        // 3단계: 세션 병합 (✅ 개선: Primary/Secondary 우선순위)
        var mergedSessions = MergeSessionsByPriority(allRawSessions);
        
        // ... (기존 4~6단계 유지)
    }
    
    // ❌ 제거: ExtractRawSessions (MediaCameraSessionSource로 이동)
    // ❌ 제거: ExtractSessionsFromEventSequence (MediaCameraSessionSource로 이동)
    
    // ✅ 수정: MergeSessions → MergeSessionsByPriority
    private List<CameraSession> MergeSessionsByPriority(List<CameraSession> sessions)
    {
        // 1. Primary 세션 우선 (usagestats)
        // 2. Secondary 세션과 80% 이상 겹치면 병합
        // 3. 겹치지 않으면 Secondary 세션 추가
    }
}
```

**수정 내용**:
1. ✅ **생성자**: `IEnumerable<ISessionSource>` 추가
2. ✅ **DetectSessions**: 여러 SessionSource에서 세션 추출
3. ✅ **MergeSessions**: Primary/Secondary 우선순위 기반 병합
4. ❌ **제거**: `ExtractRawSessions`, `ExtractSessionsFromEventSequence` (MediaCameraSessionSource로 이동)
5. ✅ **유지**: 패키지 필터링, 불완전 세션 처리, 신뢰도 필터링

**영향도**: ⚠️ **내부만 변경** (인터페이스 유지, 외부 코드 영향 없음)

---

#### **📝 수정 파일 (DI 등록)**

##### **5. `ServiceCollectionExtensions.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Extensions/ServiceCollectionExtensions.cs
// 크기: ~80 lines → ~100 lines
// 의존성: None
// 영향: ✅ 없음 (DI 등록 추가만)

public static IServiceCollection AddAndroidAdbAnalysis(this IServiceCollection services)
{
    // ... (기존 코드 유지)
    
    // ===== Session Sources ===== (✅ 추가)
    services.AddSingleton<ISessionSource, UsagestatsSessionSource>();   // Priority: 100
    services.AddSingleton<ISessionSource, MediaCameraSessionSource>();  // Priority: 50
    
    // Session Detector (✅ 수정: ISessionSource[] 주입됨)
    services.AddSingleton<ISessionDetector, CameraSessionDetector>();
    
    // ... (기존 코드 유지)
}
```

**영향도**: ✅ **없음** (DI 등록 추가만, 외부 코드 영향 없음)

---

### **Phase 1 요약**

| 항목 | 파일 | 수정 범위 | 영향도 |
|------|------|-----------|--------|
| ✅ 신규 | `ISessionSource.cs` | ~50 lines | **없음** (신규 인터페이스) |
| ✅ 신규 | `UsagestatsSessionSource.cs` | ~300 lines | **없음** (신규 구현체) |
| ✅ 신규 | `MediaCameraSessionSource.cs` | ~250 lines | **없음** (기존 로직 이동) |
| 📝 수정 | `CameraSessionDetector.cs` | ~50 lines 수정 | ⚠️ **내부만** (인터페이스 유지) |
| 📝 수정 | `ServiceCollectionExtensions.cs` | +10 lines | ✅ **없음** (DI 등록만) |

**총 작업량**: ~660 lines (신규 600, 수정 60)  
**영향도**: ✅ **외부 코드 영향 없음** (인터페이스 유지)

---

### **Phase 2: SessionContextProvider 패키지 필터링**

#### **📝 수정 파일 (1개)**

##### **1. `SessionContextProvider.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Services/Context/SessionContextProvider.cs
// 크기: ~150 lines → ~180 lines
// 의존성: None
// 영향: ⚠️ SessionContext.AllEvents 필터링 변경

public sealed class SessionContextProvider : ISessionContextProvider
{
    // ... (기존 코드 유지)
    
    public SessionContext CreateContext(
        CameraSession session,
        IReadOnlyList<NormalizedLogEvent> allEvents)
    {
        var sessionStart = session.StartTime.AddSeconds(-ExtendedWindowSeconds);
        var sessionEnd = (session.EndTime ?? session.StartTime).AddSeconds(ExtendedWindowSeconds);
        
        // ✅ 수정: 패키지 필터링 추가
        var sessionEvents = allEvents
            .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
            .Where(e => 
                e.PackageName == session.PackageName ||          // 세션 패키지
                IsSystemLevelEvent(e.EventType) ||               // 시스템 이벤트
                string.IsNullOrEmpty(e.PackageName))             // 패키지 정보 없음
            .OrderBy(e => e.Timestamp)
            .ToList();
        
        // ... (기존 코드 유지)
    }
    
    // ✅ 추가
    private bool IsSystemLevelEvent(string eventType)
    {
        return eventType switch
        {
            LogEventTypes.CAMERA_CONNECT => true,
            LogEventTypes.CAMERA_DISCONNECT => true,
            LogEventTypes.SCREEN_INTERACTIVE => true,
            LogEventTypes.SCREEN_NON_INTERACTIVE => true,
            LogEventTypes.KEYGUARD_SHOWN => true,
            LogEventTypes.KEYGUARD_HIDDEN => true,
            _ => false
        };
    }
}
```

**수정 내용**:
1. ✅ **CreateContext**: `AllEvents`에 패키지 필터링 추가
2. ✅ **IsSystemLevelEvent**: 시스템 레벨 이벤트 판단 로직 추가

**영향도**: ⚠️ **Strategy에 영향**
- `SessionContext.AllEvents`가 필터링됨
- 기존 Strategy: android 패키지 이벤트 필터링됨 (TelegramStrategy 수정 완료)
- ✅ **오탐 방지 효과**

**하위 호환성**:
- ⚠️ 기존 Strategy가 android 패키지 이벤트에 의존하면 영향 받음
- ✅ **TelegramStrategy**: 이미 패키지 필터링 추가 완료 → 영향 없음
- ✅ **BasePatternStrategy**: 주 증거 기반 → 영향 없음

---

### **Phase 2 요약**

| 항목 | 파일 | 수정 범위 | 영향도 |
|------|------|-----------|--------|
| 📝 수정 | `SessionContextProvider.cs` | +30 lines | ⚠️ **Strategy에 영향** (오탐 방지) |

**총 작업량**: ~30 lines  
**영향도**: ⚠️ **Strategy 검증 필요** (기존 테스트로 확인 가능)

---

### **Phase 3: CameraSession 모델 확장 (선택)**

#### **📝 수정 파일 (1개, 선택)**

##### **1. `CameraSession.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis/Models/Sessions/CameraSession.cs
// 크기: ~80 lines → ~90 lines
// 의존성: None
// 영향: ✅ 없음 (선택 필드 추가)

public sealed class CameraSession
{
    // ... (기존 필드 유지)
    
    /// <summary>
    /// 실제 앱 패키지 이름 (taskRootPackage 우선)
    /// </summary>
    /// <remarks>
    /// usagestats 기반 세션의 경우 taskRootPackage 사용
    /// - 기본 카메라: com.sec.android.app.camera
    /// - 카카오톡: com.kakao.talk
    /// - 텔레그램: org.telegram.messenger
    /// </remarks>
    public string? ActualPackageName { get; init; } // ✅ 추가 (선택)
    
    // ... (기존 코드 유지)
}
```

**수정 내용**:
1. ✅ **ActualPackageName**: taskRootPackage 기반 실제 앱 구분용

**영향도**: ✅ **없음** (선택 필드, 기존 코드는 `PackageName` 사용)

**사용 예시**:
```csharp
// UsagestatsSessionSource에서:
new CameraSession
{
    PackageName = package,                    // com.sec.android.app.camera
    ActualPackageName = taskRootPackage,      // com.kakao.talk (카카오톡)
    // ...
};

// Strategy 선택 시:
var selectedStrategy = SelectStrategy(session.ActualPackageName ?? session.PackageName);
```

---

### **Phase 3 요약**

| 항목 | 파일 | 수정 범위 | 영향도 |
|------|------|-----------|--------|
| 📝 수정 | `CameraSession.cs` | +10 lines | ✅ **없음** (선택 필드) |

**총 작업량**: ~10 lines  
**영향도**: ✅ **없음** (선택 사항)

---

## 🧪 **테스트 수정 범위**

### **기존 테스트 (유지)**

##### **1. `CameraSessionDetectorTests.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/CameraSessionDetectorTests.cs
// 수정: ⚠️ Mock 수정 필요
// 영향: ⚠️ ISessionSource Mock 추가

[Fact]
public void DetectSessions_WithMediaCameraEvents_DetectsSessions()
{
    // Arrange
    var mockLogger = new Mock<ILogger<CameraSessionDetector>>();
    var mockConfidence = new Mock<IConfidenceCalculator>();
    
    // ✅ 추가: ISessionSource Mock
    var mockMediaCameraSource = new Mock<ISessionSource>();
    mockMediaCameraSource.Setup(x => x.Priority).Returns(50);
    mockMediaCameraSource.Setup(x => x.SourceName).Returns("media_camera");
    mockMediaCameraSource.Setup(x => x.ExtractSessions(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
        .Returns(expectedSessions);
    
    var sessionSources = new List<ISessionSource> { mockMediaCameraSource.Object };
    var detector = new CameraSessionDetector(mockLogger.Object, mockConfidence.Object, sessionSources);
    
    // Act
    var result = detector.DetectSessions(events, options);
    
    // Assert
    result.Should().HaveCount(expectedSessions.Count);
}
```

**수정 내용**:
1. ✅ **Mock 추가**: `ISessionSource` Mock
2. ✅ **Setup**: `ExtractSessions` 반환값 설정

**작업량**: ~15 tests × ~5 lines = ~75 lines

---

### **신규 테스트**

##### **2. `UsagestatsSessionSourceTests.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/UsagestatsSessionSourceTests.cs
// 크기: ~500 lines (신규)
// 목적: usagestats 기반 세션 추출 검증

[Fact]
public void ExtractSessions_BasicCamera_DetectsSession()
{
    // ACTIVITY_RESUMED → ACTIVITY_PAUSED 매칭
    // package=com.sec.android.app.camera
    // taskRootPackage=com.sec.android.app.camera
}

[Fact]
public void ExtractSessions_KakaoTalkCamera_DetectsSession()
{
    // ACTIVITY_RESUMED → ACTIVITY_PAUSED 매칭
    // package=com.sec.android.app.camera
    // taskRootPackage=com.kakao.talk → 카카오톡 세션
}

[Fact]
public void ExtractSessions_SilentCamera_DetectsSession()
{
    // package=com.peace.SilentCamera
}

// ... (~20 tests)
```

**작업량**: ~500 lines

---

##### **3. `MediaCameraSessionSourceTests.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/MediaCameraSessionSourceTests.cs
// 크기: ~400 lines (신규)
// 목적: media_camera 기반 세션 추출 검증 (기존 로직)

[Fact]
public void ExtractSessions_CameraConnect_DetectsSession()
{
    // CAMERA_CONNECT → CAMERA_DISCONNECT 매칭
}

[Fact]
public void ExtractSessions_Telegram_DetectsSession()
{
    // Telegram 자체 카메라
}

// ... (~15 tests)
```

**작업량**: ~400 lines

---

##### **4. `SessionMergingTests.cs`**
```csharp
// 경로: AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/SessionMergingTests.cs
// 크기: ~300 lines (신규)
// 목적: Primary + Secondary 세션 병합 검증

[Fact]
public void MergeSessions_PrimaryAndSecondary_MergesCorrectly()
{
    // usagestats (Primary) + media_camera (Secondary)
    // 80% 이상 겹침 → 병합
}

[Fact]
public void MergeSessions_NoOverlap_KeepsBoth()
{
    // usagestats (Primary) + media_camera (Secondary)
    // 겹침 없음 → 둘 다 유지
}

// ... (~10 tests)
```

**작업량**: ~300 lines

---

### **테스트 요약**

| 항목 | 파일 | 수정 범위 | 작업량 |
|------|------|-----------|--------|
| 📝 수정 | `CameraSessionDetectorTests.cs` | Mock 수정 | ~75 lines |
| ✅ 신규 | `UsagestatsSessionSourceTests.cs` | 신규 테스트 | ~500 lines |
| ✅ 신규 | `MediaCameraSessionSourceTests.cs` | 신규 테스트 | ~400 lines |
| ✅ 신규 | `SessionMergingTests.cs` | 신규 테스트 | ~300 lines |

**총 작업량**: ~1,275 lines  
**영향도**: ✅ **기존 테스트 유지** (Mock만 수정)

---

## 📈 **전체 수정 범위 요약**

### **구현 코드**

| Phase | 항목 | 파일 수 | 작업량 | 영향도 |
|-------|------|---------|--------|--------|
| **Phase 1** | SessionSource 추상화 | 5 | ~660 lines | ✅ **외부 영향 없음** |
| **Phase 2** | SessionContextProvider 패키지 필터링 | 1 | ~30 lines | ⚠️ **Strategy 검증 필요** |
| **Phase 3** | CameraSession 모델 확장 (선택) | 1 | ~10 lines | ✅ **없음** |

**구현 총 작업량**: ~700 lines

---

### **테스트 코드**

| 항목 | 파일 수 | 작업량 |
|------|---------|--------|
| 기존 테스트 수정 | 1 | ~75 lines |
| 신규 테스트 | 3 | ~1,200 lines |

**테스트 총 작업량**: ~1,275 lines

---

### **최종 요약**

| 항목 | 작업량 |
|------|--------|
| **구현 코드** | **~700 lines** |
| **테스트 코드** | **~1,275 lines** |
| **총계** | **~1,975 lines** |

---

## 🎯 **외부 코드 영향 분석**

### **✅ 영향 없음 (외부 코드)**

1. **`AnalysisOrchestrator`**:
   - ✅ `ISessionDetector` 인터페이스 유지 → 수정 불필요
   
2. **`CameraCaptureDetector`**:
   - ✅ `CameraSession` 모델 유지 → 수정 불필요
   
3. **Integration Tests** (`EndToEndAnalysisTests`):
   - ✅ 기존 테스트 그대로 통과 예상
   - ⚠️ Ground Truth 값 검증 필요 (카카오톡 세션 분류 변경)

---

### **⚠️ 영향 있음 (내부 코드)**

1. **`CameraSessionDetector`**:
   - ⚠️ 내부 로직 변경 (인터페이스 유지)
   - ✅ 기존 기능 동작 보장
   
2. **`SessionContextProvider`**:
   - ⚠️ `AllEvents` 필터링 추가
   - ✅ Strategy 검증 필요 (기존 테스트로 확인)
   
3. **Strategy 클래스**:
   - ✅ **TelegramStrategy**: 이미 패키지 필터링 완료 → 영향 없음
   - ✅ **BasePatternStrategy**: 주 증거 기반 → 영향 없음

---

## 🚀 **구현 순서 (단계별)**

### **Step 1: Phase 1 구현** (3-4시간)
1. `ISessionSource` 인터페이스 작성
2. `MediaCameraSessionSource` 구현 (기존 로직 이동)
3. `UsagestatsSessionSource` 구현 (신규)
4. `CameraSessionDetector` 수정
5. `ServiceCollectionExtensions` 수정

**검증**:
- ✅ 빌드 성공
- ✅ 기존 테스트 통과 (Mock 수정 후)

---

### **Step 2: Phase 1 테스트** (2-3시간)
1. `UsagestatsSessionSourceTests` 작성
2. `MediaCameraSessionSourceTests` 작성
3. `SessionMergingTests` 작성
4. `CameraSessionDetectorTests` Mock 수정

**검증**:
- ✅ 신규 테스트 통과
- ✅ 기존 테스트 통과

---

### **Step 3: Phase 2 구현** (1시간)
1. `SessionContextProvider` 패키지 필터링 추가

**검증**:
- ✅ 기존 테스트 통과 (오탐 방지 효과 확인)

---

### **Step 4: Integration Test** (1-2시간)
1. `EndToEndAnalysisTests` 실행
2. Ground Truth 검증 (카카오톡 세션 분류 변경)
3. 5차 샘플 테스트 검증

**검증**:
- ✅ 카카오톡 세션 정확히 분류
- ✅ 오탐지 제거 확인

---

### **Step 5: Phase 3 구현 (선택)** (30분)
1. `CameraSession.ActualPackageName` 필드 추가
2. Strategy 선택 로직 개선

**검증**:
- ✅ 기존 기능 동작 확인

---

## ⏱️ **예상 작업 시간**

| Phase | 작업 | 예상 시간 |
|-------|------|-----------|
| **Step 1** | Phase 1 구현 | 3-4시간 |
| **Step 2** | Phase 1 테스트 | 2-3시간 |
| **Step 3** | Phase 2 구현 | 1시간 |
| **Step 4** | Integration Test | 1-2시간 |
| **Step 5** | Phase 3 구현 (선택) | 30분 |
| **총계** | - | **7.5-10.5시간** |

---

## 📝 **결론**

### **✅ 외부 코드 수정 없음**
- 기존 `ISessionDetector` 인터페이스 유지
- 기존 `CameraSession` 모델 유지
- DI 기반 구현 → 외부 코드 영향 없음

### **📊 작업량**
- **구현**: ~700 lines
- **테스트**: ~1,275 lines
- **총계**: ~1,975 lines

### **⏱️ 예상 시간**
- **7.5-10.5시간** (단계별 구현)

### **🎯 효과**
- ✅ usagestats 기반 24시간 보존
- ✅ 카카오톡/텔레그램 정확한 세션 분류
- ✅ 오탐지 제거
- ✅ 하위 호환성 유지

---

**작성일**: 2025-10-08  
**작성자**: AI Assistant  
**버전**: 1.0

