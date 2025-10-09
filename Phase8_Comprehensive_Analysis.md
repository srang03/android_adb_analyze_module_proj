# Phase 8: 종합 분석 및 아키텍처 재검토

## 📊 **1. PLAYER_EVENT 제외 시 커버리지 분석**

### **4차 샘플 증거 분포 (2025-10-06 22:46~22:59)**

| 증거 타입 | 로그 소스 | 발생 횟수 | 커버하는 촬영 |
|-----------|-----------|-----------|---------------|
| **DATABASE_INSERT** | - | **0개** | - |
| **DATABASE_EVENT** | - | **0개** | - |
| **MEDIA_INSERT_END** | - | **0개** | - |
| **URI_PERMISSION_GRANT** | activity.log | **4개** | 카카오톡 3개 + 앨범 1개 |
| **PLAYER_EVENT** | audio.log | **7개** | 기본 2, 카카오톡 1, 텔레그램 3, 앨범 1 |
| **SILENT_CAMERA_CAPTURE** | activity.log | **1개** | 무음 카메라 1개 |

### **URI_PERMISSION_GRANT 상세**
```
카카오톡 촬영 (3개):
1. 22:48:51.594: +10123<1> content://.../tmp/temp_1759758531580.jpg  ← 촬영 1
2. 22:49:52.810: +10123<1> content://.../tmp/temp_1759758592792.jpg  ← 촬영 2
3. 22:50:54.667: +10123<1> content://.../tmp/temp_1759758654649.jpg  ← 촬영 3

카카오톡 앨범 전송 (1개):
4. 22:52:32.718: +10365<1> content://media/external/images/media/1190  ← 앨범 (오탐 위험)
```

### **커버리지 분석 결과**

#### **방안 A: PLAYER_EVENT 제거 시**
| 앱 | 촬영 시나리오 | 확정 주 증거 | 조건부 주 증거 | 탐지 여부 |
|-----|---------------|--------------|----------------|-----------|
| 기본 카메라 | 촬영 없음 (Session 1) | ❌ | ❌ | ✅ 정상 (촬영 없음) |
| 기본 카메라 | 촬영 1회 (Session 2) | ❌ | ❌ | ❌ **미탐지** |
| 카카오톡 | 촬영 없음 (Session 3) | ❌ | URI_GRANT ✅ | ⚠️ **오탐 위험** (temp file) |
| 카카오톡 | 촬영 1회 (Session 4) | ❌ | URI_GRANT ✅ | ✅ 탐지 가능 |
| 카카오톡 | 촬영+전송 (Session 5) | ❌ | URI_GRANT ✅ | ✅ 탐지 가능 |
| 카카오톡 | 앨범 전송 | ❌ | URI_GRANT ✅ | ⚠️ **오탐** (media path) |
| 텔레그램 | 촬영 없음 (Session 6) | ❌ | ❌ | ✅ 정상 (촬영 없음) |
| 텔레그램 | 촬영 1회 (Session 7) | ❌ | ❌ | ❌ **미탐지** |
| 텔레그램 | 촬영+전송 (Session 8) | ❌ | ❌ | ❌ **미탐지** |
| 무음 카메라 | 촬영 없음 (Session 9) | ❌ | ❌ | ✅ 정상 (촬영 없음) |
| 무음 카메라 | 촬영 1회 (Session 10) | ❌ | SILENT ✅ | ✅ 탐지 |

**결과**:
- **탐지 성공**: 4개 / 6개 (66.7%)
  - 카카오톡 2개 ✅
  - 무음 카메라 1개 ✅
  - 촬영 없음 4개 ✅ (정상)
- **미탐지**: 3개 / 6개 (50%)
  - 기본 카메라 1개 ❌
  - 텔레그램 2개 ❌
- **오탐 위험**: 2개
  - 카카오톡 Session 3 (temp file 남음)
  - 카카오톡 앨범 전송

---

## 🎯 **2. PLAYER_EVENT를 세션 내로 한정하는 방안**

### **현재 구현 검토**

```csharp
// CameraCaptureDetector.cs Line 79-106
private List<NormalizedLogEvent> FilterSessionEvents(
    CameraSession session,
    IReadOnlyList<NormalizedLogEvent> events)
{
    var startTime = session.StartTime;
    var endTime = session.EndTime ?? DateTime.MaxValue;

    return events
        .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
        .OrderBy(e => e.Timestamp)
        .ToList();
}
```

**현재 이미 세션 내 필터링 구현됨!** ✅

### **문제점 분석**

#### **문제 1: 텔레그램 Player 재사용**
```
Session 8 (22:55:24 ~ 22:55:38):
- 세션 내 PLAYER_EVENT: 22:55:39.364, 22:55:39.368 (촬영)
- 세션 외 PLAYER_EVENT: 22:57:02.200 (앨범 전송, 82초 후)

현재 코드: 세션 외 이벤트(22:57:02)는 이미 필터링됨 ✅
```

#### **문제 2: 세션 종료 시점 부정확**
```log
audio.log:
- 22:55:39.363: new player piid:375 (텔레그램)
- 22:55:39.364: player piid:375 event:started (촬영)
- 22:57:02.200: player piid:375 event:started (앨범 전송)
- releasing player 없음

media_camera.log:
- 22:55:24: CONNECT (텔레그램)
- 22:55:38: DISCONNECT (텔레그램)  ← 세션 종료

세션은 22:55:38에 종료되었지만, player는 계속 유지됨
→ 22:57:02 이벤트는 세션 외부이므로 이미 제외됨 ✅
```

**결론**: **세션 내 필터링은 이미 정상 동작 중입니다!**

### **실제 문제: 세션 내 중복**

