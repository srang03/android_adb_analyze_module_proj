# 설정 파일 작성 가이드

## 개요

이 문서는 YAML 설정 파일을 작성하여 새로운 로그 타입을 파싱하는 방법을 설명합니다.

---

## 기본 구조

```yaml
# 필수: 설정 파일 스키마 버전
configSchemaVersion: "1.0"

# 필수: 메타데이터
metadata:
  logType: "adb_audio"                        # 로그 타입 식별자
  displayName: "ADB Audio Log Parser"        # 표시 이름
  description: "Parses dumpsys media.audio_flinger logs"
  author: "Your Name"
  supportedVersions: ["15"]                   # 지원 안드로이드 버전

# 필수: 파일 매칭 패턴 (문서화용)
filePatterns:
  - "audio.txt"
  - "media.audio_flinger.txt"

# 필수: 글로벌 설정
globalSettings:
  timestampFormat: "MM-dd HH:mm:ss':'fff"     # 타임스탬프 포맷
  timeSeriesOrder: "ascending"                # ascending | descending | none

# 선택: 성능 설정
performance:
  maxFileSizeMB: 500
  timeoutSeconds: 300

# 선택: 에러 처리 설정
errorHandling:
  onInvalidLine: "skip"                       # skip | abort | log
  onMissingTimestamp: "skip"
  onParsingError: "log"

# 필수: 섹션 정의
sections:
  - id: "players_section"
    name: "Players Section"
    enabled: true
    startMarker: "Players:"
    endMarker: "^Hardware"
    markerType: "text"                        # text | regex

# 필수: 파서 정의
parsers:
  - id: "audio_parser"
    name: "Audio Parser"
    enabled: true
    targetSections: ["players_section"]
    priority: 0
    linePatterns:
      - id: "new_player_pattern"
        name: "New Player Pattern"
        eventType: "PLAYER_CREATED"
        regex: "new player piid:(\\d+) uid:(\\d+)"
        fields:
          piid:
            group: 1
            type: "int"
          uid:
            group: 2
            type: "int"
```

---

## 필드 상세 설명

### 1. `configSchemaVersion` (필수)

**현재 지원 버전**: `"1.0"`

```yaml
configSchemaVersion: "1.0"
```

- 설정 파일의 스키마 버전
- 지원되지 않는 버전은 로드 시 예외 발생
- 향후 스키마 변경 시 버전 업데이트 및 마이그레이션 필요

---

### 2. `metadata` (필수)

```yaml
metadata:
  logType: "adb_audio"                  # 로그 타입 식별자 (자유롭게 정의)
  displayName: "ADB Audio Log Parser"  # 사용자에게 표시될 이름
  description: "Parses audio logs"     # 설명
  author: "Your Name"                  # 작성자 (선택)
  supportedVersions: ["15"]            # 지원하는 안드로이드 버전
```

#### `supportedVersions`

**옵션 1: 특정 버전 명시**
```yaml
supportedVersions: ["11", "12", "14", "15"]
```

**옵션 2: 모든 버전 지원**
```yaml
supportedVersions: ["*"]
```

- 파싱 시 `DeviceInfo.AndroidVersion`과 비교
- 호환되지 않는 버전은 예외 발생

---

### 3. `filePatterns` (필수)

```yaml
filePatterns:
  - "audio.txt"
  - "media.audio_flinger.txt"
  - "*.audio"
```

- 이 설정이 처리할 로그 파일 패턴
- **현재는 문서화 용도로만 사용** (자동 매핑 미지원)
- 사용자가 로그 파일과 설정을 수동으로 매핑

---

### 4. `globalSettings` (필수)

```yaml
globalSettings:
  encoding: "utf-8"
  skipEmptyLines: true
  skipComments: false
  commentPrefix: "#"
  timeSeriesOrder: "ascending"  # ascending, descending, none
  timestampFormat: "MM-dd HH:mm:ss':'fff"
```

