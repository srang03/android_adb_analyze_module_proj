namespace AndroidAdbAnalyzeModule.Core.Constants;

/// <summary>
/// 로그 이벤트 타입 상수 정의
/// 모든 프로젝트에서 공통으로 사용하는 이벤트 타입 문자열을 중앙에서 관리합니다.
/// </summary>
public static class LogEventTypes
{
    // ============================================================
    // 세션 관련 이벤트 (Camera Session Management)
    // ============================================================
    
    /// <summary>카메라 연결 이벤트 (세션 시작)</summary>
    public const string CAMERA_CONNECT = "CAMERA_CONNECT";
    
    /// <summary>카메라 연결 해제 이벤트 (세션 종료)</summary>
    public const string CAMERA_DISCONNECT = "CAMERA_DISCONNECT";
    
    // ============================================================
    // 데이터베이스 관련 이벤트 (Database Operations)
    // ============================================================
    
    /// <summary>데이터베이스 삽입 이벤트</summary>
    public const string DATABASE_INSERT = "DATABASE_INSERT";
    
    /// <summary>데이터베이스 이벤트 (일반)</summary>
    public const string DATABASE_EVENT = "DATABASE_EVENT";
    
    /// <summary>미디어 삽입 시작 이벤트</summary>
    public const string MEDIA_INSERT_START = "MEDIA_INSERT_START";
    
    /// <summary>미디어 삽입 완료 이벤트 (촬영 완료 확정)</summary>
    public const string MEDIA_INSERT_END = "MEDIA_INSERT_END";
    
    // ============================================================
    // 미디어 관련 이벤트 (Media & Audio)
    // ============================================================
    
    /// <summary>오디오 트랙 이벤트</summary>
    public const string AUDIO_TRACK = "AUDIO_TRACK";
    
    /// <summary>미디어 추출기 이벤트</summary>
    public const string MEDIA_EXTRACTOR = "MEDIA_EXTRACTOR";
    
    /// <summary>셔터 사운드 이벤트</summary>
    public const string SHUTTER_SOUND = "SHUTTER_SOUND";
    
    /// <summary>진동 이벤트</summary>
    public const string VIBRATION = "VIBRATION";
    
    /// <summary>진동 이벤트 (Vibrator Manager)</summary>
    public const string VIBRATION_EVENT = "VIBRATION_EVENT";
    
    // ============================================================
    // 플레이어 관련 이벤트 (Audio Player)
    // ============================================================
    
    /// <summary>플레이어 생성 이벤트</summary>
    public const string PLAYER_CREATED = "PLAYER_CREATED";
    
    /// <summary>플레이어 이벤트 (시작/일시정지 등)</summary>
    public const string PLAYER_EVENT = "PLAYER_EVENT";
    
    /// <summary>플레이어 해제 이벤트</summary>
    public const string PLAYER_RELEASED = "PLAYER_RELEASED";
    
    // ============================================================
    // 오디오 포커스 관련 이벤트 (Audio Focus)
    // ============================================================
    
    /// <summary>오디오 포커스 요청 이벤트</summary>
    public const string FOCUS_REQUESTED = "FOCUS_REQUESTED";
    
    /// <summary>오디오 포커스 해제 이벤트</summary>
    public const string FOCUS_ABANDONED = "FOCUS_ABANDONED";
    
    // ============================================================
    // 녹음 관련 이벤트 (Recording)
    // ============================================================
    
    /// <summary>녹음 이벤트</summary>
    public const string RECORDING_EVENT = "RECORDING_EVENT";
    
    // ============================================================
    // 권한 관련 이벤트 (Permissions)
    // ============================================================
    
    /// <summary>URI 권한 부여 이벤트</summary>
    public const string URI_PERMISSION_GRANT = "URI_PERMISSION_GRANT";
    
    /// <summary>URI 권한 회수 이벤트</summary>
    public const string URI_PERMISSION_REVOKE = "URI_PERMISSION_REVOKE";
    
    // ============================================================
    // Activity 관련 이벤트 (Activity Lifecycle)
    // ============================================================
    
    /// <summary>Activity 생명주기 이벤트</summary>
    public const string ACTIVITY_LIFECYCLE = "ACTIVITY_LIFECYCLE";
    
    /// <summary>Activity 실행 이벤트</summary>
    public const string ACTIVITY_LAUNCH = "ACTIVITY_LAUNCH";
    
    /// <summary>Activity 재개 이벤트 (usagestats)</summary>
    public const string ACTIVITY_RESUMED = "ACTIVITY_RESUMED";
    
    /// <summary>Activity 일시정지 이벤트 (usagestats)</summary>
    public const string ACTIVITY_PAUSED = "ACTIVITY_PAUSED";
    
    /// <summary>Activity 중지 이벤트 (usagestats)</summary>
    public const string ACTIVITY_STOPPED = "ACTIVITY_STOPPED";
    
    /// <summary>Intent 세부 정보 이벤트</summary>
    public const string INTENT_DETAILS = "INTENT_DETAILS";
    
    /// <summary>카메라 Activity Refresh Rate 변경 이벤트 (무음 카메라 탐지용)</summary>
    public const string CAMERA_ACTIVITY_REFRESH = "CAMERA_ACTIVITY_REFRESH";
    
    /// <summary>무음 카메라 촬영 이벤트 (SilentCamera + Toast 패턴)</summary>
    public const string SILENT_CAMERA_CAPTURE = "SILENT_CAMERA_CAPTURE";
    
    // ============================================================
    // 서비스 관련 이벤트 (Services)
    // ============================================================
    
    /// <summary>포그라운드 서비스 이벤트</summary>
    public const string FOREGROUND_SERVICE = "FOREGROUND_SERVICE";
    
    // ============================================================
    // 시스템 관련 이벤트 (System Events)
    // ============================================================
    
    /// <summary>시스템 부팅 이벤트</summary>
    public const string SYSTEM_BOOT = "SYSTEM_BOOT";
    
    /// <summary>디바이스 재부팅 이벤트</summary>
    public const string DEVICE_REBOOT = "DEVICE_REBOOT";
    
    /// <summary>알림 이벤트</summary>
    public const string NOTIFICATION = "NOTIFICATION";
    
    /// <summary>화면 상태 변경 이벤트</summary>
    public const string SCREEN_STATE = "SCREEN_STATE";
    
    /// <summary>키가드(잠금화면) 이벤트</summary>
    public const string KEYGUARD = "KEYGUARD";
    
    /// <summary>Standby Bucket 변경 이벤트</summary>
    public const string STANDBY_BUCKET_CHANGED = "STANDBY_BUCKET_CHANGED";
    
    // ============================================================
    // 기타 이벤트 (Miscellaneous)
    // ============================================================
    
    /// <summary>섹션 마커 이벤트 (로그 파싱용)</summary>
    public const string SECTION_MARKER = "SECTION_MARKER";
}
