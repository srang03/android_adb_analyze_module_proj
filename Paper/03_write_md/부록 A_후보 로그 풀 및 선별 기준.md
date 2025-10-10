# 부록 A: 후보 로그 풀 및 선별 기준

## A.1 개요

본 부록은 3.3절에서 언급한 분석 대상 로그 선정 과정의 상세 근거와 후보 로그 풀의 전체 현황을 제시한다. 17편의 선행연구 분석을 통해 확보한 23개의 dumpsys 서비스를 대상으로, 카메라 활동 탐지를 위한 체계적 선별 과정과 그 결과를 상세히 기술한다.

## A.2 전체 후보 로그 풀 현황

### A.2.1 선행연구 기반 후보 로그 추출

17편의 선행연구에서 카메라 관련 로그 분석에 활용된 dumpsys 서비스를 전수 조사한 결과, 총 23개의 후보 로그를 확보하였다.

**<표 A-1> 선행연구별 dumpsys 서비스 활용 현황**

| 서비스명                    | 활용 논문 수 | 주요 활용 논문                                                                | 기능 분류     |
| --------------------------- | ------------ | ----------------------------------------------------------------------------- | ------------- |
| **media.camera**      | 4편          | 안원석(2025), 권혁철(2024), 김종만(2018), 최슬기(2015)                        | 카메라 서비스 |
| **usagestats**        | 8편          | 강예지(2022,2021), 이지원(2024), 안원석(2025), 권혁철(2024), Bortnik(2020) 외 | 앱 사용 통계  |
| **activity**          | 6편          | 이지원(2024), 김수영(2023), 이경률(2017), Bortnik(2020), 조재형(2017) 외      | 앱 생명주기   |
| **audio**             | 3편          | 안원석(2025), 권혁철(2024), 최슬기(2015)                                      | 오디오 서비스 |
| **package**           | 7편          | 안원석(2025), 김수영(2023), 김종만(2018), 최슬기(2015), 조형철(2024) 외       | 패키지 정보   |
| **wifi**              | 5편          | 이경률(2017), Bortnik(2020), 권혁철(2024), 조재형(2017) 외                    | 네트워크 상태 |
| **bluetooth_manager** | 3편          | Bortnik(2020), 권혁철(2024), 조형철(2024)                                     | 블루투스 관리 |
| **appops**            | 2편          | 최슬기(2015), 권혁철(2024)                                                    | 앱 권한 관리  |
| **power**             | 2편          | 이경률(2017), Bortnik(2020)                                                   | 전원 관리     |
| **meminfo**           | 2편          | 이경률(2017), 최슬기(2015)                                                    | 메모리 정보   |
| 기타 13개 서비스            | 각 1편       | -                                                                             | 지원 정보     |

### A.2.2 계층별 후보 로그 분류

3장에서 제시한 Level 1-3 계층적 탐지 모델에 따라 후보 로그를 분류하였다.

**<표 A-2> 계층별 후보 로그 분류**

| 계층              | 역할               | 후보 로그 (개수)                                                      | 선별 기준                               |
| ----------------- | ------------------ | --------------------------------------------------------------------- | --------------------------------------- |
| **Level 1** | 서비스 활성화 탐지 | media.camera, audio, media.camera.worker, appops, media.metrics (5개) | 직접적 카메라/오디오 서비스 활성화 로그 |
| **Level 2** | 사용 맥락 분석     | usagestats, activity, package, wifi (4개)                             | 앱 실행 및 시스템 상태 변화 로그        |
| **Level 3** | 실행 흔적 검증     | external.db, logcat, vibrator_manager, bluetooth_manager (4개)        | 실제 촬영 행위 및 부가 효과 로그        |
| **기타**    | 지원 정보          | power, meminfo, cpuinfo, account, window 외 10개                      | 시스템 상태 및 환경 정보                |

## A.3 각 로그별 상세 분석

### A.3.1 Level 1 로그 (서비스 활성화)

#### A.3.1.1 media.camera

**기능**: 카메라 서비스 활성화/비활성화 및 사용 앱 추적
**휘발성**: 휘발성 (재부팅 시 삭제)
**보존 특성**: 최대 100개 이벤트, 7일 이상 보관 (안원석, 2025)
**탐지 패턴**:

```bash
CONNECT device /dev/video0 client 12345 (package com.sec.android.app.camera)
DISCONNECT device /dev/video0 client 12345
```

**선행연구 근거**:

- 안원석(2025): 4개 핵심 dumpsys 서비스 중 하나로 분류, 카메라 ON/OFF 상태 정확 탐지
- 권혁철(2024): Table 11에서 Camera On/Off 탐지 방법론 제시
- 최슬기(2015): 데이터 선정 단계에서 카메라 관련 핵심 서비스로 분류

#### A.3.1.2 audio

**기능**: 오디오 녹음 서비스 활성화 및 앱별 사용 현황
**휘발성**: 휘발성 (재부팅 시 삭제)
**보존 특성**: 최대 50개 이벤트, 7일 이상 보관 (안원석, 2025)
**탐지 패턴**:

```bash
rec start: source=7 session=12345 uid=10123
rec stop: source=7 session=12345
```

**선행연구 근거**:

- 안원석(2025): 녹음 시작/종료, UID 및 오디오 소스 정보 제공
- 권혁철(2024): 오디오 관련 카메라 앱 사용 패턴 분석에 활용

#### A.3.1.3 appops

**기능**: 앱별 권한 사용 이력 및 시간 정보
**휘발성**: 비휘발성 (영구 보존)
**보존 특성**: 마지막 사용 시점까지 +XXXdXXhXXmXXsXXXms ago 형태로 정밀 기록
**탐지 패턴**:

```bash
CAMERA: mode=0; time=+5s831ms ago; duration=0
RECORD_AUDIO: mode=0; time=+283d11h9m24s542ms ago; duration=0
```

**선행연구 근거**:

- 최슬기(2015): 37개 권한의 시간별 사용 이력 정밀 추적, Android 4.3+ 지원
- 권혁철(2024): 카메라 권한 사용 패턴 분석에 활용

### A.3.2 Level 2 로그 (사용 맥락)

#### A.3.2.1 usagestats

**기능**: 앱 사용 통계 및 액티비티 생명주기 추적
**휘발성**: 비휘발성 (24시간 보존, 재부팅 후에도 유지)
**보존 특성**: 일 단위 로그 파일, Protocol Buffer 형태 저장
**탐지 패턴**:

```bash
ACTIVITY_RESUMED: com.sec.android.app.camera
ACTIVITY_PAUSED: com.sec.android.app.camera
FOREGROUND_SERVICE_START: com.telegram.messenger
```

**선행연구 근거**:

- 강예지(2022): S1-S4 아키텍처 구분의 핵심 근거, Activity vs Service 패턴 분석
- 강예지(2021): Android 5-12 버전별 구조 진화 분석, 15개 이벤트 타입 분류
- 이지원(2024): 2초 단위 앱 실행 추적, 앱 삭제 후에도 사용 기록 보존 실증
- 안원석(2025): 액티비티 Resume/Stopped/Paused 정보, 재부팅 후 24시간 보존

#### A.3.2.2 activity

**기능**: 앱 생명주기 관리 및 브로드캐스트 수신기 추적
**휘발성**: 비휘발성 (장기 보존)
**보존 특성**: Recent Tasks는 22시간 보존 (Bortnik, 2020)
**탐지 패턴**:

```bash
ApplicationExitInfo: reason=USER_REQUESTED
PACKAGE_REMOVED: com.example.camera
Recent Tasks Intent: {act=android.intent.action.MAIN cat=[android.intent.category.LAUNCHER]}
```

**선행연구 근거**:

- 이지원(2024): ApplicationExitInfo, PACKAGE_REMOVED를 통한 앱 삭제 추적
- Bortnik(2020): Recent Tasks 분석을 통한 앱 실행 이력 추적

#### A.3.2.3 package

**기능**: 앱 설치/삭제 이력, 권한 정보, 패키지 상세 정보
**휘발성**: 비휘발성 (영구 보존)
**탐지 패턴**:

```bash
Package [com.sec.android.app.camera] permissions:
  android.permission.CAMERA: granted=true
firstInstallTime=2024-01-15 14:23:05
lastUpdateTime=2024-02-20 09:15:33
```

**선행연구 근거**:

- 안원석(2025): 패키지명, 설치 상태, 권한 정보 영구 보존
- 최슬기(2015): 앱 설치 및 업데이트 패턴 분석
- 김종만(2018): ADB 기반 패키지 정보 수집 방법론

