# Plugin Architecture Design

## 개요
복잡한 로그 패턴을 처리하기 위한 플러그인 시스템 설계

## 설계 원칙
1. **단순성 우선**: 복잡한 패턴은 일단 하드코딩으로 구현
2. **필요시 플러그인화**: 반복되는 복잡한 패턴이 확인되면 플러그인으로 전환
3. **다양한 로그 타입 지원**: audio 외에도 battery, power, network 등 다양한 dumpsys 로그 처리
4. **격리 및 안정성**: 플러그인 오류가 전체 파싱에 영향을 주지 않도록

## 플러그인 인터페이스

### ICustomLogParser
커스텀 로직이 필요한 복잡한 패턴 처리용 인터페이스

**주요 메서드:**
- `CanParse()`: 해당 라인/섹션을 처리할 수 있는지 판단
- `Parse()`: 실제 파싱 수행
- `GetParserId()`: 파서 고유 식별자 반환
- `GetVersion()`: 파서 버전 정보

**사용 시나리오:**
- 여러 줄에 걸친 데이터 파싱 (예: Stream Volumes)
- 계층 구조 데이터 파싱 (예: Device Aliases)
- 조건부 파싱 로직 (예: 특정 마커 사이의 내용만)
- Regex로 표현 불가능한 복잡한 패턴

### ILogTypeAdapter
다양한 로그 타입(audio, battery, power 등)에 대한 공통 인터페이스

**주요 메서드:**
- `GetLogType()`: 로그 타입 식별자 반환 (예: "adb_audio", "adb_battery")
- `GetSupportedVersions()`: 지원하는 안드로이드 버전 목록
- `Preprocess()`: 로그 타입별 전처리 로직
- `Validate()`: 로그 파일이 해당 타입인지 검증

**사용 시나리오:**
- 로그 타입 자동 감지
- 로그 타입별 특수 처리
- 버전별 호환성 관리

## 플러그인 로드 메커니즘

### 로드 프로세스
1. `Plugins/` 디렉토리에서 DLL 스캔
2. AssemblyLoadContext를 사용하여 격리된 컨텍스트에서 로드
3. `ICustomLogParser` 또는 `ILogTypeAdapter` 구현체 검색
4. 파서 ID 중복 체크 후 등록
5. 버전 호환성 검증

### 플러그인 관리
- **동적 로드**: 런타임에 플러그인 추가 가능
- **Lazy Loading**: 필요한 시점에만 로드
- **예외 격리**: 플러그인 오류 발생 시 해당 플러그인만 비활성화
- **로깅**: 플러그인 로드/실행 과정 상세 로깅

## 플러그인 배포 구조

```
ExecutableDir/
├── AndroidAdbAnalyzeModule.dll
├── Configs/
│   ├── adb_audio_config.yaml
│   ├── adb_battery_config.yaml
│   └── adb_power_config.yaml
└── Plugins/
    ├── Core/                          # 기본 플러그인
    │   ├── StreamVolumeParser.dll
    │   └── DeviceAliasParser.dll
    └── Custom/                        # 사용자 정의 플러그인
        └── MyCustomParser.dll
```

## 설정 파일에서 플러그인 지정

### Regex 기반 파서 (기본)
```yaml
parsers:
  - id: "playback_parser"
    name: "Playback Events Parser"
    type: "regex"  # 기본 Regex 파서 사용
    targetSections: ["playback_activity"]
    linePatterns:
      - regex: "^(\\d{2}-\\d{2}...)..."
        fields: [...]
```

### 플러그인 기반 파서 (복잡한 패턴)
```yaml
parsers:
  - id: "stream_volume_parser"
    name: "Stream Volume Parser"
    type: "plugin"  # 플러그인 사용
    pluginAssembly: "Core/StreamVolumeParser.dll"
    targetSections: ["stream_volumes"]
    options:  # 플러그인별 설정
      includeAliases: true
      maxDepth: 3
```

### 하드코딩 파서 (내장)
```yaml
parsers:
  - id: "camera_capture_parser"
    name: "Camera Capture Parser"
    type: "builtin"  # 하드코딩된 내장 파서
    className: "AndroidAdbAnalyzeModule.Parsing.BuiltIn.CameraCaptureParser"
    targetSections: ["playback_activity"]
```

## 하드코딩 → 플러그인 전환 기준

### 하드코딩 유지
- 한 번만 사용되는 로직
- 간단한 패턴 (Regex로 충분)
- 핵심 기능 (상태 머신, 이벤트 상관분석)

