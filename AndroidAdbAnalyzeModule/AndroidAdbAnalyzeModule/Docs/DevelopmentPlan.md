# Android ADB Log Analyzer - 개발 계획서

## 프로젝트 개요
ADB dumpsys 로그를 **파싱하고 전처리**하여 **NormalizedLogEvent 형태로 변환**하는 C# .NET 8 라이브러리

**이 DLL의 책임:**
- 로그 파일 파싱 (SectionSplitter, RegexLineParser)
- 데이터 전처리 (타임스탬프 정규화, 필드 변환)
- 정규화된 이벤트 저장 (InMemory/DB Repository)

**상위 애플리케이션 책임:**
- 상관관계 분석 (여러 이벤트 간 관계 분석)
- 이벤트 감지 (카메라 촬영, 앱 실행 등)
- 타임라인 생성
- 클러스터링
- UI 표시

---

## 핵심 요구사항

### 1. 외부 설정 기반 파싱
- ✅ YAML 설정 파일 사용
- ✅ 실행 파일과 동일 위치에 설정 파일 배치
- ✅ 잘못된 설정 시 예외 발생
- ✅ 수동 재로드 지원 (작업 완료 후)

### 2. 로그 파싱
- ✅ 10MB 미만 파일 전체 로드 방식
- ✅ 실시간 처리 요구
- ✅ 섹션 기반 파싱
- ✅ Regex 패턴 매칭
- ⬜ 커스텀 파서 (복잡한 패턴용)

### 3. 데이터 전처리/정규화
- ✅ 디바이스 TimeZone과 현재 시간을 전달받아 연도/UTC 설정
- ✅ 시계열 정렬
- ✅ 필드 정규화

### 4. 저장소
- ✅ 초기: InMemory 구현
- ✅ 인터페이스 추상화 (ILogEventRepository)
- ⬜ 추후: SQLite + 트랜잭션

### 5. 플러그인
- ⬜ 인터페이스 정의 (ICustomLogParser)
- ⬜ 복잡한 패턴은 하드코딩으로 우선 처리
- ⬜ AssemblyLoadContext 사용 (추후)

### 6. 버전 관리
- ✅ 설정 파일 스키마 버전 관리
- ✅ 멀티 Android 버전 지원 (SupportedVersions)
- ✅ 안드로이드 버전/제조사별 설정 분리 (네이밍 규칙)
- ⏸️ 버전별 마이그레이터 구현 (보류)

### 7. 다양한 로그 타입 지원
- ✅ audio, vibrator, usagestats, camera_worker, activity, media.camera, media.metrics 로그 지원
- ✅ 로그 타입별 설정 파일 분리 (7개 설정 파일)
- ✅ 공통 파싱 로직 재사용 (RegexLineParser)

### 8. 코딩 원칙
- ✅ 객체지향, 캡슐화
- ✅ 불변성 (외부에서 상태 변경 불가)
- ✅ 유지보수성
- ✅ **오버 엔지니어링 절대 금지**
- ✅ 단순하고 명확한 구조

---

## Phase별 작업 계획

### Phase 1: 프로젝트 기본 구조 설정 ✅
- ✅ NuGet 패키지 추가 (YamlDotNet 16.2.1, Microsoft.Extensions.Logging.Abstractions 8.0.0)
- ✅ 폴더 구조 생성 (Core, Configuration, Parsing, Preprocessing, Repositories, Plugins)
- ✅ 핵심 모델 클래스 정의 (7개)
  - ✅ DeviceInfo (디바이스 정보)
  - ✅ LogParsingOptions (파싱 옵션)
  - ✅ NormalizedLogEvent (정규화된 이벤트)
  - ✅ ParsedLogEntry (파싱된 중간 결과)
  - ✅ ParsingError (에러 정보)
  - ✅ ParsingStatistics (통계)
  - ✅ ParsingResult (최종 결과)
- ✅ 핵심 인터페이스 정의 (5개 + 보조 클래스 4개)
  - ✅ ILogParser
  - ✅ ILogEventRepository
  - ✅ IConfigurationLoader<TConfig>
  - ✅ ISectionSplitter (+ SectionDefinition, LogSection)
  - ✅ ILineParser (+ ParsingContext)
  - ✅ ConfigurationChangedEventArgs
