# Phase 8: 중장기 아키텍처 개선 계획

## 📋 **개요**

현재 세션 탐지는 `media_camera.log`의 `CAMERA_CONNECT`/`CAMERA_DISCONNECT`에만 의존합니다.  
하지만 이 로그는 **휘발성**이며, 재부팅 시 소실됩니다.

**요구사항**:
- `usagestats.log`는 **24시간 보존**되므로 이를 **Primary 세션 소스**로 사용
- `media_camera.log`는 **Secondary 세션 소스**로 보완 (Telegram 등 자체 카메라 앱)

---

## 🎯 **목표**

1. **usagestats 기반 세션 탐지** (Primary)
   - `ACTIVITY_RESUMED` → `ACTIVITY_PAUSED/STOPPED`로 세션 감지
   - 기본 카메라, 카카오톡, 무음 카메라 등 탐지

2. **media_camera 기반 세션 탐지** (Secondary)
   - `CAMERA_CONNECT` → `CAMERA_DISCONNECT`로 세션 감지
   - Telegram, Instagram 등 자체 카메라 앱 탐지

3. **세션 병합**
   - Primary와 Secondary 세션을 시간 기반으로 병합
   - 중복 제거 및 우선순위 관리

4. **패키지 기반 이벤트 필터링**
   - 세션 내 이벤트를 패키지/식별자 기준으로 필터링
   - 오탐 방지 (예: android 패키지의 VIBRATION_EVENT)

---

## 📊 **현재 구조 분석**

### **1. 세션 탐지** (`CameraSessionDetector`)

**현재 흐름**:
```
1. 패키지 필터링 (ApplyPackageFilters)
2. 원시 세션 추출 (ExtractRawSessions)
   - CAMERA_CONNECT → CAMERA_DISCONNECT 매칭
3. 세션 병합 (MergeSessions)
4. 불완전 세션 처리 (HandleIncompleteSessions)
5. 시스템 패키지 필터링
6. 신뢰도 필터링
```

**수정 필요**:
- `ExtractRawSessions` 메서드를 **추상화**하여 여러 세션 소스 지원
- `ISessionSource` 인터페이스 도입:
  - `MediaCameraSessionSource` (CAMERA_CONNECT/DISCONNECT)
  - `UsagestatsSessionSource` (ACTIVITY_RESUMED/PAUSED)

---

### **2. 세션 컨텍스트** (`SessionContextProvider`)

**현재 흐름**:
```csharp
public SessionContext CreateContext(
    CameraSession session,
    IReadOnlyList<NormalizedLogEvent> allEvents)
{
    // 1. 세션 시간 범위 (시작 -10초, 종료 +10초)
    var sessionStart = session.StartTime.AddSeconds(-ExtendedWindowSeconds);
    var sessionEnd = (session.EndTime ?? session.StartTime).AddSeconds(ExtendedWindowSeconds);
    
    // 2. 시간 범위 내 모든 이벤트 (패키지 필터링 없음!)
    var sessionEvents = allEvents
        .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
        .OrderBy(e => e.Timestamp)
        .ToList();
    
    // 3. usagestats 정보 추출
    var activityResumedTime = FindActivityResumedTime(sessionEvents, session.PackageName);
    var activityPausedTime = FindActivityPausedTime(sessionEvents, session.PackageName);
    var foregroundServices = ExtractForegroundServices(sessionEvents, session.PackageName);
    
    return new SessionContext
    {
        Session = session,
        AllEvents = sessionEvents,  // ← 패키지 필터링 없음!
        ActivityResumedTime = activityResumedTime,
        ActivityPausedTime = activityPausedTime,
        ForegroundServices = foregroundServices
    };
}
```

**문제점**:
- `AllEvents`에 **모든 패키지의 이벤트**가 포함됨
- `TelegramStrategy`에서 `android` 패키지의 `VIBRATION_EVENT`도 주 증거로 처리

**수정 방안**:
```csharp
// 옵션 A: SessionContextProvider에서 패키지 필터링
var sessionEvents = allEvents
    .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
    .Where(e => 
        e.PackageName == session.PackageName ||          // 세션 패키지
        IsSystemLevelEvent(e.EventType) ||               // 시스템 이벤트 (CAMERA_CONNECT 등)
        string.IsNullOrEmpty(e.PackageName))             // 패키지 정보 없음
    .OrderBy(e => e.Timestamp)
    .ToList();

// 옵션 B: Strategy에서 패키지 필터링 (현재 TelegramStrategy 수정 완료)
```

---

### **3. 촬영 탐지** (`CameraCaptureDetector`)

