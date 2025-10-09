# Phase 8 코드베이스 정밀 분석 리포트

**작성일**: 2025-10-05  
**분석 대상**: 세션/촬영 감지 로직 (CameraSessionDetector, CameraCaptureDetector)  
**목적**: 세션 과다 감지(8개 vs 예상 5개) 및 촬영 미감지(0개 vs 예상 3개) 원인 파악

---

## 📊 테스트 환경 설정

### AnalysisOptions 설정값 (CreateDefaultAnalysisOptions)
**위치**: `EndToEndAnalysisTests.cs` 라인 171-193

```csharp
MinConfidenceThreshold = 0.3          // 30% (매우 낮음)
MaxSessionGap = TimeSpan.FromMinutes(5)
EventCorrelationWindow = TimeSpan.FromSeconds(30)
EnableIncompleteSessionHandling = true
ScreenshotPathPatterns = ["screenshot", "Screenshot"]
DownloadPathPatterns = ["download", "Download"]
PackageWhitelist = null               // 모든 패키지 분석
```

**중요**: `MinConfidenceThreshold = 0.3`은 매우 낮은 값으로, 대부분의 세션/촬영이 통과합니다.

---

## 🔍 Part 1: 세션 과다 감지 원인 분석

### 문제 현황
```
예상: 5개 세션
실제: 8개 세션 (60% 과다)

파싱 이벤트:
- CAMERA_CONNECT: 10개
- CAMERA_DISCONNECT: 24개
→ 불균형: 14개 DISCONNECT 미매칭
```

---

### 1.1. ExtractSessionsFromEventSequence() 로직 분석

**위치**: `CameraSessionDetector.cs` 라인 163-228

#### 알고리즘:
```
currentStart = null
sessionEvents = []

FOR EACH event:
    IF event is CAMERA_CONNECT:
        IF currentStart != null:
            → 이전 세션 종료 (MissingEnd)  ← ⚠️ 문제점 1
        currentStart = event
        sessionEvents = [event]
    
    ELSE IF event is CAMERA_DISCONNECT:
        IF currentStart != null:
            → 정상 세션 완료
            currentStart = null
        ELSE:
            → MissingStart 세션 생성  ← ⚠️ 문제점 2
    
    ELSE:
        → 세션 내 이벤트 추가

IF currentStart != null:
    → 마지막 세션 종료 (MissingEnd)  ← ⚠️ 문제점 3
```

#### 🔴 문제점 1: 중첩 CONNECT 처리 (라인 178-184)
```csharp
if (SessionStartTypes.Contains(evt.EventType))
{
    // 새 세션 시작
    if (currentStart != null)
    {
        // 이전 세션 종료 (불완전)
        sessions.Add(CreateSession(
            currentStart, null, packageName, sourceType, sessionEvents, 
            SessionIncompleteReason.MissingEnd));
    }
    currentStart = evt;
    sessionEvents = new List<NormalizedLogEvent> { evt };
}
```

**시나리오**:
```
21:58:03 CONNECT (session 1 시작)
21:58:09 (세션 1 진행 중...)
21:59:08 CONNECT (session 2 시작) → session 1이 MissingEnd로 강제 종료!
```

**영향**:
- 실제로는 DISCONNECT가 누락된 것인데, 새로운 완전한 세션으로 착각
- CONNECT가 10개면 최대 9개의 MissingEnd 세션 생성 가능

#### 🔴 문제점 2: 고아 DISCONNECT 처리 (라인 203-206)
```csharp
else
{
    // 시작 없이 종료 (불완전)
    sessions.Add(CreateSession(
        evt, evt, packageName, sourceType, new List<NormalizedLogEvent> { evt },
        SessionIncompleteReason.MissingStart));
}
```

**시나리오**:
```
(세션 진행 중이 아님)
21:58:09 DISCONNECT → MissingStart 세션 생성!
```

**영향**:
- 14개의 미매칭 DISCONNECT → 최대 14개의 MissingStart 세션 생성
- **이것이 세션 과다 생성의 주범!**