- ✅ 커스텀 예외 클래스 정의 (6개)
  - ✅ ConfigurationException (기본 클래스)
  - ✅ ConfigurationNotFoundException
  - ✅ ConfigurationValidationException
  - ✅ ConfigurationLoadException
  - ✅ ParsingException (기본 클래스)
  - ✅ LogFileTooLargeException
  - ✅ CriticalParsingException

---

### Phase 2: YAML 로더 & 섹션 분리 ✅
- ✅ Configuration 모델 클래스 (14개)
  - ✅ LogConfiguration (루트 모델)
  - ✅ ConfigMetadata
  - ✅ GlobalSettings
  - ✅ PerformanceSettings
  - ✅ ErrorHandlingSettings
  - ✅ SectionConfig
  - ✅ ParserConfig
  - ✅ LinePatternConfig
  - ✅ FieldDefinition
  - ✅ CorrelationRuleConfig (스텁)
  - ✅ PreprocessingConfig (스텁)
  - ✅ LoggingConfig
- ✅ YamlConfigurationLoader 구현
  - ✅ YamlDotNet으로 YAML 파싱
  - ✅ 동기/비동기 로드 지원
  - ✅ 설정 재로드 지원
  - ✅ 설정 변경 이벤트
- ✅ ConfigurationValidator 구현
  - ✅ 필수 필드 검증
  - ✅ Regex 문법 체크
  - ✅ ID 중복 체크
  - ✅ 논리적 일관성 검증 (섹션-파서 참조)
  - ✅ 성능 설정 검증
- ✅ LogSectionSplitter 구현
  - ✅ text/regex 마커 지원
  - ✅ 섹션별 라인 추출
  - ✅ 여러 섹션 동시 처리
- ✅ RegexLineParser 구현
  - ✅ Regex 패턴 매칭
  - ✅ 그룹 추출
  - ✅ 타입 변환 (string, int, long, double, bool, hex, datetime)
  - ✅ ParsedLogEntry 생성

---

### Phase 3: 정규화 & 저장소 ✅
- ✅ TimestampNormalizer 구현
  - ✅ 로그 타임스탬프 파싱 (6가지 포맷 지원)
  - ✅ 디바이스 TimeZone 정보 활용
  - ✅ UTC 변환
  - ✅ 연도 정보 보완 (MM-dd 포맷용)
- ✅ InMemoryLogEventRepository 구현
  - ✅ SaveEventAsync, SaveEventsAsync
  - ✅ GetEventsByTimeRangeAsync
  - ✅ GetRelatedEventsAsync (시간 윈도우 기반)
  - ✅ 스레드 안전성 (ReaderWriterLockSlim)
  - ✅ IDisposable 패턴
- ✅ 시계열 정렬 로직 (ascending/descending/none)
- ✅ ILogParser 통합 구현 (AdbLogParser)
  - ✅ SectionSplitter → LineParser → Normalizer → NormalizedLogEvent
  - ✅ 에러 처리 (라인별 try-catch)
  - ✅ 통계 생성 (ParsingStatistics)

---

### Phase 4: 에러 처리 & 로깅 ✅
- ✅ 라인 파싱 예외 처리 (Phase 3에서 이미 구현)
  - ✅ Try-Catch로 각 라인 보호
  - ✅ ParsingError 객체 생성
  - ✅ 에러 로깅
- ✅ ParsingStatistics 집계 (Phase 3에서 이미 구현)
  - ✅ TotalLines, ParsedLines, SkippedLines, ErrorLines
  - ✅ EventTypeCounts
  - ✅ ElapsedTime
- ✅ 로깅 프레임워크 통합
  - ✅ Microsoft.Extensions.Logging 사용
  - ✅ 로그 레벨별 출력 (Information, Debug, Warning, Error)
  - ✅ AdbLogParser 로깅 (파싱 흐름, 에러)
  - ✅ LogSectionSplitter 로깅 (섹션 분리)
  - ✅ YamlConfigurationLoader 로깅 (설정 로드)
  - ✅ ConfigurationValidator 로깅 (설정 검증)

---

### Phase 5: 설정 버전 관리 & 검증 ✅
- ✅ ConfigMetadata 개선
  - ✅ Version → SupportedVersions 변경 (멀티 버전 지원)
  - ✅ "*" 와일드카드 지원 (모든 버전)
  - ✅ 버전 범위 지정 가능 (예: ["11", "12", "14", "15"])
