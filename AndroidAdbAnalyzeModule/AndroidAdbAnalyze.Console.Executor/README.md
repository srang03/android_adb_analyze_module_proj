# AndroidAdbAnalyze Console Executor

Android ADB 로그를 수집하고 분석하여 카메라 세션과 촬영 이벤트를 탐지하는 CLI 도구입니다.

## 🚀 빠른 시작

### 1. 사전 요구사항

- **.NET 8 Runtime** (실행 환경)
- **Android Platform Tools** (ADB 포함)
  - 다운로드: https://developer.android.com/tools/releases/platform-tools
  - ADB가 시스템 PATH에 등록되어 있어야 합니다.
- **Android 디바이스** (USB 또는 무선 디버깅으로 연결)

### 2. 디바이스 연결 확인

#### USB 연결
```bash
# 디바이스 USB 디버깅 활성화 필요
adb devices
```

#### 무선 디버깅 (Android 11+)
```bash
# 페어링 (최초 1회)
adb pair <IP>:<PAIR_PORT>

# 연결
adb connect <IP>:<PORT>

# 확인
adb devices
```

### 3. 로그 수집 및 분석 실행

#### 기본 실행
```bash
AndroidAdbAnalyze.Console.Executor.exe analyze
```

#### 고급 옵션
```bash
# 출력 디렉토리 지정
AndroidAdbAnalyze.Console.Executor.exe analyze --output-dir ./my-logs

# 시간 범위 필터링
AndroidAdbAnalyze.Console.Executor.exe analyze \
  --start-time 2025-10-18T10:00:00 \
  --end-time 2025-10-18T12:00:00

# 조용한 모드 (진행 상황 숨김)
AndroidAdbAnalyze.Console.Executor.exe analyze --quiet

# HTML 보고서 생성 안 함
AndroidAdbAnalyze.Console.Executor.exe analyze --no-html-report
```

## 📋 명령어 및 옵션

### 기본 명령어

```bash
# 도움말 표시
AndroidAdbAnalyze.Console.Executor.exe --help

# analyze 명령 도움말
AndroidAdbAnalyze.Console.Executor.exe analyze --help

# 버전 정보
AndroidAdbAnalyze.Console.Executor.exe --version
```

### analyze 명령 옵션

| 옵션 | 설명 | 기본값 |
|------|------|--------|
| `-o, --output-dir <path>` | 로그 출력 디렉토리 | `./logs` |
| `-s, --start-time <datetime>` | 분석 시작 시간 (ISO 8601) | 전체 로그 |
| `-e, --end-time <datetime>` | 분석 종료 시간 (ISO 8601) | 전체 로그 |
| `--no-html-report` | HTML 보고서 생성 안 함 | `false` |
| `-v, --verbose` | 상세 로그 출력 | `false` |
| `-q, --quiet` | 최소 로그 출력 | `false` |

### 종료 코드

| 코드 | 의미 |
|------|------|
| 0 | 성공 |
| 1 | ADB 실행 파일을 찾을 수 없음 |
| 2 | 연결된 디바이스 없음 |
| 3 | 다중 디바이스 연결됨 (단일 디바이스만 지원) |
| 4 | 필수 로그 수집 실패 |
| 5 | 로그 파싱 실패 |
| 6 | 분석 실패 |
| 7 | 잘못된 명령줄 인자 |
| 99 | 알 수 없는 오류 |

## 📁 출력 구조

```
./logs/  (또는 --output-dir 지정 경로)
├── device_info.json                # 디바이스 정보
├── collection_summary.txt          # 로그 수집 요약
├── raw_logs/                       # 수집된 원본 로그
│   ├── activity.log
│   ├── audio.log
│   ├── media.camera.log
│   ├── media.camera.worker.log
│   ├── media.metrics.log
│   ├── usagestats.log
│   └── vibrator_manager.log
├── analysis_result.json            # 분석 결과 (JSON)
├── report.html                     # HTML 보고서
└── errors.log                      # 오류 로그
```

## ⚙️ 설정 파일

### appsettings.json

