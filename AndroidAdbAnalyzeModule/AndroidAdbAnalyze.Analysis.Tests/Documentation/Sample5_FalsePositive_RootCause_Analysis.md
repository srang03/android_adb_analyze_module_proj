# 5차 샘플 오탐 근본 원인 분석

## 📋 개요

- **분석 일자**: 2025-10-08
- **대상 오탐**: 23:15:42.062 (카카오톡 세션)
- **탐지 결과**: 촬영 1회로 잘못 탐지
- **실제 상황**: 카카오톡에서 카메라를 열었으나 촬영하지 않음

---

## 🔍 **오탐 상세 분석**

### **1. 탐지 정보**

```
시간: 23:15:42.062
신뢰도: 0.85
증거: URI_PERMISSION_GRANT, PLAYER_CREATED, VIBRATION_EVENT, PLAYER_RELEASED
앱: com.sec.android.app.camera (기본 카메라로 분류)
```

---

### **2. 실제 로그 분석**

#### **2.1 media_camera.log (세션 탐지)**

```
10-07 23:15:42 : CONNECT device 20 client for package com.sec.android.app.camera (PID 22548, priority 0)
10-07 23:15:46 : DISCONNECT device 20 client for package com.sec.android.app.camera (PID 22548)
```

**분석**:
- **package**: `com.sec.android.app.camera` → 기본 카메라로 분류
- **세션**: 23:15:42 ~ 23:15:46 (4초)

---

#### **2.2 usagestats.log (실제 앱 확인)**

```
time="2025-10-07 23:15:40" type=ACTIVITY_PAUSED package=com.kakao.talk class=com.kakao.talk.activity.chatroom.ChatRoomHolderActivity
time="2025-10-07 23:15:40" type=ACTIVITY_RESUMED package=com.kakao.talk class=com.kakao.talk.activity.media.PickMediaActivity
time="2025-10-07 23:15:41" type=ACTIVITY_PAUSED package=com.kakao.talk class=com.kakao.talk.activity.media.PickMediaActivity
time="2025-10-07 23:15:41" type=ACTIVITY_RESUMED package=com.sec.android.app.camera class=com.sec.android.app.camera.Camera 
                                                   instanceId=232839571 
                                                   taskRootPackage=com.kakao.talk ← 핵심!
                                                   taskRootClass=com.kakao.talk.activity.TaskRootActivity
time="2025-10-07 23:15:46" type=ACTIVITY_STOPPED package=com.sec.android.app.camera class=com.sec.android.app.camera.Camera 
                                                  instanceId=232839571 
                                                  taskRootPackage=com.kakao.talk ← 핵심!
```

**분석**:
- **package**: `com.sec.android.app.camera` (카메라 Activity)
- **taskRootPackage**: `com.kakao.talk` ← **실제 앱은 카카오톡!**
- **시나리오**: 카카오톡에서 채팅방 → 미디어 선택 → 카메라 열기 → 촬영 안 하고 닫기

---

#### **2.3 activity.log (URI 권한 부여)**

```
2025-10-07 23:15:42.062: +10123<1> content://com.kakao.talk.FileProvider/external_files/emulated/0/Android/data/com.kakao.talk/tmp/temp_1759846542047.jpg [user 0]<-com.kakao.talk
2025-10-07 23:15:47.322: -10123{0} content://com.kakao.talk.FileProvider/external_files/emulated/0/Android/data/com.kakao.talk/tmp/temp_1759846542047.jpg [user 0]
```

**분석**:
- **URI**: `com.kakao.talk.FileProvider` 임시 파일
- **경로**: `/tmp/temp_1759846542047.jpg` ← 임시 파일
- **판단**: `IsCapturePath(uri)` = **true** (임시 파일 경로)

---

#### **2.4 audio.log (오디오 이벤트)**

```
10-07 23:15:42:104 new player piid:447 uid/pid:10123/22548 package:com.sec.android.app.camera 
                   type:android.media.SoundPool 
                   attr:AudioAttributes: usage=USAGE_ASSISTANCE_SONIFICATION content=CONTENT_TYPE_SONIFICATION 
                   flags=0x801 tags=;CAMERA bundle=null session:0
10-07 23:15:46:786 releasing player piid:447, uid:10123
```

