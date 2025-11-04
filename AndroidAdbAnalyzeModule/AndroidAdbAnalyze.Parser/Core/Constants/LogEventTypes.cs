namespace AndroidAdbAnalyze.Parser.Core.Constants;

/// <summary>
/// 로그 이벤트 타입 상수 정의
/// 모든 프로젝트에서 공통으로 사용하는 이벤트 타입 문자열을 중앙에서 관리합니다.
/// </summary>
/// <remarks>
/// 논문 연구 범위: 총 18개 EventType (13개 촬영 탐지용 + 5개 세션 탐지용)
/// 
/// 세션 탐지용 (5개):
/// - CAMERA_CONNECT, CAMERA_DISCONNECT (media.camera)
/// - ACTIVITY_RESUMED, ACTIVITY_PAUSED, ACTIVITY_STOPPED (usagestats)
/// 
/// 촬영 탐지용 (13개):
/// - 확정 핵심 (3개): DATABASE_INSERT, DATABASE_EVENT, SILENT_CAMERA_CAPTURE
/// - 조건부 핵심 (4개): VIBRATION_EVENT, PLAYER_EVENT, FOREGROUND_SERVICE, URI_PERMISSION_GRANT
/// - 보조 (6개): URI_PERMISSION_REVOKE, PLAYER_CREATED, SHUTTER_SOUND, MEDIA_EXTRACTOR, PLAYER_RELEASED, CAMERA_ACTIVITY_REFRESH
/// 
/// 테스트용 (논문 제외): ACTIVITY_LIFECYCLE (Obsolete)
/// </remarks>
public static class LogEventTypes
{
    // ============================================================
    // 세션 관련 이벤트 (Camera Session Management)
    // ============================================================
    
    /// <summary>
    /// 카메라 연결 이벤트 (세션 시작)
    /// <para>📄 로그: media.camera.txt, media.camera.worker.txt</para>
    /// <para>⚙️ YAML: adb_media_camera_config.yaml (camera_connect_pattern), adb_media_camera_worker_config.yaml (camera_connect_pattern)</para>
    /// <para>🎯 논문: 세션 탐지용 (1/5)</para>
    /// </summary>
    public const string CAMERA_CONNECT = "CAMERA_CONNECT";
    
    /// <summary>
    /// 카메라 연결 해제 이벤트 (세션 종료)
    /// <para>📄 로그: media.camera.txt, media.camera.worker.txt</para>
    /// <para>⚙️ YAML: adb_media_camera_config.yaml (camera_disconnect_pattern), adb_media_camera_worker_config.yaml (camera_disconnect_pattern)</para>
    /// <para>🎯 논문: 세션 탐지용 (2/5)</para>
    /// </summary>
    public const string CAMERA_DISCONNECT = "CAMERA_DISCONNECT";
    
    // ============================================================
    // 데이터베이스 관련 이벤트 (Database Operations)
    // ============================================================
    
    /// <summary>
    /// 데이터베이스 삽입 완료 이벤트 (촬영 확정!)
    /// <para>📄 로그: media.camera.worker.txt</para>
    /// <para>⚙️ YAML: adb_media_camera_worker_config.yaml (database_insert_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 확정 핵심 아티팩트 (1/13, 총점 8점, 가중치 0.5, D/E/R: 3+3+2)</para>
    /// <para>📱 적용 앱: 기본 카메라, 무음 카메라</para>
    /// </summary>
    public const string DATABASE_INSERT = "DATABASE_INSERT";
    
    /// <summary>
    /// 데이터베이스 이벤트 (일반)
    /// <para>⚠️ 주의: 코드에만 정의됨. YAML 파싱 패턴 없음</para>
    /// <para>🎯 논문: 촬영 탐지용 - 확정 핵심 아티팩트 (2/13, 총점 8점, 가중치 0.5, D/E/R: 3+3+2)</para>
    /// <para>📱 적용 앱: DATABASE_INSERT와 동일 역할 (DB 조작 변형 패턴)</para>
    /// </summary>
    public const string DATABASE_EVENT = "DATABASE_EVENT";
    