- ✅ 설정 스키마 버전 검증
  - ✅ ConfigSchemaVersion 검증 (엄격한 검증)
  - ✅ 지원되지 않는 버전 시 예외 발생
  - ✅ 명확한 에러 메시지
- ✅ 디바이스 버전 호환성 검증
  - ✅ ValidateDeviceCompatibility() 구현
  - ✅ DeviceInfo.AndroidVersion vs SupportedVersions 검증
  - ✅ 호환되지 않는 버전 시 예외 발생
  - ✅ AdbLogParser에서 파싱 전 검증
- ✅ Reload() 코드 리뷰
  - ✅ 동시 재로드 방지 (volatile flag)
  - ✅ 스레드 안전성 (lock)
  - ✅ 이벤트 발생 (ConfigurationChanged)
  - ✅ 예외 처리 (try-finally)
- ⏸️ ConfigurationMigrationService (보류 → Phase 7 이후)

---

### Phase 6: 다른 로그 타입 추가 ✅
- ✅ 새 로그 타입별 설정 파일 작성 (7개)
  - ✅ adb_audio_config.yaml (audio.txt - dumpsys media.audio_flinger)
  - ✅ adb_vibrator_config.yaml (vibrator_manager.txt - dumpsys vibrator_manager)
  - ✅ adb_usagestats_config.yaml (usagestats.txt - dumpsys usagestats)
  - ✅ adb_media_camera_worker_config.yaml (media.camera.worker.txt - camera lifecycle)
  - ✅ adb_activity_config.yaml (activity.txt - dumpsys activity)
  - ✅ adb_media_camera_config.yaml (media.camera.txt - camera connect/disconnect)
  - ✅ adb_media_metrics_config.yaml (media.metrics.txt - media extractor/audio track)
- ✅ 통합 테스트 작성 (47개 테스트)
  - ✅ AdbLogParserEndToEndTests (기본 + 에러 케이스)
  - ✅ ActivityLogParserTests (URI permissions, activity starter)
  - ✅ MediaCameraLogParserTests (camera connect/disconnect)
  - ✅ MediaMetricsLogParserTests (extractor/audio.track events)
- ⬜ 커스텀 파서 구현 (복잡한 패턴 필요시 구현)
  - ⬜ 하드코딩 방식으로 구현
  - ⬜ ICustomLogParser 인터페이스 (추후 필요 시)

---

### Phase 7: 플러그인 시스템 (필요 시)
- ⬜ ICustomLogParser 인터페이스 정의
- ⬜ ILogTypeAdapter 인터페이스 정의
- ⬜ PluginManager 기본 구조
- ⬜ AssemblyLoadContext 기반 로드
- ⬜ 플러그인 설정 스키마 정의

---

### Phase 8: 통합 테스트 & 검증 ✅
- ✅ End-to-End 테스트 (47개 테스트 통과)
  - ✅ audio.txt 로그 파싱 테스트 (5개 테스트)
  - ✅ vibrator_manager.txt 파싱 테스트 (3개 테스트)
  - ✅ usagestats.txt 파싱 테스트 (5개 테스트)
  - ✅ media.camera.worker.txt 파싱 테스트 (5개 테스트)
  - ✅ activity.txt 파싱 테스트 (5개 테스트)
  - ✅ media.camera.txt 파싱 테스트 (5개 테스트)
  - ✅ media.metrics.txt 파싱 테스트 (6개 테스트)
  - ✅ NormalizedLogEvent 생성 확인
  - ✅ 타임스탬프 정규화 검증 (8가지 포맷)
  - ✅ 속성 추출 정확성 검증
- ✅ 에러 케이스 테스트 (13개 테스트)
  - ✅ 잘못된 설정 파일 (YAML 문법 오류)
  - ✅ 필수 필드 누락 (sections, configSchemaVersion)
  - ✅ 지원하지 않는 스키마 버전
  - ✅ 호환되지 않는 안드로이드 버전
  - ✅ 와일드카드 버전 지원 검증
  - ✅ 잘못된 Regex 패턴
  - ✅ 존재하지 않는 로그 파일
  - ✅ 빈 로그 파일
  - ✅ 파일 크기 초과 (MaxFileSizeMB)