### A.3.3 Level 3 로그 (실행 흔적)

#### A.3.3.1 external.db

**기능**: 미디어 파일 생성/삭제 이력 및 메타데이터
**휘발성**: 비휘발성 (파일 삭제 후에도 row_status='D'로 보존)
**저장 경로**: `/data/data/com.android.providers.media/databases/external.db`
**탐지 패턴**:

```sql
SELECT _data, date_added, date_modified, row_status 
FROM files WHERE _data LIKE '%DCIM%'
```

**선행연구 근거**:

- 김종만(2018): External.db 기반 미디어 로그 분석의 선구적 접근, 삭제 흔적 추적
- 정승태(2024): 카메라 로그와 Media DB 메타데이터 60+개 항목 통합 분석

#### A.3.3.2 logcat

**기능**: 실시간 시스템 로그, 실제 촬영 행위 특정
**휘발성**: 휘발성 (Ring Buffer, 제조사별 차이)
**보존 특성**: Samsung 5MiB/20-40분, Google/Xiaomi 256KiB/1-2분 (권혁철, 2024)
**탐지 패턴**:

```bash
# 삼성: CaptureResult onCaptureCompleted
# 샤오미: QCAMERA_HAL_INFO: capture
# 구글: CameraService: camera 0 closed
```

**선행연구 근거**:

- 권혁철(2024): 제조사별 실제 촬영 행위 특정, 99.8% 탐지율
- 김승규(2020): 현장용 도구에서 로그 분석 활용

## A.4 선별 기준 및 적용

### A.4.1 H1 가설 검증을 위한 아키텍처 구분력 기준

S1-S4 앱 아키텍처 유형을 구분할 수 있는 판별력을 기준으로 선별하였다.

**<표 A-3> 아키텍처 구분력 평가**

| 로그 서비스            | S1 (직접API)     | S2 (Intent)    | S3 (내장)          | S4 (특수)    | 구분력 |
| ---------------------- | ---------------- | -------------- | ------------------ | ------------ | ------ |
| **usagestats**   | ACTIVITY_RESUMED | 기본앱→타겟앱 | FOREGROUND_SERVICE | 특수패턴     | ⭐⭐⭐ |
| **media.camera** | CONNECT 직접     | CONNECT 간접   | CONNECT 직접       | CONNECT 없음 | ⭐⭐⭐ |
| **activity**     | Recent Tasks     | Intent 패턴    | Service 패턴       | Hidden 패턴  | ⭐⭐   |
| **audio**        | 동시 녹음        | 선택 녹음      | 동시 녹음          | 무음 녹음    | ⭐⭐   |
| power                  | 화면 ON 공통     | 화면 ON 공통   | 화면 ON 공통       | 배경 실행    | ⭐     |

**선별 결과**: usagestats, media.camera > activity, audio > 기타 순서

### A.4.2 H2 가설 검증을 위한 휘발성 측정력 기준

V0-V-Event 휘발성 조건별 측정 가능성을 기준으로 선별하였다.

**<표 A-4> 휘발성 측정력 평가**

| 로그 서비스            | V0 (즉시) | V-Time (24h) | V-Usage (누적) | V-Event (재부팅) | 측정력 |
| ---------------------- | --------- | ------------ | -------------- | ---------------- | ------ |
| **media.camera** | 완전보존  | 완전보존     | 순환저장       | 완전소실         | ⭐⭐⭐ |
| **audio**        | 완전보존  | 완전보존     | 순환저장       | 완전소실         | ⭐⭐⭐ |
| **usagestats**   | 완전보존  | 완전보존     | 완전보존       | 완전보존         | ⭐⭐   |
| **activity**     | 완전보존  | 부분보존     | 완전보존       | 완전보존         | ⭐⭐   |
| package                | 완전보존  | 완전보존     | 완전보존       | 완전보존         | ⭐     |

**선별 결과**: media.camera, audio > usagestats, activity > package 순서

### A.4.3 통합 선별 기준 적용

H1과 H2 가설 검증을 위한 두 기준을 통합하여 최종 선별을 수행하였다.

**<표 A-5> 통합 선별 기준 적용 결과**

