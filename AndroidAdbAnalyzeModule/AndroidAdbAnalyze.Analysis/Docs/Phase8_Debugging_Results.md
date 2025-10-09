# Phase 8 디버깅 결과 리포트

**실행일**: 2025-10-05  
**테스트**: Sample2_AnalysisResult_MatchesGroundTruth  
**목적**: 세션 과다 감지(8개 vs 5개) 및 촬영 미감지(0개 vs 3개) 원인 규명

---

## 📊 테스트 실행 결과

### 파싱 결과
```
✅ 총 이벤트: 2,129개
✅ 처리 시간: 1.261초
✅ 중복 제거: 1,666개

파일별 파싱:
- audio.log: 29 events
- media_camera_worker.log: 25 events  
- media_camera.log: 10 events
- media_metrics.log: 74 events
- usagestats.log: 1,939 events
- vibrator_manager.log: 36 events
- activity.log: 16 events
```

### Top 20 이벤트 타입
```
1. STANDBY_BUCKET_CHANGED: 1,161개
2. ACTIVITY_LIFECYCLE: 698개
3. MEDIA_EXTRACTOR: 39개
4. FOREGROUND_SERVICE: 36개
5. VIBRATION_EVENT: 36개
6. AUDIO_TRACK: 35개
7. CAMERA_DISCONNECT: 24개  ⚠️
8. NOTIFICATION: 18개
9. SCREEN_STATE: 16개
10. PLAYER_CREATED: 14개
11. URI_PERMISSION_GRANT: 13개
12. PLAYER_RELEASED: 11개
13. CAMERA_CONNECT: 10개  ⚠️
14. KEYGUARD: 10개
15. PLAYER_EVENT: 4개
16. URI_PERMISSION_REVOKE: 3개
17. MEDIA_INSERT_START: 1개  ⚠️
```

### 🔴 DATABASE 관련 이벤트 (주 증거)
```
총 1개만 발견:
- MEDIA_INSERT_START: 1개 (12:59:15.705)

❌ 부재한 이벤트:
- DATABASE_INSERT: 0개
- DATABASE_EVENT: 0개
- MEDIA_INSERT_END: 0개
```

### 🎥 카메라 이벤트 분석
```
CAMERA_CONNECT: 10개
CAMERA_DISCONNECT: 24개
불균형: 14개 (58% 미매칭)
```

---

## 🔍 Part 1: 세션 과다 감지 원인 (8개 vs 예상 5개)

### 감지된 8개 세션 상세

#### 세션 #1: android.system
```
Time: 12:54:14.691 ~ 12:54:14.691
Duration: 0.0초
Status: 완전
Confidence: 0.400 (40%)
SourceLogs: camera_event
Captures: 0개
```
**분석**: 
- ⚠️  Duration 0초 → 시작==종료
- ❓ `android.system` 패키지는 정상 카메라 세션이 아님 (시스템 프로세스)

#### 세션 #2: com.sec.android.app.camera
```
Time: 12:58:05.000 ~ 12:58:10.931
Duration: 5.9초
Status: 완전
Confidence: 1.000 (100%)
SourceLogs: camera_events, camera_event
Captures: 0개
```
**분석**: 
- ✅ Ground Truth 세션 1 (21:58:03~09, 약 6초)과 일치 가능
- ⏰ 시간 차이: 로그 타임스탬프가 UTC 변환 또는 다른 시간대?

#### 세션 #3: com.sec.android.app.camera
```
Time: 12:59:09.000 ~ 12:59:20.000
Duration: 11.0초
Status: 완전
Confidence: 0.800 (80%)
SourceLogs: camera_events
Captures: 0개
```

#### 세션 #4: com.sec.android.app.camera
```
Time: 12:59:09.763 ~ 13:01:06.000
Duration: 116.2초
Status: 완전
Confidence: 0.400 (40%)
SourceLogs: camera_event
Captures: 0개
```
**분석**: 
- ⚠️  세션 #3과 시작 시각 거의 동일 (12:59:09)
- ⚠️  세션 #4는 116초(약 2분) 지속 → Ground Truth와 불일치
- ❌ 이 두 세션은 병합되어야 하나 병합되지 않음

#### 세션 #5: com.sec.android.app.camera
```
Time: 13:01:07.000 ~ 13:01:12.390
Duration: 5.4초
Status: 완전
Confidence: 1.000 (100%)
SourceLogs: camera_events, camera_event
Captures: 0개
```
**분석**: 
- ✅ Ground Truth 세션 3 (22:01:05~10, 약 5초)과 일치 가능

#### 세션 #6: com.sec.android.app.camera
```
Time: 13:02:24.000 ~ 13:02:33.811
Duration: 9.8초
Status: 완전
Confidence: 1.000 (100%)
SourceLogs: camera_events, camera_event
Captures: 0개
```
**분석**: 
- ✅ Ground Truth 세션 4 (22:02:17~32, 약 15초)와 일치 가능

#### 세션 #7: com.sec.android.app.camera
```
Time: 13:04:00.000 ~ 13:04:10.000
Duration: 10.0초
Status: 완전
Confidence: 0.800 (80%)
SourceLogs: camera_events
Captures: 0개
```

