# 부록 A: 후보 로그 풀 구성 (1단계: 문헌 연구 기반)

## A.1 개요

본 부록은 3.3절 "분석 대상 로그 선정"의 1단계에 해당하는 문헌 연구 기반 후보 로그 풀 구성 과정을 상세히 기술한다. 총 17편의 선행연구 전수 분석과 Android 공식문서 검토를 통해 카메라 활동 탐지에 관련된 후보 로그들을 체계적으로 수집하고 정리하였다.

## A.2 선행연구 기반 후보 로그 수집

### A.2.1 선행연구 전수 분석

17편의 선행연구(국내 15편, 해외 2편)에서 dumpsys 서비스 활용 현황을 전수 조사하였다. 각 논문에서 언급되거나 활용된 모든 dumpsys 서비스를 T(활용)/F(미활용)로 분류하여 데이터베이스화했고 아래 <표 A-1>과 같이 정리하였다.

**<표 A-1> 선행연구별 dumpsys 서비스 활용 현황 (정확한 통계)**

| 서비스명 | 활용 논문 수 | 주요 활용 논문 |
|----------|--------------|----------------|
| **activity** | 11편 | 김수영(2023), 권혁철(2024), 최슬기(2015), 이지원(2024), 이경률(2017), 임상우(2013), 안원석(2025), 조형철(2024), 정승태(2024), Bortnik(2013,2020) |
| **usagestats** | 8편 | 강예지(2022,2021), 권혁철(2024), 최슬기(2015), 이지원(2024), 조재형(2017), 안원석(2025), Bortnik(2020) |
| **wifi** | 6편 | 권혁철(2024), 이경률(2017), 조재형(2017), 안원석(2025), 조형철(2024), Bortnik(2020) |
| **package** | 5편 | 김수영(2023), 권혁철(2024), 최슬기(2015), 임상우(2013), 안원석(2025) |
| **audio** | 3편 | 권혁철(2024), 이경률(2017), 안원석(2025) |
| **bluetooth_manager** | 2편 | 권혁철(2024), Bortnik(2020) |
| **media.camera** | 2편 | 안원석(2025), 권혁철(2024) |
| **meminfo** | 2편 | 이경률(2017), 최슬기(2015) |
| **appops** | 1편 | 최슬기(2015) |
| **power** | 1편 | 이경률(2017) |
| **account** | 1편 | 최슬기(2015) |
| **window** | 1편 | 이경률(2017) |
| **cpuinfo** | 1편 | 이경률(2017) |
| **diskstats** | 1편 | 최슬기(2015) |
| **iphonesubinfo** | 1편 | 최슬기(2015) |

### A.2.2 선행연구 분석을 통한 발견사항

1. **높은 검증도 로그**: `activity`(11편), `usagestats`(8편), `wifi`(6편)가 다수 연구에서 검증
2. **카메라 직접 관련**: `media.camera`(2편), `audio`(3편)가 최신 연구에서 활용
3. **권한/패키지 관련**: `package`(5편), `appops`(1편)가 앱 분석에 활용
4. **시스템 상태**: `meminfo`, `power`, `bluetooth_manager` 등이 컨텍스트 정보 제공

**1단계 결과**: 선행연구 기반으로 **15개 검증된 후보 로그** 확보

## A.3 Android 공식문서 기반 추가 후보 로그

### A.3.1 추가 후보 로그 선정 기준

선행연구에서 다뤄지지 않았으나, Android 공식 개발자 문서에서 카메라 관련 기능이 명시적으로 확인되는 서비스를 추가 후보로 선정하였다.

### A.3.2 media.metrics

