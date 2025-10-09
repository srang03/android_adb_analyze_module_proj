# Phase 8 분석 계획서 - 세션/촬영 감지 로직 분석

## 현재 문제 요약

**2차 샘플 테스트 결과 (2025-10-05)**
```
✅ 파싱 성공: 2,129개 이벤트
✅ 이벤트 타입 인식: CAMERA_CONNECT 10개, CAMERA_DISCONNECT 24개
✅ 처리 시간: 1.278초
✅ 중복 제거: 1,666개

❌ 세션 감지: 8개 (예상 5개) ← 60% 과다 감지
❌ 촬영 감지: 0개 (예상 3개) ← 100% 미감지
```

---

## 분석 대상 코드

### 1️⃣ 세션 과다 감지 원인 분석

#### 분석 파일: `CameraSessionDetector.cs`
**위치**: `AndroidAdbAnalyze.Analysis/Services/Sessions/CameraSessionDetector.cs`

#### 핵심 분석 포인트

**1.1. ExtractRawSessions() 메서드**
- **위치**: 라인 120-180 (추정)
- **책임**: CAMERA_CONNECT/DISCONNECT 페어링하여 원시 세션 추출
- **분석 질문**:
  - ❓ CAMERA_DISCONNECT 24개 vs CAMERA_CONNECT 10개 → 어떻게 페어링하는가?
  - ❓ 불완전 세션 (MissingStart/MissingEnd)은 어떻게 처리하는가?
  - ❓ 동일 패키지의 여러 CONNECT/DISCONNECT가 어떻게 매칭되는가?

**1.2. MergeSessions() 메서드**
- **위치**: 라인 230-310 (추정)
- **책임**: 시간 겹침 80% 이상인 세션 병합
- **분석 질문**:
  - ❓ MinOverlapRatio 0.8 (80%)이 너무 높은가?
  - ❓ 병합 로직이 올바르게 작동하는가?
  - ❓ 신뢰도 기반 우선순위가 올바른가?

**1.3. HandleIncompleteSessions() 메서드**
- **위치**: 라인 350-450 (추정)
- **책임**: 불완전 세션의 시작/종료 시간 추정
- **분석 질문**:
  - ❓ 불완전 세션이 과도하게 생성되는가?
  - ❓ 추정 로직이 올바른가?

**1.4. 신뢰도 필터링**
- **위치**: 라인 68-70 (DetectSessions 메서드)
- **코드**:
  ```csharp
  var finalSessions = completedSessions
      .Where(s => s.ConfidenceScore >= options.MinConfidenceThreshold)
      .ToList();
  ```
- **분석 질문**:
  - ❓ MinConfidenceThreshold 기본값은 얼마인가?
  - ❓ 8개 세션의 신뢰도는 얼마인가?

#### 분석 방법
1. **중복 제거 후 이벤트 확인**
   - 중복 제거 후 CAMERA_CONNECT/DISCONNECT 수 재확인
   - Ground Truth와 매칭 (2차 샘플: 5 세션)

2. **세션 생성 과정 로깅**
   - ExtractRawSessions()에서 생성된 원시 세션 수
   - MergeSessions()에서 병합 후 세션 수
   - HandleIncompleteSessions()에서 최종 세션 수

3. **각 세션의 상세 정보 확인**
   - PackageName, StartTime, EndTime, IsIncomplete, ConfidenceScore
   - Ground Truth와 비교하여 어떤 세션이 과다 생성되었는지 파악

---

### 2️⃣ 촬영 미감지 원인 분석

#### 분석 파일: `CameraCaptureDetector.cs`
**위치**: `AndroidAdbAnalyze.Analysis/Services/Captures/CameraCaptureDetector.cs`

#### 핵심 분석 포인트

**2.1. 주 증거 이벤트 존재 여부**
- **주 증거 타입** (라인 20-25):
  ```csharp
  private static readonly HashSet<string> PrimaryEvidenceTypes = new()
  {
      LogEventTypes.DATABASE_INSERT,      // MediaProvider DB 삽입
      LogEventTypes.DATABASE_EVENT,       // 일반 DB 이벤트
      LogEventTypes.MEDIA_INSERT_END      // 미디어 삽입 완료
  };
  ```
- **분석 질문**:
  - ❓ 2차 샘플 로그에 `DATABASE_INSERT`, `DATABASE_EVENT`, `MEDIA_INSERT_END` 이벤트가 있는가?
  - ❓ 있다면 개수는 몇 개인가? (예상: 3개)
  - ❓ 없다면 어떤 이벤트가 대신 파싱되었는가?

**2.2. DetectPrimaryEvidenceCaptures() 메서드**
- **위치**: 라인 98-169
- **로직**:
  1. 세션 내 주 증거 이벤트 조회
  2. 경로 패턴 검증 (스크린샷/다운로드 제외)
  3. 보조 증거 수집 (±30초 윈도우)
  4. 신뢰도 계산
  5. 최소 신뢰도 확인
- **분석 질문**:
  - ❓ 주 증거 이벤트가 세션 시간 범위 내에 있는가?
  - ❓ 경로 패턴 검증에서 잘못 제외되는가?
  - ❓ 신뢰도가 MinConfidenceThreshold보다 낮은가?

**2.3. FilterSessionEvents() 메서드**
- **위치**: 라인 81-93
- **로직**:
  ```csharp
  return events
      .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
      .OrderBy(e => e.Timestamp)
      .ToList();
  ```
