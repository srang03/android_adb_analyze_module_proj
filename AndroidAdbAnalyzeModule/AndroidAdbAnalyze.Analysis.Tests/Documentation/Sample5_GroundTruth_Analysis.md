# 5차 샘플 Ground Truth 분석 결과

## 📋 개요

- **분석 날짜**: 2025-10-08
- **로그 일자**: 2025-10-07
- **시나리오 시간 범위**: 23:13:00 ~ 23:30:00
- **분석 대상 로그**: audio.log, media_camera.log, activity.log, usagestats.log, vibrator_manager.log

---

## 📊 세션 및 촬영 Ground Truth

### 전체 요약

| 항목 | 개수 | 비고 |
|-----|------|------|
| **총 세션** | **11개** | ✅ 모두 정상 탐지 |
| **총 촬영** | **6개** | 시나리오 기준 |
| **현재 탐지** | **9개** | ⚠️ +3개 초과 (중복 문제) |

---

## 🎯 세션별 상세 분석

### 1. 기본 카메라 (com.sec.android.app.camera) - 5개 세션

| # | 세션 시간 | 촬영 | PLAYER_EVENT | 비고 |
|---|---------|------|--------------|------|
| 1 | 23:13:36 ~ 23:13:41 | ❌ | piid:415 (생성만) | 촬영 없음 |
| **2** | **23:14:16 ~ 23:14:26** | **✅ 1회** | **piid:431** | ⚠️ **중복 문제** |
| 3 | 23:15:42 ~ 23:15:46 | ❌ | piid:447 (생성만) | 촬영 없음 |
| 4 | 23:16:35 ~ 23:16:44 | ✅ 1회 | piid:463 (started 1개) | 정상 |
| 5 | 23:20:11 ~ 23:20:20 | ✅ 1회 | piid:479 (started 1개) | 정상 |

**기본 카메라 총 촬영**: **3회** (시나리오: 1회, 실제 로그: 3회)

#### 세션 2 중복 문제 상세
```
23:14:20:947  player piid:431 event:started  ← 1차 촬영 (정상)
23:14:24:391  player piid:431 event:started  ← 2차 이벤트 (4초 후, 동일 piid)
```
- **원인**: 동일 `piid`에서 2개의 `started` 이벤트 발생
- **예상 동작**: `DeduplicatePlayerEventsByPiid`가 첫 번째만 처리하여 **1개로 탐지**
- **실제 문제**: 현재 로직이 세션 내에서는 정상 동작하지만, 테스트 검증 필요

---

### 2. Telegram (org.telegram.messenger) - 4개 세션

| # | 세션 시간 | 촬영 | PLAYER_EVENT | 비고 |
|---|---------|------|--------------|------|
| 6 | 23:22:04 ~ 23:22:16 | ❌ | - | 촬영 없음 |
| 7 | 23:22:59 ~ 23:23:41 | ✅ 1회 | piid:495 (1차) | 정상 |
| **8** | **23:26:19 ~ 23:26:34** | **✅ 1회** | **piid:495 (2,3차)** | ⚠️ **중복 문제** |
| **9** | **23:27:52 ~ 23:27:57** | **❌** | **piid:495 (4차)** | ⚠️ **오탐지** |

**Telegram 총 촬영**: **2회** (시나리오 기준)  
**현재 탐지**: **3회** (오탐지 +1)

#### piid:495 상세 분석
```
23:26:34:792  new player piid:495        ← PLAYER_CREATED
23:26:34:793  player piid:495 event:started  ← 1ms 후 (정상)
23:26:34:796  player piid:495 event:started  ← 3ms 후 (중복!)
23:28:02:664  player piid:495 event:started  ← 88초 후, releasing 없이 재사용
```

**세션 배분**:
- **세션 8** (23:26:19 ~ 23:26:44): 23:26:34의 2개 이벤트 포함
  - `DeduplicatePlayerEventsByPiid`로 1개만 탐지 예상
- **세션 9** (23:27:52 ~ 23:28:07): 23:28:02의 1개 이벤트 포함
  - 별도 세션이므로 1개 탐지됨 (⚠️ **오탐지** - 실제 촬영 없음)

**문제점**:
1. Telegram이 플레이어 인스턴스를 `releasing` 없이 재사용
2. 세션 9에는 실제 촬영이 없었으나 `piid:495`의 재사용으로 인해 오탐지
3. 세션 9의 PLAYER_EVENT는 이전 세션의 잔존 이벤트일 가능성

---

### 3. Silent Camera (com.peace.SilentCamera) - 2개 세션