#### 🔴 문제점 3: 마지막 세션 미종료 (라인 220-225)
```csharp
if (currentStart != null)
{
    sessions.Add(CreateSession(
        currentStart, null, packageName, sourceType, sessionEvents,
        SessionIncompleteReason.MissingEnd));
}
```

**영향**:
- 로그가 끝날 때까지 DISCONNECT가 없으면 MissingEnd 세션 생성

---

### 1.2. MergeSessions() 로직 분석

**위치**: `CameraSessionDetector.cs` 라인 270-329

#### 알고리즘:
```csharp
MinOverlapRatio = 0.8  // 80% 겹침 필요

private double CalculateOverlapRatio(CameraSession s1, CameraSession s2)
{
    // 불완전 세션은 겹침 계산 불가
    if (!s1.EndTime.HasValue || !s2.EndTime.HasValue)
        return 0.0;  ← ⚠️ 문제점!
    
    // ... 겹침 계산 ...
}
```

#### 🔴 치명적 문제: 불완전 세션 병합 불가 (라인 337-338)
```csharp
if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
    return 0.0; // 겹침 없음
```

**영향**:
- MissingStart 세션 (StartTime == EndTime)
- MissingEnd 세션 (EndTime == null)
- **이들은 병합 대상에서 제외됨!**
- **결과**: 불완전 세션들이 그대로 유지되어 세션 수 증가

---

### 1.3. 세션 과다 생성 시나리오 (추정)

#### Ground Truth (2차 샘플):
```
세션 1: 21:58:03~09 (촬영 없음)
세션 2: 21:59:08~18 (촬영 1회)
세션 3: 22:01:05~10 (촬영 없음)
세션 4: 22:02:17~32 (촬영 1회)
세션 5: 22:03:58~22:04:08 (촬영 1회)
```

#### 실제 파싱 이벤트 (추정):
```
CONNECT: 10개
DISCONNECT: 24개

패턴 추정:
- media_camera_worker.log: CONNECT 5개, DISCONNECT 5개 (정상)
- media_camera.log: CONNECT 5개, DISCONNECT 19개 (불균형!)
```

#### 생성된 세션 (추정):
```
ExtractRawSessions() 단계:
- media_camera_worker.log: 5개 완전 세션
- media_camera.log: 5개 CONNECT + 19개 DISCONNECT
  → 5개 완전 세션
  → 14개 MissingStart 세션 (고아 DISCONNECT)
= 총 24개 원시 세션

MergeSessions() 단계:
- 완전 세션 10개 → 일부 병합 (80% 겹침)
- MissingStart 세션 14개 → 병합 불가 (EndTime 없음)
= 총 8-12개 세션 (실제: 8개)
```

---

### 1.4. 근본 원인 및 해결 방안

#### 🎯 근본 원인
1. **DISCONNECT 과다**: 24개 vs CONNECT 10개
   - 파싱 설정 오류 또는 실제 로그 특성
2. **고아 DISCONNECT 처리**: 별도 세션으로 생성
3. **불완전 세션 병합 불가**: MergeSessions()에서 제외

#### 💡 해결 방안

**Option A: 고아 DISCONNECT 무시** (권장)
```csharp
// ExtractSessionsFromEventSequence() 라인 203-206 수정
else
{
    // 시작 없이 종료 (불완전)
    // ⚠️ 고아 DISCONNECT는 무시 (로그 노이즈로 간주)
    _logger.LogDebug("고아 DISCONNECT 무시: {EventId}", evt.EventId);
    continue;  // ← 세션 생성하지 않음
}
```

**Option B: 불완전 세션 병합 로직 개선**
```csharp
// CalculateOverlapRatio() 수정
private double CalculateOverlapRatio(CameraSession s1, CameraSession s2)
{
    // MissingStart 세션: StartTime == EndTime
    // MissingEnd 세션: EndTime == null
    
    // MissingStart 세션의 경우 StartTime을 EndTime으로 간주
    var end1 = s1.EndTime ?? s1.StartTime;
    var end2 = s2.EndTime ?? s2.StartTime;
    
    // ... 겹침 계산 ...
}
```