#### 세션 #8: com.sec.android.app.camera
```
Time: 13:04:00.761 ~ 13:04:07.783
Duration: 7.0초
Status: 완전
Confidence: 0.400 (40%)
SourceLogs: camera_event
Captures: 0개
```
**분석**: 
- ⚠️  세션 #7과 시작 시각 거의 동일 (13:04:00)
- ⚠️  세션 #7은 camera_events, 세션 #8은 camera_event (다른 로그 소스)
- ❌ 이 두 세션은 병합되어야 하나 병합되지 않음
- ✅ Ground Truth 세션 5 (22:03:58~22:04:08, 약 10초)와 일치 가능

---

### 🎯 세션 과다 감지 근본 원인

#### 원인 1: 로그 소스별 중복 세션 생성 ✅ **확정**
```
분석:
- camera_events (복수형): media_camera_worker.log 또는 media_camera.log
- camera_event (단수형): 다른 로그 파일

결과:
- 세션 #3 (camera_events) + 세션 #4 (camera_event) → 동일 시간대 중복
- 세션 #7 (camera_events) + 세션 #8 (camera_event) → 동일 시간대 중복
```

**해결 방안**:
- `SourceSection` 값이 다르면 별도 세션으로 인식됨
- `ExtractRawSessions()` 메서드가 `SourceSection`별로 그룹화 (라인 127-130)
- 병합 로직 (`MergeSessions()`)이 동일 패키지만 병합 (라인 282-287)

```csharp
// CameraSessionDetector.cs 라인 280-281
var sessionsByPackage = sessions
    .GroupBy(s => s.PackageName)  // ← SourceSection으로 그룹화되지 않음!
```

#### 원인 2: 불균형 CAMERA_DISCONNECT (14개 미매칭) ✅ **확정**
```
CAMERA_CONNECT: 10개
CAMERA_DISCONNECT: 24개
→ 14개의 고아 DISCONNECT

예상:
- `ExtractSessionsFromEventSequence()` 메서드가 고아 DISCONNECT를 
  별도 세션(MissingStart)으로 생성 가능
```

**검증 필요**:
- 현재 모든 8개 세션이 `Status: 완전`으로 표시됨
- 고아 DISCONNECT가 세션으로 생성되지 않은 것으로 보임
- 하지만 로그 소스별 중복이 주요 원인

#### 원인 3: 세션 병합 실패 ⚠️  **부분 확정**
```
세션 #3 (12:59:09~12:59:20) + 세션 #4 (12:59:09~13:01:06)
→ MinOverlapRatio 0.8 (80%) 기준:
  - Overlap: 11초 (12:59:09~12:59:20)
  - Min Duration: 11초 (세션 #3)
  - Ratio: 11 / 11 = 1.0 (100%) ✅ → 병합되어야 함!

세션 #7 (13:04:00~13:04:10) + 세션 #8 (13:04:00~13:04:07)
→ MinOverlapRatio 0.8 기준:
  - Overlap: 7초 (13:04:00~13:04:07)
  - Min Duration: 7초 (세션 #8)
  - Ratio: 7 / 7 = 1.0 (100%) ✅ → 병합되어야 함!
```

**원인 추정**:
- `MergeSessions()` 메서드가 패키지별로 그룹화한 후 순차 병합
- 하지만 `SourceSection`이 다르면 다른 그룹으로 분류될 가능성
- 또는 `SourceLogTypes` 비교 로직 부재

---

## 🔍 Part 2: 촬영 미감지 원인 (0개 vs 예상 3개)

### 🔴 원인 1: 주 증거 이벤트 부재 ✅ **확정**

**필요한 주 증거** (`CameraCaptureDetector.cs` 라인 20-25):
```csharp
DATABASE_INSERT       // MediaProvider DB 삽입
DATABASE_EVENT        // 일반 DB 이벤트
MEDIA_INSERT_END      // 미디어 삽입 완료
```

**실제 파싱 결과**:
```
DATABASE_INSERT: 0개 ❌
DATABASE_EVENT: 0개 ❌
MEDIA_INSERT_END: 0개 ❌

유사 이벤트:
MEDIA_INSERT_START: 1개 (12:59:15.705)
```

**결론**:
- ✅ 주 증거 이벤트가 거의 없음 (99.7% 부재)
- ❌ `CameraCaptureDetector`가 촬영을 감지할 수 없음
- ⚠️  `MEDIA_INSERT_START`는 주 증거 타입에 포함되지 않음

---

### 🔴 원인 2: 파싱 설정 오류 추정

#### 가능성 A: 설정 파일에 EventType 미정의
```
확인 필요:
- media_camera.log → DATABASE_INSERT 정의?
- media_camera_worker.log → DATABASE_EVENT 정의?
- media_metrics.log → MEDIA_INSERT_END 정의?
```