**분석**:
- **PLAYER_CREATED**: piid:447 (tags=CAMERA)
- **PLAYER_EVENT (started)**: ❌ **없음!** → 셔터 음 재생 안 됨
- **판단**: PLAYER_EVENT 조건부 주 증거로 사용 **불가**

---

#### **2.5 vibrator_manager.log (진동 이벤트)**

```
10-07 23:15:46.046 | effect | finished | duration: 132ms | usage: TOUCH | android (uid=1000) | reason: Virtual Key - Press
10-07 23:15:46.983 | effect | finished | duration: 243ms | usage: TOUCH | com.sec.android.app.launcher (uid=10162)
```

**분석**:
- **VIBRATION_EVENT**: android 패키지, launcher 패키지
- **판단**: com.sec.android.app.camera 패키지의 진동 **없음**

---

### **3. 촬영 탐지 로직 분석**

#### **3.1 확정 주 증거 (Primary Evidence)**

```csharp
private static readonly HashSet<string> PrimaryEvidenceTypes = new()
{
    LogEventTypes.DATABASE_INSERT,        // ❌ 없음
    LogEventTypes.MEDIA_EXTRACTOR,        // ❌ 없음
    LogEventTypes.SILENT_CAMERA_CAPTURE   // ❌ 없음
};
```

**결과**: 확정 주 증거 **0개**

---

#### **3.2 조건부 주 증거 (Conditional Primary Evidence)**

```csharp
private static readonly HashSet<string> ConditionalPrimaryEvidenceTypes = new()
{
    LogEventTypes.PLAYER_EVENT,           // ❌ piid:447은 started 없음
    LogEventTypes.URI_PERMISSION_GRANT,   // ✅ 있음 (temp 파일)
    LogEventTypes.SILENT_CAMERA_CAPTURE   // ❌ 없음
};
```

**결과**: 조건부 주 증거 **1개** (URI_PERMISSION_GRANT)

---

#### **3.3 URI_PERMISSION_GRANT 검증**

```csharp
private bool ValidateUriPermission(NormalizedLogEvent evidence)
{
    if (!evidence.Attributes.TryGetValue("uri", out var uriObj))
        return false;

    var uri = uriObj?.ToString() ?? string.Empty;
    
    // 앨범 경로 제외
    if (IsAlbumPath(uri))  // ❌ DCIM, Pictures 등
        return false;

    // 임시 파일 경로만 허용
    return IsCapturePath(uri);  // ✅ /tmp/ 포함 → true
}
```

**URI**: `content://com.kakao.talk.FileProvider/.../tmp/temp_1759846542047.jpg`

**판단**:
- `IsAlbumPath(uri)` = false (DCIM/Pictures 아님)
- `IsCapturePath(uri)` = **true** (/tmp/ 포함)
- **결과**: ✅ **검증 통과**

---

#### **3.4 신뢰도 계산**

```
증거:
- URI_PERMISSION_GRANT (조건부 주 증거)
- PLAYER_CREATED (보조 증거)
- VIBRATION_EVENT (보조 증거, android 패키지)
- PLAYER_RELEASED (보조 증거)

신뢰도: 0.85
```

**신뢰도 계산 로직**:
- 조건부 주 증거 1개: +0.6
- 보조 증거 3개: +0.25 (각 0.083)
- **합계**: 0.85

**판단**: 0.85 ≥ 0.60 (MinConfidenceThreshold) → ✅ **탐지**

---

## 🎯 **오탐의 근본 원인**

### **1. 세션 분류 오류**

**현재 로직** (media_camera 기반):
```
package: com.sec.android.app.camera → 기본 카메라로 분류
```

**실제**:
```
package: com.sec.android.app.camera
taskRootPackage: com.kakao.talk → 카카오톡 세션
```

**문제점**:
- media_camera 로그는 `package`만 있음
- `taskRootPackage` 정보 없음 → 카카오톡 구분 불가
- **결과**: 카카오톡 세션이 기본 카메라로 잘못 분류

---

### **2. URI만으로 촬영 판단**

**현재 로직**:
```
확정 주 증거 없음
→ 조건부 주 증거 조회
  → URI_PERMISSION_GRANT (temp 파일) ✅
  → PLAYER_EVENT (started) ❌
→ URI_PERMISSION_GRANT만으로 촬영 판단
```