### 플러그인화 고려
- 3회 이상 반복되는 복잡한 패턴
- 다른 로그 타입에서도 재사용 가능
- 사용자 커스터마이징 필요
- 외부 기여 가능성

## 다양한 로그 타입 지원 전략

### 로그 타입별 설정 분리
각 로그 타입마다 별도 YAML 설정 파일:
- `adb_audio_samsung_android15.yaml` (제조사+버전 포함)
- `adb_battery_samsung_android15.yaml`
- `adb_usagestats_samsung_android15.yaml`
- `adb_camera_worker_samsung_android15.yaml`

### 공통 처리 로직
- **SectionSplitter**: 모든 로그 타입 공통 사용
- **RegexLineParser**: 모든 로그 타입 공통 사용
- **Normalizer**: 로그 타입별 특수화 가능
- **Repository**: 모든 로그 타입 통합 저장

### 전처리 로직
- **TimestampNormalizer**: 다양한 타임스탬프 포맷 → UTC 변환
- **FieldNormalizer**: 필드 타입 변환 및 정규화 (추후 필요 시)
- **커스텀 파서**: 복잡한 패턴은 하드코딩 방식으로 구현

**주의:** 상관관계 분석 (piid 추적, 세션 그룹화, 이벤트 감지 등)은 이 DLL의 책임이 아니며, 상위 애플리케이션에서 NormalizedLogEvent 목록을 기반으로 수행합니다.

### 로그 타입 자동 감지
```yaml
# 각 설정 파일에 로그 식별 규칙 포함
metadata:
  logType: "adb_audio"
  detectionRules:
    - type: "content"
      pattern: "AudioService\\(\\)"
      minMatches: 1
    - type: "section"
      requiredSections: ["playback_activity", "focus_commands"]
```

## 안정성 고려사항

### 1. 플러그인 격리
- **AssemblyLoadContext** (.NET 8): 각 플러그인을 독립적인 컨텍스트에서 로드
- **언로드 가능**: 메모리 누수 방지를 위한 플러그인 언로드 지원
- **샌드박싱**: 파일 시스템 접근 제한 (읽기 전용)

### 2. 예외 처리
- 플러그인 로드 실패 → 경고 로그, 계속 진행
- 플러그인 파싱 실패 → 해당 라인 스킵, 에러 로깅
- 전체 파싱 중단 방지

### 3. 버전 호환성
- 플러그인 API 버전 체크
- 호환되지 않는 플러그인 자동 비활성화
- 플러그인 업데이트 시 마이그레이션 가이드 제공

### 4. 성능 고려
- 플러그인 호출 오버헤드 최소화
- 캐싱 전략 (동일 패턴 반복 시)
- 프로파일링 및 성능 모니터링

## 구현 우선순위

### 완료된 작업
- ✅ **Phase 1**: 프로젝트 기본 구조 (모델, 인터페이스, 예외)
- ✅ **Phase 2**: YAML 로더, 섹션 분리, Regex 파서

### 현재 작업 (Phase 3)
- 🔄 **파싱 & 전처리만 담당**
  - ✅ Regex 기반 파서 (Phase 2 완료)
  - ⬜ TimestampNormalizer (UTC 변환)
  - ⬜ InMemoryLogEventRepository (저장소)
  - ⬜ ILogParser 통합 (전체 파이프라인)
  - ❌ 상관관계 분석 제외 (상위 애플리케이션 책임)

### 추후 확장 (Phase 4+)
- ⬜ **다른 로그 타입 추가 시**:
  - ⬜ 로그 타입별 설정 파일 추가
  - ⬜ 커스텀 파서 구현 (복잡한 패턴)
  - ⬜ ICustomLogParser 인터페이스 (필요 시)

### 플러그인 시스템 (필요성 확인 후)
- ⬜ PluginManager 구현
- ⬜ AssemblyLoadContext 기반 로드
- ⬜ 플러그인 예제 작성

## 플러그인 개발 가이드

### 플러그인 작성 방법 (추후)
1. `ICustomLogParser` 인터페이스 구현
2. 독립 프로젝트로 개발 (DLL 출력)
3. `Plugins/` 디렉토리에 배치
4. 설정 파일에서 참조

### 플러그인 테스트
- 단위 테스트 프레임워크 제공
- 샘플 로그로 검증
- 성능 벤치마크

---

**중요:** 현재 단계에서는 플러그인 시스템의 **인터페이스만 정의**하고, 실제 구현은 필요성이 확인된 후 진행합니다.

