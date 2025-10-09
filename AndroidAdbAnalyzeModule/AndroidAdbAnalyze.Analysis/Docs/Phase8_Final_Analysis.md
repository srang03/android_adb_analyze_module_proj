# Phase 8 - 최종 분석 보고서

**분석 완료일**: 2025-10-05  
**상태**: ✅ 촬영 감지 가능 확인

---

## 🎯 핵심 결론

### ✅ 1. 촬영 감지는 가능합니다!

**근거**:
- ✅ `AUDIO_TRACK` 이벤트로 셔터 음 감지 가능 (media_metrics.log)
- ✅ `URI_PERMISSION_GRANT` 이벤트로 촬영 vs 앨범 전송 구분 가능 (activity.log)
- ✅ 파싱 설정 이미 완료됨
- ✅ 로그 상수(`LogEventTypes.cs`) 이미 정의됨

**문제**: `CameraCaptureDetector.cs`가 이 이벤트들을 주 증거로 인식하지 않음

---

## 📊 실제 로그 분석 결과

### 2차 샘플 Ground Truth 검증

| 항목 | Ground Truth | 실제 로그 | 상태 |
|------|-------------|----------|------|
| 세션 수 | 5개 | 5개 (+ 시스템 14개) | ✅ 정확 |
| CONNECT | 5개 | 10개 (Ground Truth 5 + 시스템 5) | ✅ 정확 |
| DISCONNECT | 5개 | 24개 (Ground Truth 5 + 시스템 19) | ✅ 정확 |
| 촬영 1 | 21:59:13 | 21:59:14 셔터 음 (`AUDIO_TRACK`) | ✅ 감지 가능 |
| 촬영 2 | 22:02:27 | 22:02:24 URI Grant | ✅ 감지 가능 |
| 촬영 3 | 22:04:03 | 22:04:00 URI Grant | ✅ 감지 가능 |
| 앨범 전송 | 22:05:53 | 22:05:54 URI (MediaStore) | ✅ 구분 가능 |

---

## 🔍 발견된 촬영 감지 패턴

### 패턴 1: 셔터 음 (`AUDIO_TRACK`)

**로그 소스**: `media_metrics.log`

**예시** (촬영 1: 21:59:13):
```
83: {extractor, (10-05 21:59:14.567), (com.sec.android.app.camera, 0, 10123), (android.media.mediaextractor.entry=other, android.media.mediaextractor.fmt=SECOggExtractor, android.media.mediaextractor.mime=audio/ogg, android.media.mediaextractor.ntrk=1)}
84: {audio.track.38, (10-05 21:59:14.599), (com.sec.android.app.camera, 21384, 10123), (_allowUid=10123, event#=server.ctor, internalTrackId=58, streamType=AUDIO_STREAM_ENFORCED_AUDIBLE, traits=static)}
...
89: {audio.track.38, (10-05 21:59:14.720), (com.sec.android.app.camera, 0, 10123), (bufferSizeFrames=4800, event#=stop, executionTimeNs=204115, state=STATE_STOPPED, underrun=0)}
```

**특징**:
- Package: `com.sec.android.app.camera`
- Stream Type: `AUDIO_STREAM_ENFORCED_AUDIBLE` (시스템 강제 가청음)
- Duration: ~121ms (셔터 음 전형적 길이)
- **파싱 설정**: ✅ 이미 완료 (`adb_media_metrics_config.yaml` line 62-74)
- **Event Type**: `AUDIO_TRACK` (LogEventTypes.cs line 40)

---

### 패턴 2: URI 권한 (`URI_PERMISSION_GRANT`)

**로그 소스**: `activity.log`

#### 2.1. 카메라 촬영 패턴

**예시** (촬영 2: 22:02:27):
```
2025-10-05 22:02:24.650: +10123<1> content://com.kakao.talk.FileProvider/external_files/emulated/0/Android/data/com.kakao.talk/tmp/temp_1759669344637.jpg [user 0]<-com.kakao.talk
2025-10-05 22:02:34.039: -10123{0} content://com.kakao.talk.FileProvider/external_files/emulated/0/Android/data/com.kakao.talk/tmp/temp_1759669344637.jpg [user 0]
```

**특징**:
- UID: 10123 (카메라 앱)
- URI 경로: `/tmp/temp_*.jpg` ← **임시 파일!**
- Provider: `com.kakao.talk.FileProvider`
- 시점: 세션 시작 직후 (~0.7초)
- Revoke: 세션 종료 직후 (~0.2초)

#### 2.2. 앨범 전송 패턴 (오탐 방지)

**예시** (앨범 전송: 22:05:53):
```
2025-10-05 22:05:54.647: +10365<1> content://media/external/images/media/1176 [user 0]<-com.google.android.providers.media.module
```