**현재 흐름**:
```csharp
public IReadOnlyList<CameraCaptureEvent> DetectCaptures(
    CameraSession session,
    IReadOnlyList<NormalizedLogEvent> events,
    AnalysisOptions options)
{
    // 1. SessionContext 생성 (usagestats 기반)
    var context = _contextProvider.CreateContext(session, events);
    
    // 2. Strategy 선택
    var selectedStrategy = SelectStrategy(session.PackageName);
    
    // 3. Strategy로 촬영 탐지 위임
    var captures = selectedStrategy.DetectCaptures(context, options);
    
    return captures;
}
```

**이미 usagestats를 활용 중**:
- `SessionContext`에 `ActivityResumedTime`, `ActivityPausedTime`, `ForegroundServices` 포함
- 하지만 **세션 탐지 자체는 CAMERA_CONNECT/DISCONNECT 기반**

---

## 🛠️ **수정 범위**

### **Phase 1: SessionSource 추상화**

#### **1-1. 인터페이스 정의**

```csharp
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
    /// 세션 추출
    /// </summary>
    IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options);
}
```

#### **1-2. MediaCameraSessionSource 구현**

```csharp
/// <summary>
/// media_camera.log 기반 세션 소스 (CAMERA_CONNECT/DISCONNECT)
/// </summary>
public class MediaCameraSessionSource : ISessionSource
{
    public int Priority => 50; // Secondary
    
    public IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        // 현재 CameraSessionDetector.ExtractRawSessions 로직 이동
        // CAMERA_CONNECT → CAMERA_DISCONNECT 매칭
    }
}
```

#### **1-3. UsagestatsSessionSource 구현**

```csharp
/// <summary>
/// usagestats.log 기반 세션 소스 (ACTIVITY_RESUMED/PAUSED)
/// </summary>
public class UsagestatsSessionSource : ISessionSource
{
    private static readonly HashSet<string> CameraPackages = new()
    {
        "com.sec.android.app.camera",
        "com.peace.SilentCamera",
        // 카카오톡은 taskRootPackage로 감지
    };
    
    public int Priority => 100; // Primary
    
    public IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        // ACTIVITY_RESUMED → ACTIVITY_PAUSED/STOPPED 매칭
        // 1. package가 CameraPackages에 포함된 경우
        // 2. taskRootPackage가 카메라 외 앱인 경우 (카카오톡 등)
    }
}
```

**수정 파일**:
- ✅ **신규**: `AndroidAdbAnalyze.Analysis/Interfaces/ISessionSource.cs`
- ✅ **신규**: `AndroidAdbAnalyze.Analysis/Services/Sessions/MediaCameraSessionSource.cs`
- ✅ **신규**: `AndroidAdbAnalyze.Analysis/Services/Sessions/UsagestatsSessionSource.cs`
- 📝 **수정**: `AndroidAdbAnalyze.Analysis/Services/Sessions/CameraSessionDetector.cs`
  - `ExtractRawSessions` → 여러 `ISessionSource` 사용
  - 세션 병합 로직 개선 (Primary/Secondary 우선순위)

---

### **Phase 2: SessionContextProvider 패키지 필터링**

```csharp
var sessionEvents = allEvents
    .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
    .Where(e => 
        e.PackageName == session.PackageName ||          // 세션 패키지
        IsSystemLevelEvent(e.EventType) ||               // 시스템 이벤트
        string.IsNullOrEmpty(e.PackageName))             // 패키지 정보 없음
    .OrderBy(e => e.Timestamp)
    .ToList();

private bool IsSystemLevelEvent(string eventType)
{
    // CAMERA_CONNECT, CAMERA_DISCONNECT, SCREEN_INTERACTIVE 등
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
```

**수정 파일**:
- 📝 **수정**: `AndroidAdbAnalyze.Analysis/Services/Context/SessionContextProvider.cs`
  - `CreateContext` 메서드에 패키지 필터링 추가
  - `IsSystemLevelEvent` 메서드 추가

---

### **Phase 3: 세션 병합 개선**

**요구사항**:
- Primary (usagestats) 세션과 Secondary (media_camera) 세션을 병합
- 시간 겹침이 80% 이상이면 병합
- Primary 세션 우선 (덮어쓰기)