#### `encoding`
- 로그 파일의 인코딩 (기본값: `"utf-8"`)

#### `skipEmptyLines`
- 빈 라인 스킵 여부 (기본값: `true`)

#### `skipComments`
- 주석 라인 스킵 여부 (기본값: `false`)

#### `commentPrefix`
- 주석 접두사 (기본값: `"#"`)

#### `timestampFormat`

**지원하는 포맷:**
1. `MM-dd HH:mm:ss:fff` - Audio (예: `09-04 15:08:25:404`)
2. `MM-dd HH:mm:ss.fff` - Vibrator (예: `09-04 15:08:25.404`)
3. `yyyy-MM-dd HH:mm:ss.fff zzz` - Camera Worker (예: `2025-09-04 15:08:25.432 +0900`)
4. `yyyy-MM-dd HH:mm:ss` - UsageStats, Activity
5. `yyyy-MM-dd HH:mm:ss.fff` - Generic with milliseconds
6. `MM-dd HH:mm:ss` - Without milliseconds

> **참고**: 위에 명시된 6가지 형식 외에도, `.NET`의 기본 `DateTime.Parse` 메서드가 폴백(Fallback)으로 사용됩니다. 따라서 일부 표준 시간 형식(예: ISO 8601)은 추가 설정 없이 파싱될 수 있습니다.

**주의사항:**
- `:` (콜론) 사용 시 `':'` 처럼 이스케이프 필요
- 연도 정보가 없는 경우 `DeviceInfo.CurrentTime`의 연도 사용

#### `timeSeriesOrder`

- `ascending`: 시계열 오름차순 (과거 → 현재)
- `descending`: 시계열 내림차순 (현재 → 과거)
- `none`: 정렬 안 함

---

### 5. `sections` (필수)

```yaml
sections:
  - id: "players_section"              # 섹션 고유 ID
    name: "Players Section"            # 섹션 이름
    enabled: true                      # 활성화 여부
    startMarker: "Players:"            # 시작 마커
    endMarker: "^Hardware"             # 종료 마커
    markerType: "text"                 # text | regex
```

#### `markerType`

**`text` (텍스트 매칭):**
```yaml
startMarker: "Players:"
markerType: "text"
```
- 라인이 정확히 `"Players:"`를 포함하면 매칭

**`regex` (정규식 매칭):**
```yaml
startMarker: "^Players:"
markerType: "regex"
```
- 라인이 정규식 `^Players:`에 매칭되면 시작

#### 복수 섹션 정의

```yaml
sections:
  - id: "section1"
    startMarker: "START1"
    endMarker: "END1"
  
  - id: "section2"
    startMarker: "START2"
    endMarker: "END2"
```

---

### 6. `parsers` (필수)

```yaml
parsers:
  - id: "audio_parser"                 # 파서 고유 ID
    name: "Audio Parser"               # 파서 이름
    enabled: true                      # 활성화 여부
    targetSections: ["players_section"] # 대상 섹션 ID 목록
    priority: 0                        # 우선순위 (낮을수록 먼저 실행)
    linePatterns: [...]                # 라인 패턴 목록
```

#### `targetSections`

- 이 파서가 적용될 섹션 ID 목록
- 여러 섹션에 동일한 파서 적용 가능

```yaml
targetSections: ["section1", "section2", "section3"]
```

#### `priority`

- 여러 파서가 동일한 섹션을 대상으로 하는 경우 우선순위 지정
- **낮을수록 먼저 실행** (0이 가장 높은 우선순위)

---

### 7. `linePatterns` (필수)

```yaml
linePatterns:
  - id: "new_player_pattern"           # 패턴 고유 ID
    name: "New Player Pattern"         # 패턴 이름
    eventType: "PLAYER_CREATED"        # 이벤트 타입 (자유롭게 정의)
    regex: "new player piid:(\\d+) uid:(\\d+)"  # 정규식
    fields:                            # 추출할 필드
      piid:
        group: 1                       # Regex 그룹 번호
        type: "int"                    # 필드 타입
      uid:
        group: 2
        type: "int"
```