**특징**:
- UID: 10365 (카카오톡 앱, 카메라 아님!)
- URI 경로: `content://media/external/images` ← **MediaStore!**
- Provider: `com.google.android.providers.media.module`
- 카메라 세션 없음!

**파싱 설정**: ✅ 이미 완료 (`adb_activity_config.yaml` line 72-84)
**Event Type**: `URI_PERMISSION_GRANT` (LogEventTypes.cs line 89)

---

## 🐛 현재 코드의 문제점

### 문제 1: 주 증거 타입 누락 ❌

**파일**: `CameraCaptureDetector.cs` (Line 20-25)

**현재 코드**:
```csharp
private static readonly HashSet<string> PrimaryEvidenceTypes = new()
{
    LogEventTypes.DATABASE_INSERT,      // ← 로그에 없음!
    LogEventTypes.DATABASE_EVENT,       // ← 로그에 없음!
    LogEventTypes.MEDIA_INSERT_END      // ← 로그에 없음!
};
```

**문제**:
- 로그에 존재하지 않는 이벤트 타입만 주 증거로 등록
- 실제로 파싱되는 `AUDIO_TRACK`, `URI_PERMISSION_GRANT` 미포함
- 결과: 촬영 감지 0개

---

### 문제 2: 시스템 패키지 미필터링 ❌

**파일**: `CameraSessionDetector.cs`

**현재 상황**:
- `android.system`의 14개 `Close camera` 이벤트가 세션으로 인식됨
- Ground Truth 시간대 이전 (21:54:14) 이벤트
- 세션 1개 추가 (Duration 0초)

---

### 문제 3: 로그 소스 중복 세션 ❌

**파일**: `CameraSessionDetector.cs` (Line 127-130)

**현재 동작**:
- `SourceSection`별로 세션 그룹화
- `camera_events` (media_camera_worker.log) + `camera_event` (media_camera.log) = 중복 세션 생성
- 결과: 8개 세션 (예상: 5개)

---

## 🔧 수정 계획

### 수정 1: 주 증거 타입 확대 ✅ **최우선**

**파일**: `CameraCaptureDetector.cs`

```csharp
// Line 20-25 수정
private static readonly HashSet<string> PrimaryEvidenceTypes = new()
{
    // 기존 (로그에 없지만 호환성 유지)
    LogEventTypes.DATABASE_INSERT,
    LogEventTypes.DATABASE_EVENT,
    LogEventTypes.MEDIA_INSERT_END,
    
    // 실제 로그에 존재하는 주 증거 추가
    LogEventTypes.AUDIO_TRACK,           // ← 셔터 음
    LogEventTypes.URI_PERMISSION_GRANT   // ← 촬영/앨범 구분
};
```

**예상 효과**: 촬영 0개 → 3개 감지

---

### 수정 2: 촬영/앨범 구분 로직 추가 ✅ **우선**

**파일**: `CameraCaptureDetector.cs` (새로운 메서드)

```csharp
// Line 246-266 수정 또는 새 메서드 추가
private bool IsCapturePath(string uri)
{
    // 카메라 촬영 임시 파일 패턴
    if (uri.Contains("/tmp/temp_", StringComparison.OrdinalIgnoreCase) ||
        uri.Contains("/cache/", StringComparison.OrdinalIgnoreCase) ||
        uri.Contains(".tmp.", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }
    
    return false;
}

private bool IsAlbumPath(string uri)
{
    // MediaStore (기존 앨범 사진)
    if (uri.Contains("content://media/external/images", StringComparison.OrdinalIgnoreCase) ||
        uri.Contains("content://media/external/video", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }
    
    return false;
}
```

**예상 효과**: 앨범 전송 오탐 방지 (22:05:54 이벤트 제외)

---

### 수정 3: 시스템 패키지 필터링 ✅ **중요**

**파일**: `CameraSessionDetector.cs` (Line 68-70 수정 또는 새 메서드)

```csharp
// DetectSessions() 메서드 내 추가
private static readonly string[] SystemPackages = new[]
{
    "android.system",
    "com.android.systemui"
};

// 세션 필터링
sessions = sessions
    .Where(s => !SystemPackages.Contains(s.PackageName, StringComparer.OrdinalIgnoreCase))
    .ToList();
```

**예상 효과**: 세션 8개 → 7개

---

### 수정 4: 로그 소스 통합 ✅ **중요**

**파일**: `CameraSessionDetector.cs` (Line 127-130)

```csharp
// ExtractRawSessions() 메서드 수정
// 현재 (SourceSection별 그룹화)
var eventsBySource = events
    .Where(e => e.Attributes.ContainsKey("package"))
    .GroupBy(e => (
        e.SourceSection,
        e.Attributes["package"]?.ToString() ?? string.Empty
    ))
    .ToList();

// 수정 (Package만으로 그룹화)
var eventsByPackage = events
    .Where(e => e.Attributes.ContainsKey("package"))
    .GroupBy(e => e.Attributes["package"]?.ToString() ?? string.Empty)
    .ToList();
```