**Option C: 로그 소스별 신뢰도 가중치**
```csharp
// media_camera.log의 DISCONNECT는 신뢰도 낮게 설정
// media_camera_worker.log를 우선 신뢰
```

---

## 🔍 Part 2: 촬영 미감지 원인 분석

### 문제 현황
```
예상: 3개 촬영
실제: 0개 촬영 (100% 누락)

주 증거 타입:
- DATABASE_INSERT
- DATABASE_EVENT
- MEDIA_INSERT_END
```

---

### 2.1. DetectPrimaryEvidenceCaptures() 로직 분석

**위치**: `CameraCaptureDetector.cs` 라인 98-169

#### 알고리즘:
```
FOR EACH session:
    sessionEvents = FilterSessionEvents(session, events)
    
    primaryEvidences = sessionEvents
        .Where(e => PrimaryEvidenceTypes.Contains(e.EventType))
    
    FOR EACH primaryEvidence:
        IF IsExcludedByPathPattern(primaryEvidence, options):
            → SKIP (스크린샷/다운로드 제외)
        
        supportingEvidences = CollectSupportingEvidences(
            primaryEvidence, sessionEvents, ±30초)
        
        confidence = CalculateConfidence(all evidences)
        
        IF confidence < options.MinConfidenceThreshold (0.3):
            → SKIP
        
        → CameraCaptureEvent 생성
```

---

### 2.2. 가능한 원인들

#### 🔴 원인 1: 주 증거 이벤트 부재 (가능성 **높음**)
```
테스트 출력:
📝 Top 15 Event Types:
  - CAMERA_CONNECT: 10개
  - CAMERA_DISCONNECT: 24개
  - (DATABASE_INSERT, DATABASE_EVENT, MEDIA_INSERT_END 없음?)
```

**확인 필요**:
- 파싱 로그에서 `DATABASE_INSERT`, `DATABASE_EVENT`, `MEDIA_INSERT_END` 개수 확인
- 0개라면 → 파싱 설정 오류 또는 로그 파일에 실제 없음

#### 🔴 원인 2: 세션 시간 범위 밖 (가능성 중간)
```csharp
// FilterSessionEvents() 라인 85-92
var startTime = session.StartTime;
var endTime = session.EndTime ?? DateTime.MaxValue;

return events
    .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
```

**시나리오**:
```
세션 2: 21:59:08~18 (10초)
촬영 시각: 21:59:13 (세션 내) ✅

하지만 세션이 MissingEnd로 인해 잘못된 시간 범위를 가질 경우:
세션 2: 21:59:08~21:59:08 (MissingStart로 인식)
촬영 시각: 21:59:13 (세션 밖!) ❌
```

**확인 필요**:
- 실제 감지된 8개 세션의 시간 범위
- Ground Truth 촬영 시각이 세션 범위 내에 있는지

#### 🔴 원인 3: 경로 패턴 오작동 (가능성 **낮음**)
```csharp
// IsExcludedByPathPattern() 라인 246-268
if (filePath.Contains("screenshot", StringComparison.OrdinalIgnoreCase))
    return true;
if (filePath.Contains("download", StringComparison.OrdinalIgnoreCase))
    return true;
```

**확인 필요**:
- 주 증거 이벤트의 `file_path` 속성 확인
- 정상 촬영인데 `screenshot` 또는 `download` 포함 여부

#### 🔴 원인 4: 신뢰도 미달 (가능성 **매우 낮음**)
```csharp
if (confidence < options.MinConfidenceThreshold)  // 0.3
    continue;
```

**주 증거 가중치** (ConfidenceCalculator):
```
DATABASE_INSERT: 0.5
DATABASE_EVENT: 0.5
MEDIA_INSERT_END: 0.5
```

**확인 필요**:
- 주 증거가 있으면 최소 0.5 점수 → 0.3 임계값 통과
- 신뢰도 미달 가능성은 매우 낮음

---

### 2.3. 촬영 미감지 근본 원인 (추정)

#### 🎯 최우선 의심: 주 증거 이벤트 부재