**문제점**:
- **다른 주 증거 없음** (DATABASE, MEDIA_EXTRACTOR, PLAYER_EVENT)
- **URI만으로 촬영 판단** → 오탐 가능성 높음
- **카카오톡의 임시 파일**: 촬영하지 않아도 생성됨

---

### **3. 카카오톡의 카메라 사용 패턴**

**시나리오**:
```
1. 카카오톡 채팅방
2. 미디어 선택 화면 (PickMediaActivity)
3. 카메라 열기 (Camera Activity)
4. 촬영하지 않고 닫기 (Back 버튼)
5. 임시 파일 생성 (temp_*.jpg)
```

**특징**:
- **임시 파일 자동 생성**: 촬영하지 않아도 temp 파일 생성
- **셔터 음 없음**: PLAYER_EVENT (started) 없음
- **DATABASE 없음**: MediaStore에 저장 안 됨

---

## 💡 **개선 방안**

### **Option 1: usagestats 기반 세션 탐지** (근본 해결)

#### **장점**:
- ✅ **taskRootPackage 기반 정확한 앱 구분**
  - 기본 카메라: `taskRootPackage=com.sec.android.app.camera`
  - 카카오톡: `taskRootPackage=com.kakao.talk`
- ✅ **카카오톡 전용 전략 적용 가능**
  - 카카오톡 세션에서는 URI만으로 촬영 판단 안 함
  - 다른 주 증거 필수 (PLAYER_EVENT, DATABASE 등)

#### **구현**:
```csharp
// UsagestatsSessionSource
new CameraSession
{
    PackageName = taskRootPackage,  // com.kakao.talk
    ActualPackageName = package,    // com.sec.android.app.camera
    // ...
};

// KakaoTalkStrategy (신규)
public class KakaoTalkStrategy : ICaptureDetectionStrategy
{
    public string? PackageNamePattern => "com.kakao.talk";
    public int Priority => 100;
    
    public IReadOnlyList<CameraCaptureEvent> DetectCaptures(...)
    {
        // URI_PERMISSION_GRANT만으로는 촬영 판단 안 함
        // PLAYER_EVENT 또는 DATABASE_INSERT 필수
    }
}
```

#### **효과**:
- ✅ **오탐 완전 제거**: 카카오톡 세션 정확히 구분
- ✅ **카카오톡 촬영 정확히 탐지**: PLAYER_EVENT 있는 경우만

---

### **Option 2: URI_PERMISSION_GRANT 검증 강화** (현재 상태 개선)

#### **방안 A: 다른 보조 증거 필수**

```csharp
private bool ValidateUriPermission(NormalizedLogEvent evidence, SessionContext context)
{
    if (!evidence.Attributes.TryGetValue("uri", out var uriObj))
        return false;

    var uri = uriObj?.ToString() ?? string.Empty;
    
    // 앨범 경로 제외
    if (IsAlbumPath(uri))
        return false;

    // 임시 파일 경로 확인
    if (!IsCapturePath(uri))
        return false;

    // ✅ 추가: 다른 강력한 보조 증거 필수
    bool hasStrongSupportingEvidence = context.AllEvents.Any(e =>
        e.EventType == LogEventTypes.MEDIA_EXTRACTOR ||
        e.EventType == LogEventTypes.VIBRATION_EVENT ||  // 카메라 패키지만
        e.EventType == LogEventTypes.CAMERA_ACTIVITY_REFRESH);

    if (!hasStrongSupportingEvidence)
    {
        _logger.LogTrace(
            "[BaseStrategy] URI_PERMISSION_GRANT 제외: 강력한 보조 증거 없음 (uri={Uri})",
            uri);
        return false;
    }

    return true;
}
```

**효과**:
- ⚠️ **오탐 일부 감소**: MEDIA_EXTRACTOR 등 있는 경우만
- ❌ **근본 해결 아님**: 여전히 세션 분류 오류

---

#### **방안 B: 신뢰도 임계값 상향**

```csharp
// AnalysisOptions
public double MinConfidenceThreshold { get; set; } = 0.90;  // 0.60 → 0.90
```

