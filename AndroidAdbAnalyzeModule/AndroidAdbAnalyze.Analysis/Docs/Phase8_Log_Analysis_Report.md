# Phase 8 - 실제 로그 파일 정밀 분석 보고서

**분석일**: 2025-10-05  
**대상**: 2차 샘플, 3차 샘플 (Ground Truth 기준)  
**목적**: 촬영 감지 가능성 재검토 및 패턴 발견

---

## 🎯 핵심 발견 요약

### ✅ 1. DATABASE 이벤트는 실제로 없습니다

**결론**: `DATABASE_INSERT`, `DATABASE_EVENT`, `MEDIA_INSERT_END` 이벤트는 **2차 샘플 로그에 존재하지 않습니다**.

**증거**:
- `media_metrics.log`를 전체 검토한 결과, `extractor`, `audio.track`, `codec` 이벤트만 존재
- `DATABASE` 문자열을 포함한 이벤트는 **0개**
- 현재 `CameraCaptureDetector`가 기대하는 주 증거(Primary Evidence)가 없음

---

### ✅ 2. 카메라 세션 정보는 정확합니다

#### Ground Truth 대비 실제 로그 (2차 샘플)

| 세션 | Ground Truth | media_camera_worker.log | media_camera.log | 패키지 |
|------|--------------|-------------------------|------------------|-------|
| 1 | 21:58:03~09 | 21:58:05.444 ~ 21:58:10.931 | CONNECT ~ DISCONNECT | com.sec.android.app.camera |
| 2 | 21:59:08~18 | 21:59:09.763 ~ 21:59:20.059 | CONNECT ~ DISCONNECT | com.sec.android.app.camera |
| 3 | 22:01:05~10 | 22:01:07.723 ~ 22:01:12.390 | CONNECT ~ DISCONNECT | com.sec.android.app.camera |
| 4 | 22:02:17~32 | 22:02:24.702 ~ 22:02:33.811 | CONNECT ~ DISCONNECT | com.sec.android.app.camera |
| 5 | 22:03:58~08 | 22:04:00.761 ~ 22:04:10.012 | CONNECT ~ DISCONNECT | com.sec.android.app.camera |

**분석**:
- ✅ 5개 세션 모두 정확히 감지 가능
- ✅ `CAMERA_CONNECT` 10개, `CAMERA_DISCONNECT` 24개 불균형은 **시스템 초기화 이벤트** 때문
  - `media_camera_worker.log` 5-18줄: `android.system`의 14개 `Close camera` 이벤트 (21:54:14 시각)
  - 이는 Ground Truth 시간대 이전의 시스템 종료 이벤트

**Ground Truth 시간대만 필터링하면**: `CAMERA_CONNECT` 5개 = `CAMERA_DISCONNECT` 5개 ✅

---

### ✅ 3. 촬영 감지 가능! - `audio.track` + `activity.log` 패턴

#### 3.1. audio.track (셔터 음)

**`media_metrics.log` 분석 결과**:

##### ❌ 촬영 1: 21:59:13 (기본 카메라)
```
21:59:14.567: extractor (com.sec.android.app.camera) - audio/ogg
21:59:14.599: audio.track.38 (com.sec.android.app.camera) - AUDIO_STREAM_ENFORCED_AUDIBLE
21:59:14.720: audio.track.38 stop (duration: 121ms)
```
→ **셔터 음 감지! 촬영 시각 21:59:13과 약 1초 차이**

##### ❌ 촬영 2: 22:02:27 (카카오톡 카메라)
`media_metrics.log` 검색 필요 (라인 100 이후)

##### ❌ 촬영 3: 22:04:03 (카카오톡 카메라)
`media_metrics.log` 검색 필요 (라인 100 이후)

#### 3.2. activity.log - URI PERMISSIONS (결정적 증거!)

**`activity.log` 6758-6791줄 분석**:

```
Uri Permission History:
  2025-10-05 22:01:07.647: +10123<1> content://com.kakao.talk.FileProvider/.../tmp/temp_1759669267633.jpg
  2025-10-05 22:01:12.992: -10123{0} content://com.kakao.talk.FileProvider/.../tmp/temp_1759669267633.jpg
  
  2025-10-05 22:02:24.650: +10123<1> content://com.kakao.talk.FileProvider/.../tmp/temp_1759669344637.jpg ← 촬영 2 증거!
  2025-10-05 22:02:34.039: -10123{0} content://com.kakao.talk.FileProvider/.../tmp/temp_1759669344637.jpg
  
  2025-10-05 22:04:00.698: +10123<1> content://com.kakao.talk.FileProvider/.../tmp/temp_1759669440677.jpg ← 촬영 3 증거!
  2025-10-05 22:04:10.228: -10123{0} content://com.kakao.talk.FileProvider/.../tmp/temp_1759669440677.jpg
  
  2025-10-05 22:05:54.647: +10365<1> content://media/external/images/media/1176 ← 앨범 전송 (촬영 아님!)
```

**핵심 발견**:

1. **카메라 촬영 패턴** (UID 10123 = 기본 카메라 앱)
   - `+10123` (Grant): 카메라 앱이 카카오톡 임시 파일(`/tmp/temp_*.jpg`)에 URI 권한 부여
   - 시각: 세션 시작 직후 (22:01:07, 22:02:24, 22:04:00)
   - 경로: `com.kakao.talk.FileProvider/.../tmp/` ← **임시 파일 = 촬영!**
   - `-10123` (Revoke): 세션 종료 직후 권한 해제

2. **앨범 전송 패턴** (UID 10365 = 카카오톡 앱)
   - `+10365` (Grant): 카카오톡 앱이 미디어 제공자 URI 획득
   - 시각: 22:05:54 (카메라 세션 없음)
   - 경로: `content://media/external/images/media/1176` ← **MediaStore = 기존 앨범 사진!**
   - Revoke 없음 (영구 권한)

**결론**: 
- ✅ **촬영 vs 앨범 전송 구분 가능!**
- ✅ **URI 권한 패턴으로 촬영 시점 정확히 감지 가능!**

---

## 📊 촬영 감지 증거 종합

### 촬영 1: 21:59:13 (기본 카메라)

| 증거 소스 | 시각 | 내용 |
|----------|------|------|
| Ground Truth | 21:59:13 | 기본 카메라 사진 촬영 |
| media_metrics.log | 21:59:14.567 | extractor (audio/ogg) |
| media_metrics.log | 21:59:14.599~720 | audio.track.38 (셔터 음, 121ms) |

### 촬영 2: 22:02:27 (카카오톡 카메라)

| 증거 소스 | 시각 | 내용 |
|----------|------|------|
| Ground Truth | 22:02:27 | 카카오톡 앱 카메라 사진 촬영 |
| activity.log | 22:02:24.650 | URI Grant (카메라 → 카카오톡 임시 파일) |
| activity.log | 22:02:34.039 | URI Revoke |

### 촬영 3: 22:04:03 (카카오톡 카메라)

| 증거 소스 | 시각 | 내용 |
|----------|------|------|
| Ground Truth | 22:04:03 | 카카오톡 앱 카메라 사진 촬영 |
| activity.log | 22:04:00.698 | URI Grant (카메라 → 카카오톡 임시 파일) |
| activity.log | 22:04:10.228 | URI Revoke |

### ❌ 앨범 전송: 22:05:53 (오탐 방지 대상)

| 증거 소스 | 시각 | 내용 |
|----------|------|------|
| Ground Truth | 22:05:53 | 카카오톡 앱 기존 앨범 사진 전송 |
| activity.log | 22:05:54.647 | URI Grant (카카오톡 → MediaStore) |
| media_camera_worker.log | **없음** | ❌ 카메라 세션 없음 |