```csharp
private List<CameraSession> MergeSessions(
    List<CameraSession> primarySessions,
    List<CameraSession> secondarySessions)
{
    var mergedSessions = new List<CameraSession>(primarySessions);
    
    foreach (var secondarySession in secondarySessions)
    {
        // Primary 세션과 겹치는지 확인
        var overlappingPrimary = mergedSessions
            .FirstOrDefault(p => CalculateOverlap(p, secondarySession) >= 0.8);
        
        if (overlappingPrimary != null)
        {
            // Primary 세션 우선, Secondary 정보만 보완
            overlappingPrimary.SourceLogTypes.AddRange(secondarySession.SourceLogTypes);
            overlappingPrimary.SourceEventIds.AddRange(secondarySession.SourceEventIds);
        }
        else
        {
            // 겹치지 않으면 Secondary 세션 추가
            mergedSessions.Add(secondarySession);
        }
    }
    
    return mergedSessions;
}
```

**수정 파일**:
- 📝 **수정**: `AndroidAdbAnalyze.Analysis/Services/Sessions/CameraSessionDetector.cs`
  - `MergeSessions` 메서드 개선

---

### **Phase 4: 테스트 추가**

**신규 테스트**:
1. `UsagestatsSessionSourceTests.cs`
   - ACTIVITY_RESUMED → PAUSED 매칭
   - 기본 카메라, 카카오톡, 무음 카메라 탐지
2. `MediaCameraSessionSourceTests.cs`
   - CAMERA_CONNECT → DISCONNECT 매칭
   - Telegram 탐지
3. `SessionMergingTests.cs`
   - Primary + Secondary 병합
   - 우선순위 검증

**수정 파일**:
- ✅ **신규**: `AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/UsagestatsSessionSourceTests.cs`
- ✅ **신규**: `AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/MediaCameraSessionSourceTests.cs`
- ✅ **신규**: `AndroidAdbAnalyze.Analysis.Tests/Services/Sessions/SessionMergingTests.cs`

---

## 📈 **예상 효과**

| 항목 | 현재 | 개선 후 |
|---|---|---|
| **24시간 보존** | ❌ (휘발성) | ✅ (usagestats Primary) |
| **Telegram 탐지** | ✅ | ✅ (media_camera Secondary) |
| **기본 카메라** | ✅ | ✅ (usagestats Primary) |
| **재부팅 후 분석** | ❌ | ✅ (usagestats 24시간) |
| **오탐 방지** | ⚠️ (패키지 필터링 없음) | ✅ (패키지 필터링 추가) |

---

## 🚀 **구현 순서**

1. **Phase 1**: SessionSource 추상화 (2-3시간)
   - `ISessionSource` 인터페이스
   - `MediaCameraSessionSource` (기존 로직 이동)
   - `UsagestatsSessionSource` (신규)
2. **Phase 2**: SessionContextProvider 패키지 필터링 (1시간)
3. **Phase 3**: 세션 병합 개선 (1-2시간)
4. **Phase 4**: 테스트 추가 및 검증 (2-3시간)

**총 예상 시간**: 6-9시간

---

## ⚠️ **리스크 및 고려사항**

### **1. usagestats에 카메라 Activity가 없는 경우**

**예시**: Telegram, Instagram 등 자체 카메라
- **대응**: media_camera Secondary 세션으로 보완

### **2. 세션 시간 불일치**

**예시**: 
- usagestats: 23:13:35 ~ 23:13:41
- media_camera: 23:13:36 ~ 23:13:40

**대응**: 
- 병합 허용 범위 확대 (±1초)
- Primary (usagestats) 시간 우선

### **3. 카카오톡 taskRootPackage 처리**

**usagestats**:
```
package=com.sec.android.app.camera
taskRootPackage=com.kakao.talk
```

**대응**:
- `taskRootPackage`가 카카오톡이면 카카오톡 세션으로 분류
- `package`는 `com.sec.android.app.camera`지만 실제 촬영은 카카오톡

### **4. 기존 코드 영향 최소화**

**전략**:
- `CameraSessionDetector`의 public API는 유지
- 내부 로직만 변경 (SessionSource 사용)
- 기존 테스트는 그대로 통과해야 함

---

## 📝 **결론**

**즉시 해결**:
- ✅ TelegramStrategy 패키지 필터링 추가 → 오탐 3개 제거

**중장기 개선**:
- 📋 usagestats 기반 Primary 세션 탐지 (24시간 보존)
- 📋 media_camera 기반 Secondary 세션 보완 (Telegram 등)
- 📋 패키지 기반 이벤트 필터링 강화 (오탐 방지)
- 📋 세션 병합 개선 (Primary/Secondary 우선순위)

**다음 단계**:
1. 사용자 승인
2. Phase 1부터 단계적 구현
3. 각 Phase별 테스트 및 검증