**가설 1**: `DATABASE_INSERT`, `DATABASE_EVENT`, `MEDIA_INSERT_END`가 로그에 없음
- **원인**: 파싱 설정 파일에서 해당 이벤트 타입을 정의하지 않음
- **확인 방법**: 
  1. 테스트 출력에서 이벤트 타입 통계 확인 (Top 20으로 확대)
  2. 각 로그 파일 설정 (yaml) 검토

**가설 2**: 이벤트는 있지만 다른 타입명으로 파싱됨
- **원인**: 로그 설정 파일에서 `eventType` 값이 다름
- **확인 방법**:
  - `media_camera_worker.log` → `DATABASE_EVENT` 정의 확인
  - `media_camera.log` → `CAMERA_EVENT` 사용 여부 확인
  - `media_metrics.log` → `MEDIA_INSERT_END` 정의 확인

**가설 3**: 세션 시간 범위 오류로 주 증거 필터링됨
- **원인**: 세션 과다 생성으로 인한 시간 범위 왜곡
- **확인 방법**: 각 세션의 StartTime, EndTime과 주 증거 타임스탬프 비교

---

### 2.4. 해결 방안

#### 💡 Option A: 이벤트 타입 매핑 확인 및 수정
1. **현재 파싱된 이벤트 타입 확인**
   ```
   Top 20 이벤트 타입 출력 → DATABASE 관련 타입 찾기
   ```

2. **로그 설정 파일 검토**
   ```yaml
   # adb_media_camera_worker_config.yaml
   - eventType: "DATABASE_EVENT"  # ← 이 값이 맞는지 확인
   
   # adb_media_camera_config.yaml
   - eventType: "CAMERA_EVENT"    # ← DATABASE_INSERT로 변경 필요?
   ```

3. **필요시 CameraCaptureDetector 수정**
   ```csharp
   // PrimaryEvidenceTypes에 실제 파싱된 타입 추가
   private static readonly HashSet<string> PrimaryEvidenceTypes = new()
   {
       LogEventTypes.DATABASE_INSERT,
       LogEventTypes.DATABASE_EVENT,
       LogEventTypes.MEDIA_INSERT_END,
       LogEventTypes.CAMERA_EVENT,      // ← 추가?
       LogEventTypes.MEDIA_INSERT,      // ← 추가?
   };
   ```

#### 💡 Option B: 세션 시간 범위 확장
```csharp
// FilterSessionEvents() 수정
var bufferTime = TimeSpan.FromSeconds(5);  // ±5초 버퍼
var startTime = session.StartTime - bufferTime;
var endTime = (session.EndTime ?? DateTime.MaxValue) + bufferTime;
```

---

## 📋 다음 단계: 실행 계획

### Step 1: 디버깅 정보 추가 (최우선)

#### 1.1. 이벤트 타입 통계 확대
```csharp
// EndToEndAnalysisTests.cs - ParseSampleLogsAsync()
var eventTypeCounts = allEvents
    .GroupBy(e => e.EventType)
    .OrderByDescending(g => g.Count())
    .Take(20);  // ← 15 → 20으로 변경
```

#### 1.2. DATABASE 관련 이벤트 상세 출력
```csharp
// 추가
_output.WriteLine($"\n🔍 DATABASE 관련 이벤트:");
var dbEvents = allEvents.Where(e => 
    e.EventType.Contains("DATABASE") || 
    e.EventType.Contains("MEDIA_INSERT")).ToList();
_output.WriteLine($"  총 {dbEvents.Count}개");
foreach (var evt in dbEvents.Take(5))
{
    _output.WriteLine($"  - {evt.EventType}: {evt.Timestamp:HH:mm:ss}");
}
```