```json
{
  "Adb": {
    "ExecutablePath": null,           // ADB 경로 (null이면 PATH에서 찾기)
    "CommandTimeout": 60,             // 명령 타임아웃 (초)
    "RetryCount": 3,                  // 재시도 횟수
    "RetryDelayMs": 1000              // 재시도 간격 (밀리초)
  },
  "LogCollection": {
    "OutputDirectory": "./logs",      // 기본 출력 디렉토리
    "Logs": [                         // 수집할 로그 목록
      {
        "Name": "activity",
        "DumpsysService": "activity",
        "Required": true,             // 필수 로그 여부
        "Timeout": 90                 // 개별 타임아웃 (초)
      },
      // ... 7개 로그 정의
    ]
  },
  "Analysis": {
    "MinConfidenceThreshold": 0.3,    // 최소 신뢰도 임계값
    "EventCorrelationWindowSeconds": 30,  // 이벤트 상관관계 윈도우
    "MaxSessionGapMinutes": 5,        // 세션 간 최대 간격
    "DeduplicationSimilarityThreshold": 0.8  // 중복 제거 유사도
  }
}
```

### 환경 변수

```bash
# ADB 경로 지정
export ADBANALYZE_Adb__ExecutablePath="/path/to/adb"

# 출력 디렉토리 지정
export ADBANALYZE_LogCollection__OutputDirectory="/my/logs"
```

## 🔧 문제 해결

### ADB를 찾을 수 없음

```
[오류] ADB 실행 파일을 찾을 수 없습니다.

해결 방법:
1. Android Platform Tools를 설치하고 ADB가 시스템 PATH에 추가되었는지 확인
2. appsettings.json에서 Adb.ExecutablePath를 직접 지정
3. 환경 변수 ADBANALYZE_Adb__ExecutablePath 설정
```

### 연결된 디바이스 없음

```
[오류] 연결된 ADB 디바이스가 없습니다.

확인 사항:
1. USB 케이블이 올바르게 연결되었는지 확인
2. Android 디바이스의 'USB 디버깅'이 활성화되었는지 확인
3. 디바이스 화면의 'USB 디버깅 허용' 팝업에서 '허용' 선택
4. 'adb devices' 명령어로 디바이스 인식 여부 확인

무선 디버깅:
1. 'adb pair <IP>:<PORT>' 명령어로 페어링 완료 확인
2. 'adb connect <IP>:<PORT>' 명령어로 연결 확인
```

### 다중 디바이스 연결됨

```
[오류] 여러 ADB 디바이스가 연결되어 있습니다.

해결 방법:
하나의 디바이스만 연결 상태로 유지하세요.
(향후 버전에서 --device-serial 옵션이 추가될 예정입니다.)
```

## 📊 실행 예시

### 1. 기본 실행

```bash
$ AndroidAdbAnalyze.Console.Executor.exe analyze

[23:59:00 INF] === AndroidAdbAnalyze Console Executor 시작 ===
[23:59:00 INF] === analyze 명령 시작 ===
[진행] 디바이스 연결 확인 중...
[진행] 디바이스 연결됨: 1234567890
[진행] 로그 수집 중...
[진행] 로그 파싱 중...
[진행] 로그 분석 중...
[진행] 파이프라인 실행 완료!

=== 분석 완료 ===
세션: 5개
촬영 이벤트: 12개
실행 시간: 45.32초
출력 디렉토리: ./logs

[23:59:45 INF] === AndroidAdbAnalyze Console Executor 종료 (ExitCode: 0) ===
```

### 2. 시간 범위 필터링

```bash
$ AndroidAdbAnalyze.Console.Executor.exe analyze \
  --start-time "2025-10-18T10:00:00" \
  --end-time "2025-10-18T12:00:00" \
  --output-dir "./analysis-results"

=== 분석 완료 ===
세션: 2개
촬영 이벤트: 5개
실행 시간: 38.15초
출력 디렉토리: ./analysis-results
```

## 🧪 수집되는 로그

| 로그 이름 | dumpsys 서비스 | 필수 여부 | 용도 |
|----------|---------------|---------|------|
| activity | activity | ✅ 필수 | 앱 활동 추적 |
| audio | audio | ✅ 필수 | 오디오 이벤트 탐지 |
| media.camera | media.camera | ✅ 필수 | 카메라 API 사용 |
| media.camera.worker | media.camera.worker | 선택 | Samsung 전용 DB INSERT |
| media.metrics | media.metrics | ✅ 필수 | 미디어 메트릭 |
| usagestats | usagestats | ✅ 필수 | 앱 사용 통계 |
| vibrator_manager | vibrator_manager | ✅ 필수 | 진동 이벤트 |

**참고**: `media.camera.worker`는 Samsung 디바이스 전용이며, 없어도 분석은 계속 진행됩니다.

## 📄 라이선스

이 프로젝트는 연구 목적으로 개발되었습니다.

## 🙋 지원

문제가 발생하면 다음 로그 파일을 확인하세요:
- `logs/executor-<날짜>.txt` - 실행 로그
- `<output-dir>/errors.log` - 오류 로그