| # | 세션 시간 | 촬영 | 증거 | 비고 |
|---|---------|------|------|------|
| 10 | 23:28:38 ~ 23:28:43 | ❌ | - | 촬영 없음 |
| 11 | 23:29:36 ~ 23:29:46 | ✅ 1회 | SILENT_CAMERA_CAPTURE | 정상 |

**Silent Camera 총 촬영**: **1회** ✅

---

## 🔍 중복 감지 문제 요약

### 문제 1: 기본 카메라 piid:431 (세션 2)
- **증상**: 동일 `piid`에서 2개 `started` 이벤트 (4초 간격)
- **현재 동작**: `DeduplicatePlayerEventsByPiid`가 첫 번째만 처리 → **1개 탐지 예상**
- **검증 필요**: 실제로 1개만 탐지되는지 디버깅 로그로 확인

### 문제 2: Telegram piid:495 (세션 8, 9)
- **증상**: 
  1. 세션 8에서 2개 `started` 이벤트 (3ms 간격) → **1개로 정상 처리**
  2. 세션 9에서 1개 `started` 이벤트 (88초 후) → **1개 오탐지**
- **원인**: Telegram이 플레이어 인스턴스를 `releasing` 없이 재사용
- **해결 방안**: 
  - Option A: 세션 경계를 넘는 `piid` 재사용 감지 및 필터링
  - Option B: Telegram 전용 전략에서 `VIBRATION_EVENT` 우선 사용

### 문제 3: 기본 카메라 추가 촬영 (세션 4, 5)
- **증상**: 시나리오에는 1회 촬영이지만, 실제 로그에서는 **3회 촬영** 감지
- **원인**: 시나리오 데이터 시트와 실제 로그 불일치
- **결론**: **실제 로그가 Ground Truth**이므로, 기본 카메라는 **3회 촬영**으로 수정

---

## ✅ 최종 Ground Truth (로그 기반)

| 앱 | 세션 | 실제 촬영 | 오탐지 | 정상 탐지 목표 |
|---|------|----------|--------|--------------|
| 기본 카메라 | 5 | **3회** | 0 | 3회 |
| Telegram | 4 | **2회** | 1 | 2회 |
| Silent Camera | 2 | **1회** | 0 | 1회 |
| **합계** | **11** | **6회** | **1회** | **6회** |

---

## 🎯 수정 계획

### 1. 디버깅 로그 추가
- `BasePatternStrategy.DetectCaptures`에 디버깅 로그 추가
- `DeduplicatePlayerEventsByPiid` 실행 결과 로그 출력
- 각 세션별 탐지된 촬영 이벤트 상세 로그

### 2. piid 중복 제거 개선
- 현재 로직 검증: 세션 내 중복 제거가 정상 동작하는지 확인
- Telegram `piid` 재사용 문제 해결 방안 검토

### 3. 테스트 Ground Truth 업데이트
```csharp
// 현재
result.Statistics.TotalSessions.Should().BeInRange(10, 12, "11개 세션 예상");
result.Statistics.TotalCaptureEvents.Should().BeInRange(5, 7, "6개 촬영 예상");

// 수정 후 (중복 제거 완료 시)
result.Statistics.TotalSessions.Should().Be(11, "11개 세션");
result.Statistics.TotalCaptureEvents.Should().Be(6, "6개 촬영 (오탐지 제거 후)");
```

### 4. 시간 범위 설정 정확화
```csharp
// 5차 샘플 시나리오 시간 범위로 수정
var startTime = new DateTime(2025, 10, 7, 23, 13, 0);
var endTime = new DateTime(2025, 10, 7, 23, 30, 0);
```

---

## 📝 추가 고려사항

1. **Telegram 전략 개선**:
   - `VIBRATION_EVENT` (usage: TOUCH)를 주 증거로 우선 사용
   - `PLAYER_EVENT`를 보조 증거로만 활용

2. **piid 재사용 감지**:
   - `PLAYER_CREATED` 없이 발생하는 `PLAYER_EVENT`는 이전 세션의 잔존 이벤트로 처리
   - 세션 시작 시간 이전에 생성된 `piid`는 필터링

3. **시나리오 데이터 시트 업데이트**:
   - 기본 카메라 촬영 횟수: 1회 → 3회로 수정 필요
   - 실제 로그와 시나리오의 불일치 원인 분석 필요

---

## 🔗 관련 파일

- `sample_logs/5차 샘플/audio.log`: PLAYER_EVENT 분석
- `sample_logs/5차 샘플/media_camera.log`: 세션 경계 확인
- `BasePatternStrategy.cs`: piid 중복 제거 로직
- `SessionContextProvider.cs`: 세션 시간 범위 확장 로직

---

**작성일**: 2025-10-08  
**작성자**: AI Assistant  
**버전**: 1.0