#### 가능성 B: 로그 파일에 실제로 없음
```
Ground Truth:
- 촬영 1: 21:59:13 (세션 2 내)
- 촬영 2: 22:02:27 (세션 4 내)
- 촬영 3: 22:04:03 (세션 5 내)

하지만:
- DATABASE 관련 이벤트 1개만 발견
- 촬영 시각과 매칭되는 이벤트 없음
```

#### 가능성 C: 이벤트 타입명이 다름
```
실제 파싱:
- MEDIA_INSERT_START (12:59:15.705)

추정:
- MEDIA_INSERT_START가 촬영의 시작 이벤트?
- MEDIA_INSERT_END는 파싱되지 않았거나 없음?
```

---

## 📋 핵심 발견 요약

### ✅ 확정된 문제

#### 1. 세션 과다 감지
| 문제 | 원인 | 영향 |
|------|------|------|
| 로그 소스별 중복 세션 | `SourceSection`별로 세션 생성, 병합 안 됨 | +3개 세션 (세션 3-4, 7-8 중복) |
| android.system 세션 | 시스템 패키지 필터링 안 됨 | +1개 세션 (세션 1) |
| CAMERA_DISCONNECT 불균형 | 24개 vs CONNECT 10개 (14개 미매칭) | 영향 미확인 (불완전 세션 없음) |

**총 과다 감지**: 8개 - 4개 중복/불필요 = 4개 (거의 Ground Truth 5개와 일치 가능)

#### 2. 촬영 미감지
| 문제 | 원인 | 영향 |
|------|------|------|
| 주 증거 부재 | DATABASE_INSERT/EVENT/MEDIA_INSERT_END 없음 | 100% 미감지 |
| 파싱 설정 오류 | EventType 미정의 또는 로그 부재 | 주 증거 생성 실패 |

---

## 🎯 즉시 수행할 수정 작업

### 수정 1: 로그 소스 통합 (세션 중복 방지)

**Option A: SourceSection 무시하고 병합**
```csharp
// CameraSessionDetector.cs - ExtractRawSessions() 수정
// 라인 127-130
var eventsBySource = events
    .Where(e => e.Attributes.ContainsKey("package"))
    .GroupBy(e => e.Attributes["package"]?.ToString() ?? string.Empty)  // ← SourceSection 제거
    .ToList();

// SourceSection별 그룹화 제거 → 패키지별로만 그룹화
```

**Option B: 병합 로직 개선**
```csharp
// CameraSessionDetector.cs - MergeSessions() 수정
// 병합 시 SourceSection이 다라도 시간 겹침이 높으면 병합
```

**권장**: Option A (단순하고 효과적)

---

### 수정 2: android.system 필터링

```csharp
// CameraSessionDetector.cs - ApplyPackageFilters() 또는 DetectSessions()
// 시스템 패키지 필터링
var systemPackages = new[] { "android.system", "com.android.systemui" };
sessions = sessions
    .Where(s => !systemPackages.Contains(s.PackageName))
    .ToList();
```

---

### 수정 3: 주 증거 이벤트 파싱 확인

#### Step 1: 설정 파일 검토
```bash
# 확인 대상
adb_media_camera_config.yaml
adb_media_camera_worker_config.yaml
adb_media_metrics_config.yaml
```

**확인 사항**:
- `eventType: "DATABASE_INSERT"` 정의 여부
- `eventType: "DATABASE_EVENT"` 정의 여부
- `eventType: "MEDIA_INSERT_END"` 정의 여부

#### Step 2: MEDIA_INSERT_START를 주 증거로 추가 (임시)
```csharp
// CameraCaptureDetector.cs - PrimaryEvidenceTypes
private static readonly HashSet<string> PrimaryEvidenceTypes = new()
{
    LogEventTypes.DATABASE_INSERT,
    LogEventTypes.DATABASE_EVENT,
    LogEventTypes.MEDIA_INSERT_END,
    "MEDIA_INSERT_START"  // ← 임시 추가
};
```

---

## 🔄 다음 단계

### Step 1: 코드 수정 적용 (우선순위 순)
1. ✅ 로그 소스 통합 (ExtractRawSessions 수정)
2. ✅ android.system 필터링
3. ⚠️  MEDIA_INSERT_START를 주 증거로 임시 추가

### Step 2: 테스트 재실행
```bash
dotnet test --filter "Sample2_AnalysisResult_MatchesGroundTruth"
```

**예상 결과**:
- 세션: 5-6개 (8개 → 4-5개 감소)
- 촬영: 1-3개 (0개 → 1-3개 증가, MEDIA_INSERT_START 기준)

### Step 3: 설정 파일 정밀 분석
- 각 로그 설정 파일의 EventType 정의 확인
- DATABASE 관련 이벤트 파싱 규칙 검토
- 필요시 새로운 파싱 패턴 추가

### Step 4: Ground Truth 타임스탬프 검증
- 로그 시각 (12:58:05) vs Ground Truth (21:58:03)
- UTC 변환 또는 시간대 차이 확인

---

**다음 문서**: `Phase8_Code_Fix.md` (수정 작업 수행 후 작성)  
**상태**: 디버깅 완료, 수정 작업 대기 중