**효과**:
- ⚠️ **오탐 일부 감소**: 신뢰도 0.85는 탐지 안 됨
- ❌ **정상 탐지도 감소**: 다른 촬영도 누락 가능

---

#### **방안 C: 카카오톡 provider 명시적 제외**

```csharp
private bool ValidateUriPermission(NormalizedLogEvent evidence)
{
    if (!evidence.Attributes.TryGetValue("uri", out var uriObj))
        return false;

    var uri = uriObj?.ToString() ?? string.Empty;
    
    // ✅ 추가: 카카오톡 provider 제외
    if (uri.Contains("com.kakao.talk.FileProvider", StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogTrace(
            "[BaseStrategy] URI_PERMISSION_GRANT 제외: 카카오톡 임시 파일 (uri={Uri})",
            uri);
        return false;
    }
    
    // 앨범 경로 제외
    if (IsAlbumPath(uri))
        return false;

    // 임시 파일 경로만 허용
    return IsCapturePath(uri);
}
```

**효과**:
- ✅ **이 오탐 제거**: 카카오톡 임시 파일 제외
- ⚠️ **정상 탐지도 누락**: 카카오톡에서 실제 촬영한 경우도 제외됨
- ❌ **하드코딩**: 다른 메신저 앱도 추가해야 함 (텔레그램, 라인 등)

---

### **Option 3: 하이브리드 접근** (추천)

1. **즉시 적용** (Option 2-C): 카카오톡 provider 명시적 제외
2. **중장기** (Option 1): usagestats 기반 세션 탐지 전환

**장점**:
- ✅ **즉시 오탐 제거**: 카카오톡 임시 파일 제외
- ✅ **근본 해결 준비**: usagestats 기반으로 점진적 전환

**단점**:
- ⚠️ **임시 하드코딩**: 카카오톡 provider 명시적 제외
- ⚠️ **다른 앱 추가 필요**: 텔레그램, 라인 등

---

## 📊 **개선 방안 비교**

| 방안 | 오탐 제거 | 정상 탐지 유지 | 구현 난이도 | 유지보수성 | 추천도 |
|------|----------|--------------|-----------|----------|---------|
| **Option 1**: usagestats 기반 | ✅ 완전 | ✅ 유지 | ⚠️ 높음 (7-10시간) | ✅ 우수 | ⭐⭐⭐⭐⭐ |
| **Option 2-A**: 보조 증거 필수 | ⚠️ 일부 | ⚠️ 일부 감소 | ✅ 낮음 (30분) | ⚠️ 보통 | ⭐⭐ |
| **Option 2-B**: 신뢰도 상향 | ⚠️ 일부 | ❌ 감소 | ✅ 낮음 (5분) | ❌ 나쁨 | ⭐ |
| **Option 2-C**: provider 제외 | ✅ 이 오탐만 | ⚠️ 카카오톡 촬영 누락 | ✅ 낮음 (10분) | ❌ 하드코딩 | ⭐⭐⭐ |
| **Option 3**: 하이브리드 | ✅ 완전 | ✅ 유지 | ⚠️ 높음 (단계적) | ✅ 우수 | ⭐⭐⭐⭐⭐ |

---

## 🎯 **결론**

### **오탐의 정확한 원인**:
1. ❌ **세션 분류 오류**: media_camera의 `package`만 사용 → 카카오톡 구분 불가
2. ❌ **URI만으로 촬영 판단**: 다른 주 증거 없이 URI_PERMISSION_GRANT만으로 탐지
3. ❌ **카카오톡의 임시 파일**: 촬영하지 않아도 temp 파일 생성

### **개선 가능 여부**: ✅ **가능**

### **추천 방안**: **Option 3 (하이브리드)**
1. **즉시**: 카카오톡 provider 명시적 제외 (10분)
2. **중장기**: usagestats 기반 세션 탐지 전환 (7-10시간)

### **예상 효과**:
- ✅ **오탐 완전 제거**: 카카오톡 세션 정확히 구분
- ✅ **정상 탐지 유지**: 카카오톡 실제 촬영 정확히 탐지
- ✅ **유지보수성 향상**: taskRootPackage 기반 확장 가능

---

**작성일**: 2025-10-08  
**작성자**: AI Assistant  
**버전**: 1.0