| 순위 | 로그 서비스                | 아키텍처 구분력 | 휘발성 측정력 | 선행연구 검증도 | 종합 점수 | 선정 여부 |
| ---- | -------------------------- | --------------- | ------------- | --------------- | --------- | --------- |
| 1    | **media.camera**     | ⭐⭐⭐          | ⭐⭐⭐        | 4편             | 9점       | ✅        |
| 2    | **usagestats**       | ⭐⭐⭐          | ⭐⭐          | 8편             | 8점       | ✅        |
| 3    | **audio**            | ⭐⭐            | ⭐⭐⭐        | 3편             | 8점       | ✅        |
| 4    | **activity**         | ⭐⭐            | ⭐⭐          | 6편             | 6점       | ✅        |
| 5    | **media.metrics**    | ⭐⭐            | ⭐⭐          | 실험검증        | 6점       | ✅        |
| 6    | **vibrator_manager** | ⭐              | ⭐⭐          | 실험검증        | 5점       | ✅        |
| 7    | appops                     | ⭐⭐            | ⭐            | 2편             | 5점       | ❌        |
| 8    | package                    | ⭐              | ⭐            | 7편             | 3점       | ❌        |

## A.5 최종 선정 로그 정당성 검증

### A.5.1 6개 로그 선정의 이론적 근거

최종 선정된 6개 로그는 다음과 같은 이론적 근거를 가진다:

1. **범용성 우수 (4개)**: media.camera, usagestats, audio, activity

   - 다수 선행연구에서 검증된 신뢰성
   - 모든 제조사/기기에서 안정적 지원
2. **특화 기능 (2개)**: media.metrics, vibrator_manager

   - Level 3 실행 흔적 탐지의 핵심 역할
   - 실제 촬영 행위와 무음 카메라 구분에 특화

### A.5.2 실험 데이터 기반 검증

예비실험 데이터 분석 결과, 선정된 6개 로그의 유효성이 실증되었다:

**<표 A-6> 예비실험 검증 결과**

| 로그 서비스      | 기본앱 | 무음앱 | KakaoTalk  | Telegram   | 구분 성능       |
| ---------------- | ------ | ------ | ---------- | ---------- | --------------- |
| media.camera     | T      | F      | T          | T          | S1-S4 구분      |
| usagestats       | T      | T      | T          | T          | V0-V-Event 측정 |
| audio            | T      | F      | T          | T          | 무음 탐지       |
| activity         | T      | T      | T          | T          | 앱 생명주기     |
| media.metrics    | T      | F      | 구분어려움 | 구분어려움 | 특화 탐지       |
| vibrator_manager | T      | F      | T          | T          | 진동 피드백     |

**검증 결과**:

- 기본 4개 로그: 모든 아키텍처에서 안정적 탐지
- Level 3 2개 로그: 무음 카메라(S4) 특화 탐지 성능 우수

### A.5.3 제외된 로그의 한계점

선정되지 않은 로그들의 한계점은 다음과 같다:

1. **appops**: 시간 정보가 상대적이어서 정확한 시점 특정 어려움
2. **package**: 정적 정보로 실시간 활동 탐지 불가
3. **wifi, power**: 카메라와 간접적 관련성, 거짓양성 위험
4. **logcat**: 제조사별 차이가 커서 일관성 확보 어려움

## A.6 결론

17편 선행연구 분석을 통해 확보한 23개 후보 로그 중에서, H1-H2 가설 검증을 위한 체계적 선별 과정을 거쳐 최종 6개 로그를 선정하였다. 선정된 로그는 아키텍처 구분력과 휘발성 측정력을 모두 만족하며, 실제 실험 데이터를 통해 그 유효성이 검증되었다. 이는 본 연구의 160세션 대규모 실험에서 신뢰성 있는 결과를 도출할 수 있는 견고한 기반을 제공한다.

---

**주요 참고문헌**:

- 안원석(2025): "안드로이드 로그를 활용한 촬영 및 음성 녹음 탐지 방안 연구"
- 권혁철(2024): "Android 진단 로그 포렌식 분석 및 정규화 방안 연구"
- 강예지(2022): "Analysis on android usagestats for digital investigation"
- Bortnik, L. & Lavrenovs, A. (2020): "Android-Dumpsys-Analysis-to-Indicate-Driver-Distraction"