- **분석 질문**:
  - ❓ 세션 시간 범위가 올바르게 설정되었는가?
  - ❓ Ground Truth 촬영 시각이 세션 범위 내에 있는가?
    - 세션 2: 21:59:08~18, 촬영 21:59:13 ✅
    - 세션 4: 22:02:17~32, 촬영 22:02:27 ✅
    - 세션 5: 22:03:58~22:04:08, 촬영 22:04:03 ✅

**2.4. IsExcludedByPathPattern() 메서드**
- **위치**: 라인 191-210 (추정)
- **책임**: 스크린샷/다운로드 경로 패턴으로 제외
- **분석 질문**:
  - ❓ ScreenshotPathPatterns, DownloadPathPatterns 기본값은?
  - ❓ 촬영 이벤트가 잘못 제외되는가?

#### 분석 방법
1. **이벤트 타입 통계 재확인**
   - 테스트 디버깅 출력에서 `DATABASE_INSERT`, `DATABASE_EVENT`, `MEDIA_INSERT_END` 개수 확인
   - 없다면 대체 이벤트 타입 파악

2. **세션별 주 증거 조회**
   - 각 세션 내 주 증거 이벤트 개수 로깅
   - Ground Truth와 매칭

3. **신뢰도 계산 검증**
   - 주 증거 + 보조 증거의 신뢰도 계산 결과
   - MinConfidenceThreshold와 비교

---

## 분석 순서

### Step 1: 테스트 로그 상세 분석 (현재 단계)
1. **테스트 코드에 디버깅 출력 추가**
   - 중복 제거 후 이벤트 타입별 통계 (Top 20)
   - `DATABASE_INSERT`, `DATABASE_EVENT`, `MEDIA_INSERT_END` 개수 확인

2. **세션 상세 정보 출력**
   - 각 세션의 PackageName, StartTime, EndTime, IsIncomplete, ConfidenceScore
   - 세션 내 이벤트 개수

3. **촬영 감지 디버깅**
   - 각 세션에 대해 DetectCaptures() 호출 시 로깅
   - 주 증거 이벤트 개수, 경로 패턴 제외 개수, 신뢰도 미달 개수

### Step 2: CameraSessionDetector 분석
1. **ExtractRawSessions() 로직 검증**
   - CAMERA_CONNECT/DISCONNECT 페어링 알고리즘 확인
   - 불완전 세션 생성 조건 확인

2. **MergeSessions() 로직 검증**
   - 시간 겹침 계산 방식 확인
   - 병합 조건 (MinOverlapRatio 0.8) 적절성 평가

3. **필요시 임계값 조정**
   - MinOverlapRatio 조정 (0.8 → 0.5?)
   - MaxSessionGap 조정
   - MinConfidenceThreshold 조정

### Step 3: CameraCaptureDetector 분석
1. **주 증거 이벤트 존재 여부 확인**
   - 파싱 설정 파일 검토
   - 로그 파일에 실제 존재하는지 확인

2. **경로 패턴 검증 확인**
   - ScreenshotPathPatterns, DownloadPathPatterns 기본값 확인
   - 촬영 이벤트가 잘못 제외되는지 확인

3. **필요시 로직 수정**
   - 주 증거 타입 추가
   - 경로 패턴 조정
   - EventCorrelationWindow 조정

### Step 4: Ground Truth 검증
1. **2차 샘플 Ground Truth와 비교**
   - 세션 5개 vs 실제 감지
   - 촬영 3개 vs 실제 감지

2. **정확도 측정**
   - Precision, Recall, F1-Score

---

## 필요한 추가 정보

### 1. AnalysisOptions 기본값
- `MinConfidenceThreshold`: ?
- `MinOverlapRatio`: ?
- `MaxSessionGap`: ?
- `EventCorrelationWindow`: ?
- `ScreenshotPathPatterns`: ?
- `DownloadPathPatterns`: ?
- `EnableIncompleteSessionHandling`: ?

### 2. 2차 샘플 로그 파일 목록
- `audio.log`
- `media_camera_worker.log`
- `media_camera.log`
- `media_metrics.log`
- `usagestats.log`
- `vibrator_manager.log`
- `activity.log`

### 3. Ground Truth 상세 (2차 샘플)
**세션**:
1. 21:58:03~09 (촬영 없음) - 패키지: ?
2. 21:59:08~18 (촬영 1회, 21:59:13) - 패키지: ?
3. 22:01:05~10 (촬영 없음) - 패키지: ?
4. 22:02:17~32 (촬영 1회, 22:02:27) - 패키지: ?
5. 22:03:58~22:04:08 (촬영 1회, 22:04:03) - 패키지: ?

**앨범 전송** (오탐 방지 대상):
- 22:05:53

---

## 다음 작업

1. ✅ `CoreAnalysis_DevelopmentPlan.md` 업데이트 완료
2. ✅ `Phase8_Analysis_Plan.md` 작성 완료
3. 🔄 **현재**: 분석 대상 코드 정확히 파악
4. ⏭️ **다음**: Step 1 실행 - 테스트 로그 상세 분석

---

**작성일**: 2025-10-05  
**작성자**: AI Development Team  
**목적**: Phase 8 세션/촬영 감지 로직 분석 및 조정