#### `eventType`

- 이 패턴이 추출하는 이벤트의 타입
- **자유롭게 정의 가능** (하드코딩 아님)
- 결과: `NormalizedLogEvent.EventType`

**예시:**
```yaml
eventType: "PLAYER_CREATED"
eventType: "CAMERA_OPENED"
eventType: "VIBRATION_STARTED"
eventType: "MY_CUSTOM_EVENT"
```

#### `regex`

- 표준 .NET 정규식 (Regex)
- 그룹 `(...)` 사용하여 필드 추출

**예시:**
```yaml
# 간단한 패턴
regex: "new player piid:(\\d+)"

# 복잡한 패턴
regex: "^(\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}:\\d{3}).*new player piid:(\\d+) uid:(\\d+) package:(\\S+)"
```

**주의사항:**
- YAML에서 `\` (백슬래시)는 `\\`로 이스케이프
- 예: `\d` → `\\d`, `\s` → `\\s`

#### `fields`

```yaml
fields:
  fieldName:
    group: 1                 # Regex 그룹 번호 (1부터 시작)
    type: "string"           # 필드 타입
    format: "optional"       # datetime 타입용 포맷 (선택)
```

**지원 타입:**

| 타입 | 설명 | 예시 |
|------|------|------|
| `string` | 문자열 (기본값) | `"hello"` |
| `int` | 32비트 정수 | `12345` |
| `long` | 64비트 정수 | `9876543210` |
| `double` | 부동소수점 | `3.14` |
| `bool` | 불린 | `true`, `false`, `1`, `0` |
| `hex` | 16진수 → 10진수 | `0x1A` → `26` |
| `datetime` | 날짜/시간 | `2025-09-04` |

**예시:**
```yaml
fields:
  piid:
    group: 1
    type: "int"
  
  package:
    group: 2
    type: "string"
  
  timestamp:
    group: 3
    type: "datetime"
    format: "yyyy-MM-dd HH:mm:ss"
  
  enabled:
    group: 4
    type: "bool"
```

---

## 전체 예제

### 예제 1: Audio 로그

```yaml
configSchemaVersion: "1.0"

metadata:
  logType: "adb_audio"
  displayName: "ADB Audio Log Parser"
  description: "Parses dumpsys media.audio_flinger logs"
  supportedVersions: ["15"]

filePatterns:
  - "audio.txt"

globalSettings:
  timestampFormat: "MM-dd HH:mm:ss':'fff"
  timeSeriesOrder: "ascending"

sections:
  - id: "players_section"
    name: "Players Section"
    startMarker: "Players:"
    endMarker: "^Hardware"
    markerType: "text"

parsers:
  - id: "audio_parser"
    name: "Audio Parser"
    enabled: true
    targetSections: ["players_section"]
    linePatterns:
      - id: "new_player_pattern"
        name: "New Player Pattern"
        eventType: "PLAYER_CREATED"
        regex: "new player piid:(\\d+) uid:(\\d+) usage=(\\w+) content=(\\w+)"
        fields:
          piid:
            group: 1
            type: "int"
          uid:
            group: 2
            type: "int"
          usage:
            group: 3
            type: "string"
          contentType:
            group: 4
            type: "string"
      
      - id: "player_started_pattern"
        name: "Player Started Pattern"
        eventType: "PLAYER_STARTED"
        regex: "player event:started piid:(\\d+)"
        fields:
          piid:
            group: 1
            type: "int"
```

---

### 예제 2: 복수 섹션 & 복수 파서

```yaml
configSchemaVersion: "1.0"

metadata:
  logType: "adb_activity"
  displayName: "ADB Activity Log Parser"
  supportedVersions: ["*"]

filePatterns:
  - "activity.txt"

globalSettings:
  timestampFormat: "yyyy-MM-dd HH:mm:ss"
  timeSeriesOrder: "ascending"