**예상 효과**: 세션 7개 → 5개 (중복 제거)

---

## 📋 수정 우선순위

| 우선순위 | 수정 내용 | 예상 효과 | 난이도 |
|---------|----------|----------|-------|
| 1 | 주 증거 타입 확대 | 촬영 0→3개 | ⭐ 쉬움 |
| 2 | 촬영/앨범 구분 로직 | 오탐 방지 | ⭐⭐ 보통 |
| 3 | 시스템 패키지 필터링 | 세션 8→7개 | ⭐ 쉬움 |
| 4 | 로그 소스 통합 | 세션 7→5개 | ⭐⭐ 보통 |

**권장 순서**: 1 → 3 → 4 → 2  
(촬영 감지부터 수정 후, 세션 정확도 개선, 마지막으로 오탐 방지)

---

## 📊 예상 결과

### 현재 (수정 전)
```
세션: 8개 (예상: 5개) ❌
  - android.system: 1개 (Duration 0초)
  - com.sec.android.app.camera 중복: 3쌍 (6개)
  - com.sec.android.app.camera 정상: 1개
촬영: 0개 (예상: 3개) ❌
```

### 수정 후 (예상)
```
세션: 5개 ✅
  - 21:58:05 ~ 21:58:10 (5.9초) - 촬영 없음
  - 21:59:09 ~ 21:59:20 (11초) - 촬영 1개 (21:59:13)
  - 22:01:07 ~ 22:01:12 (5.4초) - 촬영 없음
  - 22:02:24 ~ 22:02:33 (9.8초) - 촬영 1개 (22:02:27)
  - 22:04:00 ~ 22:04:10 (10초) - 촬영 1개 (22:04:03)
촬영: 3개 ✅
  - 21:59:13 (기본 카메라, AUDIO_TRACK)
  - 22:02:27 (카카오톡, URI_PERMISSION_GRANT)
  - 22:04:03 (카카오톡, URI_PERMISSION_GRANT)
```

---

## 🎯 추가 검증 사항

### 1. 파싱 테스트 (디버깅 출력 확인)

**테스트 파일**: `EndToEndAnalysisTests.cs`

**확인 사항**:
- `AUDIO_TRACK` 이벤트 파싱 개수 확인
- `URI_PERMISSION_GRANT` 이벤트 파싱 개수 확인
- 이벤트 타입 통계에 두 타입이 나타나는지 확인

**예상 결과** (2차 샘플):
```
Top 20 Event Types:
  ...
  - AUDIO_TRACK: 35개
  - URI_PERMISSION_GRANT: 13개
  ...
```

### 2. 3차 샘플 (무음 카메라) 분석

**Ground Truth**:
```
무음 카메라 앱 실행	2025-10-05 22:19:50
무음 카메라 앱 종료	2025-10-05 22:19:55
무음 카메라 앱 실행	2025-10-05 22:20:22
무음 카메라 앱 사진 촬영	2025-10-05 22:20:27 ← 무음!
무음 카메라 앱 종료	2025-10-05 22:20:32
```

**예상**:
- `AUDIO_TRACK` 이벤트 없음 (무음이므로)
- `URI_PERMISSION_GRANT` 이벤트는 존재할 가능성 있음

---

## ✅ 결론

### 1. 촬영 감지는 가능합니다!

**증거**:
- ✅ 파싱 설정 완료 (`AUDIO_TRACK`, `URI_PERMISSION_GRANT`)
- ✅ 로그 상수 정의 완료 (`LogEventTypes.cs`)
- ✅ 실제 로그에 촬영 증거 존재 확인
- ✅ 촬영 vs 앨범 전송 구분 패턴 발견

**문제**: `CameraCaptureDetector`가 이 이벤트들을 주 증거로 인식하지 않음

### 2. 수정 작업은 단순합니다

**핵심 수정**:
1. `PrimaryEvidenceTypes`에 `AUDIO_TRACK`, `URI_PERMISSION_GRANT` 추가 (2줄)
2. 시스템 패키지 필터링 (5줄)
3. 로그 소스 통합 (2줄 수정)
4. 촬영/앨범 구분 로직 (새 메서드 20줄)

**총 작업량**: ~30줄 코드 수정/추가

### 3. Ground Truth 기준 정확도 달성 가능

**수정 후 예상**:
- 세션 정확도: 100% (5/5)
- 촬영 정확도: 100% (3/3)
- 오탐 방지: 100% (앨범 전송 제외)

---

**다음 단계**: 수정 작업 수행 및 테스트 재실행

**상태**: 분석 완료, 코드 수정 승인 대기 중