    /// <summary>
    /// 미디어 삽입 시작 이벤트
    /// <para>📄 로그: media.camera.worker.txt</para>
    /// <para>⚙️ YAML: adb_media_camera_worker_config.yaml (media_insert_start_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (2/9)</para>
    /// <para>📱 적용 앱: 기본 카메라, 무음 카메라</para>
    /// </summary>
    public const string MEDIA_INSERT_START = "MEDIA_INSERT_START";
    
    // MEDIA_INSERT_END는 DATABASE_INSERT로 통합됨 (2025-10-15)
    
    // ============================================================
    // 미디어 관련 이벤트 (Media & Audio)
    // ============================================================
    
    /// <summary>
    /// 오디오 트랙 이벤트
    /// <para>📄 로그: media.metrics.txt</para>
    /// <para>⚙️ YAML: adb_media_metrics_config.yaml (audio_track_event)</para>
    /// <para>🚫 논문: 연구 범위 제외 (MEDIA_EXTRACTOR로 충분)</para>
    /// </summary>
    public const string AUDIO_TRACK = "AUDIO_TRACK";
    
    /// <summary>
    /// 미디어 추출기 이벤트 (셔터 사운드 파일 추출)
    /// <para>📄 로그: media.metrics.txt</para>
    /// <para>⚙️ YAML: adb_media_metrics_config.yaml (extractor_event)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (11/13, 총점 4점, 가중치 0.2, D/E/R: 1+1+2)</para>
    /// <para>📱 적용 앱: 모든 앱</para>
    /// </summary>
    public const string MEDIA_EXTRACTOR = "MEDIA_EXTRACTOR";
    