#### 1.3. 세션 상세 정보 출력
```csharp
// 추가 - Sample2_AnalysisResult_MatchesGroundTruth()
_output.WriteLine($"\n📦 감지된 세션 상세:");
foreach (var session in result.Sessions.OrderBy(s => s.StartTime))
{
    _output.WriteLine($"  Session {session.SessionId}:");
    _output.WriteLine($"    Package: {session.PackageName}");
    _output.WriteLine($"    Time: {session.StartTime:HH:mm:ss.fff} ~ {session.EndTime?.ToString("HH:mm:ss.fff") ?? "N/A"}");
    _output.WriteLine($"    IsIncomplete: {session.IsIncomplete} ({session.IncompleteReason})");
    _output.WriteLine($"    Confidence: {session.ConfidenceScore:F3}");
    _output.WriteLine($"    SourceLogs: {string.Join(", ", session.SourceLogTypes)}");
}
```

#### 1.4. 촬영 감지 디버깅
```csharp
// CameraCaptureDetector.cs - DetectPrimaryEvidenceCaptures()
_logger.LogInformation(
    "주 증거 이벤트 {Count}개 발견 (Session={SessionId})",
    primaryEvidences.Count, session.SessionId);

// 주 증거가 0개인 경우 경고
if (primaryEvidences.Count == 0)
{
    _logger.LogWarning(
        "⚠️  세션에 주 증거 없음: SessionId={SessionId}, Package={Package}, " +
        "Time={Start}~{End}, TotalEvents={EventCount}",
        session.SessionId, session.PackageName, 
        session.StartTime, session.EndTime, sessionEvents.Count);
}
```

---

### Step 2: 테스트 재실행 및 분석

```bash
dotnet test --filter "FullyQualifiedName~Sample2" --logger "console;verbosity=detailed"
```

**확인 사항**:
1. ✅ DATABASE 관련 이벤트 존재 여부 및 개수
2. ✅ 8개 세션의 PackageName, StartTime, EndTime, IsIncomplete
3. ✅ 각 세션의 주 증거 이벤트 개수
4. ✅ Ground Truth와 실제 세션 시간 비교

---

### Step 3: 코드 수정

#### 수정 A: 고아 DISCONNECT 무시 (확정)
```csharp
// CameraSessionDetector.cs - ExtractSessionsFromEventSequence()
else
{
    // 시작 없이 종료 (고아 DISCONNECT)
    _logger.LogDebug("고아 DISCONNECT 무시: EventId={EventId}", evt.EventId);
    // sessions.Add(...) 제거
}
```

#### 수정 B: 주 증거 타입 추가 (조건부)
```csharp
// CameraCaptureDetector.cs - PrimaryEvidenceTypes
// Step 2 분석 결과에 따라 실제 파싱된 타입 추가
```

#### 수정 C: 세션 시간 버퍼 (선택)
```csharp
// CameraCaptureDetector.cs - FilterSessionEvents()
// 필요시 ±5초 버퍼 추가
```

---

### Step 4: 재검증

1. **테스트 재실행**
   ```
   Expected: 5 sessions, 3 captures
   Actual: ? sessions, ? captures
   ```

2. **Ground Truth 비교**
   - 세션 시간 매칭
   - 촬영 시각 매칭

3. **정확도 측정**
   - Precision, Recall, F1-Score

---

## 📝 요약

### 세션 과다 감지 원인
1. ✅ **확정**: 고아 DISCONNECT (14개) → MissingStart 세션 생성
2. ✅ **확정**: 불완전 세션 병합 불가
3. ⚠️  **추정**: DISCONNECT 과다 (24개 vs CONNECT 10개)

### 촬영 미감지 원인
1. 🔴 **최우선 의심**: 주 증거 이벤트 (`DATABASE_INSERT` 등) 부재
2. ⚠️  **가능성**: 세션 시간 범위 오류로 필터링
3. ⚙️  **낮음**: 경로 패턴 오작동
4. ⚙️  **매우 낮음**: 신뢰도 미달

### 즉시 수행할 작업
1. ✅ 테스트 코드에 디버깅 출력 추가
2. ✅ 테스트 재실행하여 실제 데이터 확인
3. ✅ 분석 결과에 따라 코드 수정
4. ✅ 재검증

---

**다음 문서**: `Phase8_Debugging_Results.md` (Step 1-2 실행 후 작성)  
**상태**: 코드 분석 완료, 디버깅 준비 완료