**Android 공식 근거**:
- 출처: [android.media.metrics Package Summary](https://developer.android.com/reference/android/media/metrics/package-summary)
- API 레벨: Android 12+ (API Level 31)

**주요 기능**:
- `MediaMetricsManager`: 미디어 메트릭 수집 및 관리
- `RecordingSession`: 미디어 녹화 세션 추적
- `PlaybackSession`: 미디어 재생 메트릭 분석

**카메라 관련성**: 카메라 녹화 시 RecordingSession 메트릭 생성으로 실제 촬영 행위 추적 가능

### A.3.3 vibrator_manager

**Android 공식 근거**:
- 출처: [android.os.VibratorManager API Reference](https://developer.android.com/reference/android/os/VibratorManager)
- API 레벨: Android 12+ (API Level 31)

**주요 기능**:
- 시스템 진동 서비스 관리
- 햅틱 피드백 제어
- 커스텀 진동 패턴 생성

**카메라 관련성**: 카메라 촬영 시 셔터 햅틱 피드백 및 포커스 피드백 패턴 추적 가능

## A.4 1단계 최종 후보 로그 풀 (17개)

### A.4.1 후보 로그 목록

**총 17개 후보 로그**:
- **선행연구 검증 (15개)**: activity, usagestats, wifi, package, audio, bluetooth_manager, media.camera, meminfo, appops, power, account, window, cpuinfo, diskstats, iphonesubinfo
- **공식문서 기반 (2개)**: media.metrics, vibrator_manager

### A.4.2 후보 로그별 기본 정보

**카메라 직접 관련 (4개)**:
- `media.camera`: 카메라 서비스 활성화/비활성화 추적
- `audio`: 오디오 녹음 서비스 활성화 추적  
- `media.metrics`: 미디어 녹화 세션 메트릭 수집
- `vibrator_manager`: 카메라 관련 햅틱 피드백 추적

**앱 실행/사용 패턴 (5개)**:
- `activity`: 앱 생명주기 및 Recent Tasks 관리
- `usagestats`: 앱 사용 통계 및 액티비티 추적
- `package`: 앱 설치/삭제 이력 및 권한 정보
- `appops`: 앱별 권한 사용 이력
- `wifi`: WiFi 연결 상태 및 화면 활동

**시스템 상태/환경 (8개)**:
- `bluetooth_manager`: 블루투스 연결 관리
- `power`: 전원 관리 및 화면 상태
- `meminfo`: 메모리 사용량 정보
- `account`: 디바이스 계정 정보
- `window`: 윈도우 관리
- `cpuinfo`: CPU 정보
- `diskstats`: 디스크 통계
- `iphonesubinfo`: 디바이스 ID 정보

## A.5 참고문헌

**국내 선행연구 (15편)**:
- 김수영(2023): "ADB 이용 안드로이드 앱 제어 기반 아티팩트 획득 모델 연구", 성균관대학교
- 강예지(2022): "Analysis on android usagestats for digital investigation", 고려대학교
- 강예지(2021): "안드로이드 UsageStats의 포렌식 활용 방안", Journal of Digital Forensics
- 권혁철(2024): "Android 진단 로그 포렌식 분석 및 정규화 방안 연구", 고려대학교
- 최슬기(2015): "디지털 포렌식을 위한 안드로이드 디바이스 사전 분석 기법", 순천향대학교
- 이지원(2024): "모바일 포렌식을 통한 이미지 조작 행위 탐지에 관한 연구", 동국대학교
- 김승규(2020a): "불법촬영 범죄 대응을 위한 현장용 모바일 포렌식 도구 개발에 관한 연구", Journal of Digital Forensics
- 김승규(2020b): "휴대용 모바일 포렌식 도구의 활용방안에 관한 연구 - 몰래카메라 범죄를 중심으로", 성균관대학교
- 김종만(2018): "불법 촬영물에 대한 디지털 포렌식 프레임워크에 관한 연구", 고려대학교
- 이경률(2017): "스마트폰 포렌식 및 증거 수집방안 동향 분석", The Journal of Korean Institute of Communications and Information Sciences
- 조재형(2017): "안드로이드 기반 스마트폰 비휘발성 시스템 로그에 대한 분석", Journal of Digital Forensics
- 임상우(2013): "안드로이드 기반 스마트폰의 디지털 포렌식 분석에 관한 연구", 연세대학교
- 안원석(2025): "안드로이드 로그를 활용한 촬영 및 음성 녹음 탐지 방안 연구", 디지털포렌식연구
- 조형철(2024): "안드로이드 스마트폰의 진단데이터 분석", 고려대학교
- 정승태(2024): "안드로이드의 Sysdump 포렌식에 관한 연구", 성균관대학교

**해외 선행연구 (2편)**:
- Bortnik, L.(2013): "Android forensics- Automated data collection and reporting", Digital Investigation
- Bortnik, L. & Lavrenovs, A.(2020): "Android-Dumpsys-Analysis-to-Indicate-Driver-Distraction", NATO 협력 사이버 방어 센터

**Android 공식문서**:
- Android Developers: "android.media.metrics Package Summary", https://developer.android.com/reference/android/media/metrics/package-summary
- Android Developers: "VibratorManager API Reference", https://developer.android.com/reference/android/os/VibratorManager