    /// <summary>
    /// 셔터 사운드 이벤트
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (shutter_sound_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (10/13, 총점 4점, 가중치 0.2, D/E/R: 1+2+1)</para>
    /// <para>📱 적용 앱: 모든 앱 (로그 생성 불안정)</para>
    /// </summary>
    public const string SHUTTER_SOUND = "SHUTTER_SOUND";
    
    /// <summary>
    /// 진동 이벤트 (Vibrator Manager)
    /// <para>📄 로그: vibrator_manager.txt</para>
    /// <para>⚙️ YAML: adb_vibrator_config.yaml (vibration_event_pattern, vibration_event_step_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 조건부 핵심 아티팩트 (4/13, 총점 7점, 가중치 0.4, D/E/R: 2+2+3)</para>
    /// <para>📱 적용 앱: 모든 앱 (hapticType=50061 검증)</para>
    /// </summary>
    public const string VIBRATION_EVENT = "VIBRATION_EVENT";
    
    // ============================================================
    // 플레이어 관련 이벤트 (Audio Player)
    // ============================================================
    
    /// <summary>
    /// 플레이어 생성 이벤트
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (new_player_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (9/13, 총점 4점, 가중치 0.25, D/E/R: 1+2+1)</para>
    /// <para>📱 적용 앱: 기본 카메라, 카카오톡 (텔레그램 제외)</para>
    /// </summary>
    public const string PLAYER_CREATED = "PLAYER_CREATED";
    
    /// <summary>
    /// 플레이어 이벤트 (시작/일시정지 등)
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (player_event_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 조건부 핵심 아티팩트 (5/13, 총점 6점, 가중치 0.35, D/E/R: 2+2+2)</para>
    /// <para>📱 적용 앱: 기본 카메라, 카카오톡 (텔레그램 제외, tags=CAMERA 검증, 승격 2025-10-28)</para>
    /// </summary>
    public const string PLAYER_EVENT = "PLAYER_EVENT";
    
    /// <summary>
    /// 플레이어 해제 이벤트
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (player_release_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (12/13, 총점 3점, 가중치 0.15, D/E/R: 1+1+1)</para>
    /// <para>📱 적용 앱: 기본 카메라, 카카오톡 (텔레그램 제외)</para>
    /// </summary>
    public const string PLAYER_RELEASED = "PLAYER_RELEASED";
    
    // ============================================================
    // 오디오 포커스 관련 이벤트 (Audio Focus)
    // ============================================================
    
    /// <summary>
    /// 오디오 포커스 요청 이벤트
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (request_focus_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    public const string FOCUS_REQUESTED = "FOCUS_REQUESTED";
    
    /// <summary>
    /// 오디오 포커스 해제 이벤트
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (abandon_focus_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    public const string FOCUS_ABANDONED = "FOCUS_ABANDONED";
    
    // ============================================================
    // 녹음 관련 이벤트 (Recording)
    // ============================================================
    
    /// <summary>
    /// 녹음 이벤트
    /// <para>📄 로그: audio.txt</para>
    /// <para>⚙️ YAML: adb_audio_config.yaml (rec_update_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    public const string RECORDING_EVENT = "RECORDING_EVENT";
    
    // ============================================================
    // 권한 관련 이벤트 (Permissions)
    // ============================================================
    
    /// <summary>
    /// URI 권한 부여 이벤트
    /// <para>📄 로그: activity.txt</para>
    /// <para>⚙️ YAML: adb_activity_config.yaml (uri_grant_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 조건부 핵심 아티팩트 (6/13, 총점 5점, 가중치 0.3, D/E/R: 2+2+1)</para>
    /// <para>📱 적용 앱: 카카오톡 (임시 파일 경로 검증)</para>
    /// </summary>
    public const string URI_PERMISSION_GRANT = "URI_PERMISSION_GRANT"; // activity

    /// <summary>
    /// URI 권한 회수 이벤트
    /// <para>📄 로그: activity.txt</para>
    /// <para>⚙️ YAML: adb_activity_config.yaml (uri_revoke_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (8/13, 총점 4점, 가중치 0.22, D/E/R: 1+2+1)</para>
    /// <para>📱 적용 앱: 카카오톡 (하향 2025-10-28: Exclusivity Medium 재평가)</para>
    /// </summary>
    public const string URI_PERMISSION_REVOKE = "URI_PERMISSION_REVOKE"; // activity
    
    // ============================================================
    // Activity 관련 이벤트 (Activity Lifecycle)
    // ============================================================
    
    /// <summary>
    /// Activity 생명주기 이벤트
    /// <para>⚠️ 주의: 테스트용으로만 사용, 연구 논문에서 제외됨</para>
    /// <para>🚫 논문: 연구 범위 제외 (하위 이벤트 타입 사용, 테스트 코드 호환성 유지)</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string ACTIVITY_LIFECYCLE = "ACTIVITY_LIFECYCLE";

    /// <summary>
    /// Activity 실행 이벤트
    /// <para>📄 로그: activity.txt</para>
    /// <para>⚙️ YAML: adb_activity_config.yaml (activity_launch_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string ACTIVITY_LAUNCH = "ACTIVITY_LAUNCH";
    
    /// <summary>
    /// Activity 재개 이벤트 (usagestats)
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (activity_lifecycle_pattern, subType=ACTIVITY_RESUMED)</para>
    /// <para>🎯 논문: 세션 탐지용 (3/5)</para>
    /// </summary>
    public const string ACTIVITY_RESUMED = "ACTIVITY_RESUMED";
    
    /// <summary>
    /// Activity 일시정지 이벤트 (usagestats)
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (activity_lifecycle_pattern, subType=ACTIVITY_PAUSED)</para>
    /// <para>🎯 논문: 세션 탐지용 (4/5)</para>
    /// </summary>
    public const string ACTIVITY_PAUSED = "ACTIVITY_PAUSED";
    
    /// <summary>
    /// Activity 중지 이벤트 (usagestats)
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (activity_lifecycle_pattern, subType=ACTIVITY_STOPPED)</para>
    /// <para>🎯 논문: 세션 탐지용 (5/5)</para>
    /// </summary>
    public const string ACTIVITY_STOPPED = "ACTIVITY_STOPPED";

    /// <summary>
    /// Intent 세부 정보 이벤트
    /// <para>📄 로그: activity.txt</para>
    /// <para>⚙️ YAML: adb_activity_config.yaml (intent_action_pattern)</para>
    /// </summary>
    public const string INTENT_DETAILS = "INTENT_DETAILS";
    
    /// <summary>
    /// 카메라 Activity Refresh Rate 변경 이벤트 (무음 카메라 탐지용)
    /// <para>📄 로그: activity.txt</para>
    /// <para>⚙️ YAML: adb_activity_config.yaml (camera_activity_refresh_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 보조 아티팩트 (13/13, 총점 3점, 가중치 0.15, D/E/R: 1+1+1)</para>
    /// <para>📱 적용 앱: 모든 앱 (일반 UI 갱신 시에도 발생)</para>
    /// </summary>
    public const string CAMERA_ACTIVITY_REFRESH = "CAMERA_ACTIVITY_REFRESH";
    
    /// <summary>
    /// 무음 카메라 촬영 이벤트 (SilentCamera + Toast 패턴)
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (silent_camera_capture_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 확정 핵심 아티팩트 (3/13, 총점 8점, 가중치 0.5, D/E/R: 3+3+2)</para>
    /// <para>📱 적용 앱: 무음 카메라 (usagestats 전용 이벤트)</para>
    /// </summary>
    public const string SILENT_CAMERA_CAPTURE = "SILENT_CAMERA_CAPTURE";
    
    // ============================================================
    // 서비스 관련 이벤트 (Services)
    // ============================================================
    
    /// <summary>
    /// 포그라운드 서비스 이벤트
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (foreground_service_pattern)</para>
    /// <para>🎯 논문: 촬영 탐지용 - 조건부 핵심 아티팩트 (7/13, 총점 5점, 가중치 0.3, D/E/R: 2+1+2)</para>
    /// <para>📱 적용 앱: 기본 카메라, 카카오톡 (PostProcessService, NotificationService 검증, 추가 2025-10-26)</para>
    /// </summary>
    public const string FOREGROUND_SERVICE = "FOREGROUND_SERVICE";

    // ============================================================
    // 시스템 관련 이벤트 (System Events)
    // ============================================================

    /// <summary>
    /// 알림 이벤트
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (notification_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string NOTIFICATION = "NOTIFICATION";

    /// <summary>
    /// 화면 상태 변경 이벤트
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (screen_state_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string SCREEN_STATE = "SCREEN_STATE";

    /// <summary>
    /// 키가드(잠금화면) 이벤트
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (keyguard_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string KEYGUARD = "KEYGUARD";

    /// <summary>
    /// Standby Bucket 변경 이벤트
    /// <para>📄 로그: usagestats.txt</para>
    /// <para>⚙️ YAML: adb_usagestats_config.yaml (standby_bucket_pattern)</para>
    /// <para>🚫 논문: 연구 범위 제외</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string STANDBY_BUCKET_CHANGED = "STANDBY_BUCKET_CHANGED";

    /// <summary>
    /// 디바이스 부팅 완료 이벤트 (재부팅 탐지용)
    /// <para>📄 로그: CocktailBarService.log</para>
    /// <para>⚙️ YAML: adb_cocktail_config.yaml (boot_completed_pattern)</para>
    /// <para>🎯 논문: 재부팅 시점 탐지용</para>
    /// <para>📱 적용: 모든 디바이스 (Samsung Edge Service)</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string DEVICE_BOOT_COMPLETED = "DEVICE_BOOT_COMPLETED";
    
    // ============================================================
    // 네트워크 관련 이벤트 (Network)
    // ============================================================
    
    /// <summary>
    /// WiFi 패킷 전송 이벤트 (sem_wifi 로그) - 연구 범위에서 제외됨
    /// <para>⚠️ 이 이벤트 타입은 연구 범위에서 제외되었으며, WifiTransmissionDetector 호환성을 위해서만 유지됩니다.</para>
    /// <para>📄 로그: sem_wifi 로그 (파싱 구현 없음)</para>
    /// <para>🚫 논문: 연구 범위 제외 (실제로 파싱되지 않음)</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string WIFI_PACKET_TRANSMISSION = "WIFI_PACKET_TRANSMISSION";

    // ============================================================
    // 기타 이벤트 (Miscellaneous)
    // ============================================================

    /// <summary>
    /// 섹션 마커 이벤트 (로그 파싱용)
    /// <para>⚙️ YAML: adb_vibrator_config.yaml (usage_section_header)</para>
    /// <para>🚫 논문: 연구 범위 제외 (내부 파싱용)</para>
    /// </summary>
    [Obsolete("연구 범위에서 제외됨. 사용하지 마세요.", false)]
    public const string SECTION_MARKER = "SECTION_MARKER";
}