```
Session 2 (기본 카메라, 22:47:40 ~ 22:47:50):
- 22:47:45.699: PLAYER_EVENT #1
- 22:47:49.103: PLAYER_EVENT #2 (3.4초 후, 세션 내)

Session 8 (텔레그램, 22:55:24 ~ 22:55:38):
- 22:55:39.364: PLAYER_EVENT #1
- 22:55:39.368: PLAYER_EVENT #2 (4ms 후, 세션 내)
```

**세션 내 중복은 EventDeduplicator가 처리해야 함**

---

## 🏗️ **3. 세션 기반 로그 상관관계 분석의 타당성**

### **현재 아키텍처**

```
[Phase 1] Parsing
  ↓ NormalizedLogEvent[]
  
[Phase 2] Deduplication (이벤트 단위)
  ↓ Deduplicated NormalizedLogEvent[]
  
[Phase 3] Session Detection (세션 생성)
  ↓ CameraSession[]
  
[Phase 4] Capture Detection (세션 내 이벤트 필터링)
  ↓ CameraCaptureEvent[]
```

### **제안: 세션 우선 접근**

```
[Phase 1] Parsing
  ↓ NormalizedLogEvent[]
  
[Phase 2] Session Detection (먼저 세션 식별)
  ↓ CameraSession[]
  
[Phase 3] Session-Scoped Deduplication (세션 내 중복 제거)
  ↓ Deduplicated Events per Session
  
[Phase 4] Capture Detection (세션별 촬영 감지)
  ↓ CameraCaptureEvent[]
```

### **장단점 비교**

#### **현재 방식 (이벤트 우선)**
✅ **장점**:
- 중복 제거가 먼저 수행되어 성능 향상
- 세션 감지 시 이미 정제된 이벤트 사용
- 로그 타입별 독립적 처리 가능

❌ **단점**:
- 세션 컨텍스트 없이 중복 판단 어려움
- 세션 외부 이벤트와 내부 이벤트 구분 불가
- 시간 기반 임계값에만 의존

#### **세션 우선 방식**
✅ **장점**:
- 세션 컨텍스트 기반 중복 판단 가능
- 세션 외부 이벤트 자동 제외
- 촬영 관련 이벤트만 집중 처리

❌ **단점**:
- 세션 감지 자체에 노이즈 포함
- 불완전 세션 처리 복잡도 증가
- 세션 없는 이벤트 누락 가능

### **권장 사항: 하이브리드 접근** 🌟

```
[Phase 1] Parsing
  ↓
[Phase 2-A] Global Deduplication (명확한 중복만)
  - 동일 EventType + 동일 Attributes + ±100ms
  ↓
[Phase 2-B] Session Detection
  ↓
[Phase 2-C] Session-Scoped Deduplication (세션 내 중복)
  - 동일 player piid에서 첫 번째 event만
  - 세션 시작 후 X초 이내 이벤트만
  ↓
[Phase 3] Capture Detection
```

### **예외 로그 처리**

**카테고리 1: 세션 후 지연 이벤트**
```csharp
// 세션 종료 후 N초 이내 이벤트는 세션에 포함
private const int SessionExtensionSeconds = 5;

var sessionEndExtended = session.EndTime?.AddSeconds(SessionExtensionSeconds);
var sessionEvents = events
    .Where(e => e.Timestamp >= session.StartTime && 
                e.Timestamp <= sessionEndExtended)
    .ToList();
```

**적용 대상**:
- DATABASE_INSERT (파일 저장 완료까지 1-3초 소요)
- MEDIA_INSERT_END (미디어 스캔 완료까지 2-5초 소요)
- URI_PERMISSION_REVOKE (권한 해제 지연)

**카테고리 2: 세션 전 선행 이벤트**
```csharp
// 세션 시작 N초 전 이벤트도 포함 (준비 단계)
private const int SessionPrepareSeconds = 2;

var sessionStartPrepare = session.StartTime.AddSeconds(-SessionPrepareSeconds);
```

**적용 대상**:
- PLAYER_CREATED (셔터음 player 미리 생성)
- URI_PERMISSION_GRANT (Intent 전달 시점)

---

## 🔍 **4. 중복 처리 로직 재검토**

### **현재 EventDeduplicator 분석**

#### **구조**
```csharp
// EventDeduplicator.cs
public class EventDeduplicator
{
    // 1. 이벤트 타입별 그룹화
    var eventsByType = events.GroupBy(e => e.EventType);
    
    // 2. 타입별 전략 적용
    foreach (var typeGroup in eventsByType)
    {
        var strategy = _strategies.GetValueOrDefault(typeGroup.Key, _defaultStrategy);
        var deduplicated = strategy.Deduplicate(typeGroup.ToList());
        // ...
    }
}
```

#### **전략별 임계값**
```csharp
TimeThresholds = new()
{
    { CAMERA_CONNECT, 1000 },      // ±1초
    { CAMERA_DISCONNECT, 1000 },   // ±1초
    { DATABASE_INSERT, 500 },      // ±500ms
    { PLAYER_EVENT, 100 },         // ±100ms ← 문제!
    { URI_PERMISSION_GRANT, 200 }, // ±200ms
};
```

### **문제점 식별**

#### **문제 1: PLAYER_EVENT 임계값 부족**
```
텔레그램 Session 8:
- 22:55:39.364: PLAYER_EVENT #1
- 22:55:39.368: PLAYER_EVENT #2 (4ms 간격)

현재 임계값: ±100ms
→ 중복 제거 **가능** ✅

하지만 실제 테스트 결과: 여전히 2개 감지
→ EventDeduplicator가 제대로 동작하지 않는 것으로 추정
```

#### **실제 코드 확인 필요**

<function_calls>
<invoke name="read_file">
<parameter name="target_file">AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Analysis/Services/Deduplication/Strategies/TimeBasedDeduplicationStrategy.cs