- ⬜ 성능 테스트
  - ⬜ 10MB 파일 처리 시간 측정
  - ⬜ 메모리 사용량 체크
  - ⬜ RegexLineParser 캐싱 효과 측정

---

### Phase 9: API 문서화
- ⬜ XML 주석 작성
- ⬜ 사용 예제 코드 작성
- ⬜ README.md 작성
- ⬜ 설정 파일 가이드 작성

---

## 진행 상황 추적

### 완료된 작업
- ✅ 요구사항 분석 및 설계
- ✅ 개발 계획 수립
- ✅ YAML 설정 파일 샘플 작성
- ✅ 플러그인 아키텍처 설계
- ✅ 문서 구조 정리
- ✅ AI 개발 가이드라인 작성
- ✅ **Phase 1 완료 및 검증**: 
  - ✅ 프로젝트 기본 구조 (14개 파일)
  - ✅ 모델 클래스 (7개)
  - ✅ 인터페이스 (5개 + 보조 4개)
  - ✅ 예외 클래스 (6개)
  - ✅ 가이드라인 100% 준수 확인
  - ✅ 코드 리뷰 및 수정 완료
- ✅ **Phase 2 완료**:
  - ✅ Configuration 모델 (13개 클래스)
  - ✅ YamlConfigurationLoader (YAML 파싱, 재로드, 이벤트)
  - ✅ ConfigurationValidator (완전한 검증 로직)
  - ✅ LogSectionSplitter (text/regex 마커)
  - ✅ RegexLineParser (타입 변환 지원)
- ✅ **Phase 3 완료**:
  - ✅ TimestampNormalizer (8가지 포맷 지원, UTC 변환)
  - ✅ InMemoryLogEventRepository (스레드 안전)
  - ✅ AdbLogParser (전체 파이프라인)
- ✅ **Phase 4 완료**:
  - ✅ 로깅 프레임워크 통합 (4개 클래스)
  - ✅ 에러 처리 강화 (라인별 try-catch, ParsingError)
  - ✅ 통계 집계 (ParsingStatistics, 성능 측정)
- ✅ **Phase 5 완료**:
  - ✅ ConfigMetadata.SupportedVersions (멀티 버전 지원)
  - ✅ 설정 스키마 버전 검증 (엄격한 검증)
  - ✅ 디바이스 버전 호환성 검증
  - ✅ Reload() 검증 완료
  - ✅ YamlDotNet 역직렬화 문제 해결
- ✅ **Phase 6 완료**:
  - ✅ 7개 로그 타입 설정 파일 작성 (audio, vibrator, usagestats, camera_worker, activity, media.camera, media.metrics)
  - ✅ 통합 테스트 클래스 작성 (4개 파일, 47개 테스트)
  - ✅ RegexLineParser 캐싱 최적화 (성능 개선)
  - ✅ 한글 타임스탬프 포맷 지원 (activity.txt)
- ✅ **Phase 8 완료**:
  - ✅ End-to-End 테스트 (34개 테스트)
  - ✅ 에러 케이스 테스트 (13개 테스트)
  - ✅ 전체 테스트 통과 (47/47)

### 현재 작업
- ✅ **Phase 6, 8 완료**:
  - ✅ 7개 로그 타입 지원 (audio, vibrator, usagestats, camera_worker, activity, media.camera, media.metrics)
  - ✅ 47개 통합 테스트 통과 (End-to-End + 에러 케이스)
  - ✅ RegexLineParser 캐싱 최적화
  - ✅ 8가지 타임스탬프 포맷 지원 (한글 포맷 포함)
  - ✅ Linter 에러: 0개
  - ✅ 코드 리뷰 통과

### 다음 단계
- **Phase 9**: API 문서화
  1. XML 주석 작성 (public API)
  2. 사용 예제 코드 작성
  3. README.md 작성
  4. 설정 파일 가이드 작성
  
**또는**

- **Phase 7**: 플러그인 시스템 (필요한 경우)
  1. ICustomLogParser 인터페이스 정의
  2. 하드코딩 커스텀 파서 구현 (복잡한 패턴용)
  
**Phase 9 (문서화) 권장** ✅