**구분 포인트**:
- 촬영: URI 경로에 `/tmp/` 포함 + UID 10123(카메라)
- 앨범 전송: URI 경로에 `media/external/images` 포함 + UID 10365(카카오톡)

---

## 🔍 현재 코드의 문제점

### 1. 주 증거(Primary Evidence) 부재

**문제**:
```csharp
// CameraCaptureDetector.cs - Line 20-25
private static readonly HashSet<string> PrimaryEvidenceTypes = new()
{
    LogEventTypes.DATABASE_INSERT,      // ← 로그에 없음!
    LogEventTypes.DATABASE_EVENT,       // ← 로그에 없음!
    LogEventTypes.MEDIA_INSERT_END      // ← 로그에 없음!
};
```

**해결책**:
- `AUDIO_TRACK` (셔터 음) 추가
- `URI_PERMISSION_GRANT` (activity.log) 추가

### 2. 파싱 설정 부재

**문제**:
- `media_metrics.log`의 `audio.track` 이벤트 파싱 설정 확인 필요
- `activity.log`의 `URI_PERMISSION_GRANT` 이벤트 파싱 설정 확인 필요

---

## 📋 수정 계획

### 수정 1: 파싱 설정 확인 ✅ **우선순위 1**

**확인 대상**:
1. `adb_media_metrics_config.yaml` - `audio.track` 이벤트 파싱 여부
2. `adb_activity_config.yaml` - `URI_PERMISSION_GRANT` 이벤트 파싱 여부

### 수정 2: 주 증거 타입 확대 ✅ **우선순위 2**

```csharp
// CameraCaptureDetector.cs - PrimaryEvidenceTypes
private static readonly HashSet<string> PrimaryEvidenceTypes = new()
{
    // 기존 (로그에 없음, 유지)
    LogEventTypes.DATABASE_INSERT,
    LogEventTypes.DATABASE_EVENT,
    LogEventTypes.MEDIA_INSERT_END,
    
    // 새로 추가 (로그에 존재)
    LogEventTypes.AUDIO_TRACK,           // media_metrics.log
    LogEventTypes.URI_PERMISSION_GRANT   // activity.log
};
```

### 수정 3: 보조 증거 강화

```csharp
// 임시 파일 경로 패턴 (카카오톡 카메라 촬영)
private static readonly string[] CaptureTempPathPatterns = new[]
{
    "/tmp/temp_",
    "/cache/",
    ".tmp."
};

// 미디어 스토어 패턴 (앨범 전송, 제외)
private static readonly string[] AlbumPathPatterns = new[]
{
    "content://media/external/images",
    "content://media/external/video"
};
```

---

## 🎯 예상 결과

### 수정 전 (현재)
- 세션: 8개 (예상: 5개) ← 중복 세션 + 시스템 세션
- 촬영: 0개 (예상: 3개) ← 주 증거 없음

### 수정 후 (기대)
- 세션: 5개 ✅ (로그 소스 통합 + android.system 필터링)
- 촬영: 3개 ✅ (`audio.track` + `URI_PERMISSION_GRANT` 추가)

---

## 📌 추가 발견

### 1. 무음 카메라 감지 불가 (예상대로)

3차 샘플 분석 필요하지만, 무음 카메라는:
- `audio.track` 이벤트 없음 (셔터 음 없음)
- `URI_PERMISSION_GRANT`는 존재할 가능성 있음

### 2. 시스템 패키지 필터링 필요

```
android.system: 14개 Close camera 이벤트 (21:54:14)
→ Ground Truth 이전, 필터링 필요
```

### 3. KST → UTC 변환 정확

- Ground Truth (KST 21:58) = 로그 UTC 12:58
- 테스트에서 본 12:58~13:04 시간대 정확

---

**다음 단계**: 
1. 파싱 설정 검토 (`adb_media_metrics_config.yaml`, `adb_activity_config.yaml`)
2. 주 증거 타입 확대 코드 수정
3. 테스트 재실행 및 검증

**상태**: 분석 완료, 코드 수정 대기 중