sections:
  - id: "uri_permissions"
    name: "URI Permissions"
    startMarker: "ACTIVITY MANAGER URI PERMISSIONS"
    endMarker: "^ACTIVITY MANAGER"
    markerType: "regex"
  
  - id: "activity_starter"
    name: "Activity Starter"
    startMarker: "ACTIVITY MANAGER STARTER"
    endMarker: "^ACTIVITY MANAGER"
    markerType: "regex"

parsers:
  - id: "uri_parser"
    name: "URI Permission Parser"
    enabled: true
    targetSections: ["uri_permissions"]
    linePatterns:
      - id: "uri_grant"
        eventType: "URI_PERMISSION_GRANT"
        regex: "granted.*provider=(\\S+).*uri=(\\S+)"
        fields:
          provider:
            group: 1
            type: "string"
          uri:
            group: 2
            type: "string"
  
  - id: "starter_parser"
    name: "Activity Starter Parser"
    enabled: true
    targetSections: ["activity_starter"]
    linePatterns:
      - id: "start_activity"
        eventType: "ACTIVITY_STARTED"
        regex: "Starting activity.*component=(\\S+)"
        fields:
          component:
            group: 1
            type: "string"
```

---

## 새로운 로그 타입 추가 단계

### Step 1: 로그 구조 분석

```
1. 로그 파일 열어서 구조 파악
2. 섹션 나눌 수 있는 마커 찾기
3. 추출할 이벤트 타입 정의
4. Regex 패턴 작성
```

### Step 2: 설정 파일 작성

```yaml
configSchemaVersion: "1.0"

metadata:
  logType: "my_new_log"
  displayName: "My New Log Parser"
  supportedVersions: ["*"]

filePatterns:
  - "my_log.txt"

globalSettings:
  timestampFormat: "yyyy-MM-dd HH:mm:ss"
  timeSeriesOrder: "ascending"

sections:
  - id: "main_section"
    startMarker: "START"
    endMarker: "END"
    markerType: "text"

parsers:
  - id: "main_parser"
    targetSections: ["main_section"]
    linePatterns:
      - id: "event_pattern"
        eventType: "MY_EVENT"
        regex: "event: (\\w+)"
        fields:
          eventName:
            group: 1
            type: "string"
```

### Step 3: 테스트

```csharp
var config = await new YamlConfigurationLoader("my_new_log_config.yaml")
    .LoadAsync("my_new_log_config.yaml");

var parser = new AdbLogParser(config);
var result = await parser.ParseAsync("my_log.txt", options);

Assert.True(result.Success);
Assert.NotEmpty(result.Events);
```

---

## 문제 해결

### 문제 1: 타임스탬프 파싱 실패

**증상:**
```
Skipped Lines: 1000+
Success Rate: 0%
```

**해결:**
1. `globalSettings.timestampFormat` 확인
2. 로그 파일의 실제 타임스탬프 포맷과 일치하는지 확인
3. `:` (콜론) 이스케이프 (`':'`) 확인

### 문제 2: 섹션을 찾을 수 없음

**증상:**
```
Section Line Counts: {}
```

**해결:**
1. `startMarker`, `endMarker` 확인
2. `markerType` (`text` vs `regex`) 확인
3. 로그 파일에서 실제 마커 텍스트 확인

### 문제 3: Regex 패턴 매칭 실패

**증상:**
```
Parsed Lines: 0
Skipped Lines: 많음
```

**해결:**
1. 온라인 Regex 테스터 사용 (regex101.com)
2. `\\` 이스케이프 확인
3. 그룹 `(...)` 위치 확인
4. 로그 실제 텍스트와 패턴 비교

---

## 추가 참고

- [API 사용 가이드](API_Usage_Guide.md)
- [개발 계획](DevelopmentPlan.md)

**문서 버전**: 1.0  
**최종 업데이트**: 2025-10-04

