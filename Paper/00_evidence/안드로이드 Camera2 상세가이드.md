# Camera2 상세 가이드

## 목차

1. [Camera2 개요](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#camera2-%EA%B0%9C%EC%9A%94)
2. [카메라 캡처 세션 및 요청](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EC%B9%B4%EB%A9%94%EB%9D%BC-%EC%BA%A1%EC%B2%98-%EC%84%B8%EC%85%98-%EB%B0%8F-%EC%9A%94%EC%B2%AD)
3. [카메라 미리보기](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EC%B9%B4%EB%A9%94%EB%9D%BC-%EB%AF%B8%EB%A6%AC%EB%B3%B4%EA%B8%B0)
4. [다중 카메라 API](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EB%8B%A4%EC%A4%91-%EC%B9%B4%EB%A9%94%EB%9D%BC-api)
5. [Camera2 확장 프로그램 API](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#camera2-%ED%99%95%EC%9E%A5-%ED%94%84%EB%A1%9C%EA%B7%B8%EB%9E%A8-api)

---

## Camera2 개요

 **출처** : [Camera2 개요 - Android Developers](https://developer.android.com/media/camera/camera2?hl=ko)

### 기본 개념

Camera2는 지원 중단된 Camera 클래스를 대체하는 하위 수준의 Android 카메라 패키지입니다. Camera2는 복잡한 사용 사례를 위한 자세한 제어를 제공하지만 기기별 구성을 관리해야 합니다.

 **중요한 권장사항** : 앱에 Camera2의 특정 하위 수준 기능이 필요한 경우가 아니라면 CameraX를 사용하는 것이 좋습니다. CameraX와 Camera2는 모두 Android 5.0(API 수준 21) 이상을 지원합니다.

### 지원 버전

* **최소 API 수준** : Android 5.0 (API 수준 21)
* **권장 대안** : CameraX Jetpack 라이브러리

---

## 카메라 캡처 세션 및 요청

 **출처** : [카메라 캡처 세션 및 요청 - Android Developers](https://developer.android.com/media/camera/camera2/capture-sessions-requests?hl=ko)

### 기본 아키텍처

#### CameraDevice와 스트림

* **CameraDevice** : 하나의 Android 지원 기기에 여러 대의 카메라가 있을 수 있으며, 각 카메라는 CameraDevice입니다
* **스트림** : CameraDevice는 동시에 둘 이상의 스트림을 출력할 수 있습니다
* **스트림 용도** : 각 스트림은 특정 작업에 최적화됩니다 (사진 촬영, 동영상 제작 등)

#### 스트림 사용 사례를 통한 성능 개선

스트림 사용 사례는 Camera2 캡처 성능을 개선하는 방법입니다. 카메라 기기가 카메라 하드웨어 및 소프트웨어 파이프라인을 최적화하도록 지원합니다.

 **지원되는 스트림 사용 사례** :

* `DEFAULT`: 모든 기존 애플리케이션 동작 포함
* `PREVIEW`: 뷰파인더 또는 인앱 이미지 분석에 권장
* `STILL_CAPTURE`: 고화질 고해상도 캡처에 최적화
* `VIDEO_RECORD`: 고화질 동영상 캡처에 최적화
* `VIDEO_CALL`: 전력 소모가 큰 카메라 장시간 실행에 권장
* `PREVIEW_VIDEO_STILL`: 소셜 미디어 앱 또는 단일 스트림 사용에 권장
* `VENDOR_START`: OEM에서 정의한 사용 사례

### CameraCaptureSession 만들기

카메라 세션을 만들려면 하나 이상의 출력 버퍼를 제공해야 합니다.

#### Kotlin 예시

```kotlin
// 대상 Surface 검색
val surfaceView = findViewById<SurfaceView>(...)
val imageReader = ImageReader.newInstance(...)
val previewSurface = surfaceView.holder.surface
val imReaderSurface = imageReader.surface
val targets = listOf(previewSurface, imReaderSurface)

// 스트림 사용 사례 설정
@RequiresApi(Build.VERSION_CODES.TIRAMISU)
fun configureSession(device: CameraDevice, targets: List<Surface>) {
    val configs = mutableListOf<OutputConfiguration>()
    val streamUseCase = CameraMetadata.SCALER_AVAILABLE_STREAM_USE_CASES_PREVIEW_VIDEO_STILL
  
    targets.forEach {
        val config = OutputConfiguration(it)
        config.streamUseCase = streamUseCase.toLong()
        configs.add(config)
    }
  
    device.createCaptureSession(session)
}
```

#### Java 예시

```java
// 대상 Surface 검색
Surface surfaceView = findViewById<SurfaceView>(...);
ImageReader imageReader = ImageReader.newInstance(...);
Surface previewSurface = surfaceView.getHolder().getSurface();
Surface imageSurface = imageReader.getSurface();
List<Surface> targets = Arrays.asList(previewSurface, imageSurface);

// 스트림 사용 사례 설정
private void configureSession(CameraDevice device, List<Surface> targets) {
    ArrayList<OutputConfiguration> configs = new ArrayList();
    String streamUseCase = CameraMetadata.SCALER_AVAILABLE_STREAM_USE_CASES_PREVIEW_VIDEO_STILL;
  
    for(Surface s : targets) {
        OutputConfiguration config = new OutputConfiguration(s);
        config.setStreamUseCase(String.toLong(streamUseCase));
        configs.add(config);
    }
  
    device.createCaptureSession(session);
}
```

### 단일 CaptureRequest

각 프레임에 사용되는 구성은 CaptureRequest로 인코딩됩니다. 캡처 요청을 만들려면 사전 정의된 템플릿을 사용합니다.

#### Kotlin 예시

```kotlin
val session: CameraCaptureSession = ... // CameraCaptureSession.StateCallback에서
val captureRequest = session.device.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW)
captureRequest.addTarget(previewSurface)

// 캡처 요청 전달
session.capture(captureRequest.build(), null, null)
```

#### Java 예시

```java
CameraCaptureSession session = ...; // CameraCaptureSession.StateCallback에서
CaptureRequest.Builder captureRequest = session.getDevice().createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
captureRequest.addTarget(previewSurface);

// 캡처 요청 전달
session.capture(captureRequest.build(), null, null);
```

### CaptureRequest 반복

연속적인 프레임 스트림을 제공하기 위해 반복 요청을 사용합니다.

#### Kotlin 예시

```kotlin
val session: CameraCaptureSession = ...
val captureRequest: CaptureRequest = ...

// 세션이 종료되거나 session.stopRepeating()이 호출될 때까지 계속 전송
session.setRepeatingRequest(captureRequest.build(), null, null)
```

### CaptureRequest 인터리브

반복 캡처 요청이 활성화된 상태에서 두 번째 캡처 요청을 보낼 수 있습니다.

#### Kotlin 예시

```kotlin
val session: CameraCaptureSession = ...

// 반복 요청 생성 및 전달
val repeatingRequest = session.device.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW)
repeatingRequest.addTarget(previewSurface)
session.setRepeatingRequest(repeatingRequest.build(), null, null)

// 나중에... 단일 요청 생성 및 전달
val singleRequest = session.device.createCaptureRequest(CameraDevice.TEMPLATE_STILL_CAPTURE)
singleRequest.addTarget(imReaderSurface)
session.capture(singleRequest.build(), null, null)
```

---

## 카메라 미리보기

 **출처** : [카메라 미리보기 - Android Developers](https://developer.android.com/media/camera/camera2/camera-preview?hl=ko)

### 카메라 방향

Android 호환성 정의에서는 카메라 이미지 센서가 '카메라의 긴 쪽이 화면의 긴 쪽과 정렬되도록 방향을 설정해야 한다'고 명시합니다.

#### SENSOR_ORIENTATION

* **전면 카메라** : 일반적으로 270도
* **후면 카메라** : 일반적으로 90도
* **노트북 카메라** : 일반적으로 0도 또는 180도

### 방향 계산

카메라 미리보기의 적절한 방향은 센서 방향 및 기기 회전을 고려해야 합니다.

 **공식** :

```
rotation = (sensorOrientationDegrees - deviceOrientationDegrees * sign + 360) % 360
```

여기서 `sign`은 전면 카메라의 경우 `1`이고 후면 카메라의 경우 `-1`입니다.

#### 상대 회전 계산

##### Kotlin 예시

```kotlin
/**
 * 카메라 센서 출력 방향을 기기의 현재 방향으로 변환하는 데 필요한 회전을 계산
 */
fun computeRelativeRotation(
    characteristics: CameraCharacteristics,
    surfaceRotationDegrees: Int
): Int {
    val sensorOrientationDegrees = characteristics.get(CameraCharacteristics.SENSOR_ORIENTATION)!!
  
    // 후면 카메라의 경우 기기 방향 반전
    val sign = if (characteristics.get(CameraCharacteristics.LENS_FACING) == 
        CameraCharacteristics.LENS_FACING_FRONT) 1 else -1
  
    // 기기 방향에 대해 수직으로 이미지를 만들기 위해 원하는 방향 계산
    return (sensorOrientationDegrees - surfaceRotationDegrees * sign + 360) % 360
}
```

##### Java 예시

```java
/**
 * 카메라 센서 출력 방향을 기기의 현재 방향으로 변환하는 데 필요한 회전을 계산
 */
public int computeRelativeRotation(
    CameraCharacteristics characteristics,
    int surfaceRotationDegrees
) {
    Integer sensorOrientationDegrees = characteristics.get(CameraCharacteristics.SENSOR_ORIENTATION);
  
    // 후면 카메라의 경우 기기 방향 반전
    int sign = characteristics.get(CameraCharacteristics.LENS_FACING) == 
        CameraCharacteristics.LENS_FACING_FRONT ? 1 : -1;
  
    // 기기 방향에 대해 수직으로 이미지를 만들기 위해 원하는 방향 계산
    return (sensorOrientationDegrees - surfaceRotationDegrees * sign + 360) % 360;
}
```

### 인셋 세로 모드

Android 12 (API 수준 31)부터 앱은 인셋 세로 모드를 명시적으로 제어할 수 있습니다.

#### SCALER_ROTATE_AND_CROP 속성

* `SCALER_ROTATE_AND_CROP_AUTO`: 시스템이 인셋 세로 모드를 호출할 수 있음 (기본값)
* `SCALER_ROTATE_AND_CROP_90`: 인셋 세로 모드의 동작

### CameraX PreviewView

CameraX의 PreviewView는 카메라 미리보기 생성을 간소화합니다.

 **특징** :

* 센서 방향과 기기 회전을 자동으로 조정
* 배율 유형: `FILL_CENTER` (기본값), `FIT_CENTER`

### CameraViewfinder

CameraViewfinder 라이브러리는 Camera2와 함께 사용할 수 있는 도구입니다.

#### Kotlin 예시

```kotlin
fun startCamera() {
    val previewResolution = Size(width, height)
    val viewfinderSurfaceRequest = ViewfinderSurfaceRequest(previewResolution, characteristics)
    val surfaceListenableFuture = cameraViewfinder.requestSurfaceAsync(viewfinderSurfaceRequest)
  
    Futures.addCallback(surfaceListenableFuture, object : FutureCallback<Surface> {
        override fun onSuccess(surface: Surface) {
            /* CaptureSession 생성 */
        }
        override fun onFailure(t: Throwable) {
            /* 오류 처리 */
        }
    }, ContextCompat.getMainExecutor(context))
}
```

### SurfaceView 구현

SurfaceView는 처리가 필요하지 않고 애니메이션이 적용되지 않는 경우 간단한 접근 방식입니다.

#### Kotlin 예시

```kotlin
override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
    val width = MeasureSpec.getSize(widthMeasureSpec)
    val height = MeasureSpec.getSize(heightMeasureSpec)
    val relativeRotation = computeRelativeRotation(characteristics, surfaceRotationDegrees)
  
    if (previewWidth > 0f && previewHeight > 0f) {
        val scaleX = if (relativeRotation % 180 == 0) {
            width.toFloat() / previewWidth
        } else {
            width.toFloat() / previewHeight
        }
      
        val scaleY = if (relativeRotation % 180 == 0) {
            height.toFloat() / previewHeight
        } else {
            height.toFloat() / previewWidth
        }
      
        val finalScale = min(scaleX, scaleY)
        setScaleX(1 / scaleX * finalScale)
        setScaleY(1 / scaleY * finalScale)
    }
  
    setMeasuredDimension(width, height)
}
```

### 창 측정항목

카메라 뷰파인더 크기를 결정하는 데 화면 크기를 사용해서는 안 됩니다.

 **권장 방법** :

* `WindowManager#getCurrentWindowMetrics()` (API 수준 30+)
* Jetpack WindowManager 라이브러리의 `WindowMetricsCalculator#computeCurrentWindowMetrics()` (API 수준 14+)

### 180도 회전 감지

기기를 180도 회전할 때는 `onConfigurationChanged()`가 트리거되지 않습니다.

#### 감지 방법

```kotlin
// DisplayListener 구현
override fun onDisplayChanged(displayId: Int) {
    val rotation = display.rotation
    // 회전 처리
}
```

### 독점 리소스 처리

Android 10(API 수준 29)에서는 다중 재개를 도입했습니다. 카메라와 같은 독점 리소스를 사용하는 앱은 다중 재개를 지원해야 합니다.

 **중요** : `onDisconnected()` 콜백을 구현하여 우선순위가 더 높은 활동의 카메라 선점 액세스를 인식해야 합니다.

---

## 다중 카메라 API

 **출처** : [다중 카메라 API - Android Developers](https://developer.android.com/media/camera/camera2/multi-camera?hl=ko)

### 개요

다중 카메라는 Android 9 (API 수준 28)부터 도입되었습니다. API를 지원하는 기기가 출시되고 있으며, 다양한 다중 카메라 사용 사례가 특정 하드웨어 구성과 밀접하게 결합되어 있습니다.

### 일반적인 사용 사례

* **확대/축소** : 자르기 영역 또는 원하는 초점에 따라 카메라 간 전환
* **심도** : 여러 카메라를 사용하여 깊이 지도 생성
* **빛망울 효과** : 추론된 심도 정보를 사용하여 DSLR처럼 좁은 화면 시뮬레이션

### 논리 카메라와 물리적 카메라

#### 물리적 카메라

3개의 후면 카메라가 있는 기기에서 각각은 물리적 카메라로 간주됩니다.

#### 논리 카메라

두 개 이상의 물리적 카메라를 그룹화한 것입니다. 논리 카메라의 출력은:

* 기본 물리적 카메라 중 하나에서 들어오는 스트림
* 2개 이상의 기본 물리적 카메라에서 나오는 합성 스트림

### 다중 카메라 API

새 API에 추가된 주요 요소들:

* `CameraMetadata.REQUEST_AVAILABLE_CAPABILITIES_LOGICAL_MULTI_CAMERA`
* `CameraCharacteristics.getPhysicalCameraIds()`
* `CameraCharacteristics.getAvailablePhysicalCameraRequestKeys()`
* `CameraDevice.createCaptureSession(SessionConfiguration config)`
* `CameraCharacteritics.LOGICAL_MULTI_CAMERA_SENSOR_SYNC_TYPE`
* `OutputConfiguration`
* `SessionConfiguration`

### 한 쌍의 물리적 카메라 찾기

#### Kotlin 예시

```kotlin
data class DualCamera(val logicalId: String, val physicalId1: String, val physicalId2: String)

fun findDualCameras(manager: CameraManager, facing: Int? = null): List<DualCamera> {
    val dualCameras = mutableListOf<DualCamera>()
  
    manager.cameraIdList.map { 
        Pair(manager.getCameraCharacteristics(it), it) 
    }.filter {
        // 요청된 방향을 향하는 카메라로 필터링
        facing == null || it.first.get(CameraCharacteristics.LENS_FACING) == facing
    }.filter {
        // 논리 카메라로 필터링
        it.first.get(CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES)!!.contains(
            CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES_LOGICAL_MULTI_CAMERA)
    }.forEach {
        // 물리적 카메라 목록에서 가능한 모든 쌍이 유효한 결과
        val physicalCameras = it.first.physicalCameraIds.toTypedArray()
        for (idx1 in 0 until physicalCameras.size) {
            for (idx2 in (idx1 + 1) until physicalCameras.size) {
                dualCameras.add(DualCamera(
                    it.second, physicalCameras[idx1], physicalCameras[idx2]))
            }
        }
    }
  
    return dualCameras
}
```

### 듀얼 카메라 열기

#### Kotlin 예시

```kotlin
fun openDualCamera(
    cameraManager: CameraManager, 
    dualCamera: DualCamera,
    executor: Executor = AsyncTask.SERIAL_EXECUTOR,
    callback: (CameraDevice) -> Unit
) {
    cameraManager.openCamera(
        dualCamera.logicalId, executor,
        object : CameraDevice.StateCallback() {
            override fun onOpened(device: CameraDevice) = callback(device)
            override fun onError(device: CameraDevice, error: Int) = onDisconnected(device)
            override fun onDisconnected(device: CameraDevice) = device.close()
        })
}
```

### 여러 물리적 카메라가 있는 세션 만들기

#### Kotlin 예시

```kotlin
typealias DualCameraOutputs = Triple<MutableList<Surface>?, MutableList<Surface>?, MutableList<Surface>?>

fun createDualCameraSession(
    cameraManager: CameraManager,
    dualCamera: DualCamera,
    targets: DualCameraOutputs,
    executor: Executor = AsyncTask.SERIAL_EXECUTOR,
    callback: (CameraCaptureSession) -> Unit
) {
    // 3개의 출력 구성 세트 만들기: 논리 카메라용, 각 물리적 카메라용
    val outputConfigsLogical = targets.first?.map { OutputConfiguration(it) }
    val outputConfigsPhysical1 = targets.second?.map { 
        OutputConfiguration(it).apply { 
            setPhysicalCameraId(dualCamera.physicalId1) 
        } 
    }
    val outputConfigsPhysical2 = targets.third?.map { 
        OutputConfiguration(it).apply { 
            setPhysicalCameraId(dualCamera.physicalId2) 
        } 
    }
  
    // 모든 출력 구성을 하나의 플랫 배열에 넣기
    val outputConfigsAll = arrayOf(
        outputConfigsLogical, outputConfigsPhysical1, outputConfigsPhysical2
    ).filterNotNull().flatMap { it }
  
    // 세션을 만드는 데 사용할 수 있는 세션 구성 인스턴스화
    val sessionConfiguration = SessionConfiguration(
        SessionConfiguration.SESSION_REGULAR, outputConfigsAll, executor,
        object : CameraCaptureSession.StateCallback() {
            override fun onConfigured(session: CameraCaptureSession) = callback(session)
            override fun onConfigureFailed(session: CameraCaptureSession) = session.device.close()
        })
  
    // 이전에 정의된 함수를 사용하여 논리 카메라 열기
    openDualCamera(cameraManager, dualCamera, executor = executor) {
        // 마지막으로 세션을 만들고 콜백을 통해 반환
        it.createCaptureSession(sessionConfiguration)
    }
}
```

### 확대/축소 사용 사례

물리적 카메라를 단일 스트림으로 병합하여 사용자가 여러 물리적 카메라 사이를 전환할 수 있습니다.

#### 최소 및 최대 초점 거리 카메라 쌍 찾기

##### Kotlin 예시

```kotlin
fun findShortLongCameraPair(manager: CameraManager, facing: Int? = null): DualCamera? {
    return findDualCameras(manager, facing).map {
        val characteristics1 = manager.getCameraCharacteristics(it.physicalId1)
        val characteristics2 = manager.getCameraCharacteristics(it.physicalId2)
      
        // 각 물리적 카메라에서 보급하는 초점 거리 쿼리
        val focalLengths1 = characteristics1.get(
            CameraCharacteristics.LENS_INFO_AVAILABLE_FOCAL_LENGTHS) ?: floatArrayOf(0F)
        val focalLengths2 = characteristics2.get(
            CameraCharacteristics.LENS_INFO_AVAILABLE_FOCAL_LENGTHS) ?: floatArrayOf(0F)
      
        // 카메라 간 최소 및 최대 초점 거리 간의 가장 큰 차이 계산
        val focalLengthsDiff1 = focalLengths2.maxOrNull()!! - focalLengths1.minOrNull()!!
        val focalLengthsDiff2 = focalLengths1.maxOrNull()!! - focalLengths2.minOrNull()!!
      
        // 카메라 ID 쌍과 최소 및 최대 초점 거리 간의 차이 반환
        if (focalLengthsDiff1 < focalLengthsDiff2) {
            Pair(DualCamera(it.logicalId, it.physicalId1, it.physicalId2), focalLengthsDiff1)
        } else {
            Pair(DualCamera(it.logicalId, it.physicalId2, it.physicalId1), focalLengthsDiff2)
        }
    }.maxByOrNull { it.second }?.first // 가장 큰 차이가 있는 쌍만 반환하거나 쌍이 없는 경우 null
}
```

### 렌즈 왜곡

모든 렌즈는 일정량의 왜곡을 일으킵니다. Android에서는 `CameraCharacteristics.LENS_DISTORTION`에서 렌즈에 의해 생성된 왜곡을 확인할 수 있습니다.

#### 왜곡 수정 설정

##### Kotlin 예시

```kotlin
val cameraSession: CameraCaptureSession = ...
val captureRequest = cameraSession.device.createCaptureRequest(
    CameraDevice.TEMPLATE_STILL_CAPTURE)

// 이 기기가 왜곡 수정을 지원하는지 확인
val characteristics: CameraCharacteristics = ...
val supportsDistortionCorrection = characteristics.get(
    CameraCharacteristics.DISTORTION_CORRECTION_AVAILABLE_MODES
)?.contains(
    CameraMetadata.DISTORTION_CORRECTION_MODE_HIGH_QUALITY
) ?: false

if (supportsDistortionCorrection) {
    captureRequest.set(
        CaptureRequest.DISTORTION_CORRECTION_MODE,
        CameraMetadata.DISTORTION_CORRECTION_MODE_HIGH_QUALITY
    )
}

// 출력 대상 추가, 기타 캡처 요청 매개변수 설정...
// 캡처 요청 전달
cameraSession.capture(captureRequest.build(), ...)
```

---

## Camera2 확장 프로그램 API

 **출처** : [Camera2 확장 프로그램 API - Android Developers](https://developer.android.com/media/camera/camera2/extensions-api?hl=ko)

### 개요

Camera2는 기기 제조업체가 다양한 Android 기기에 구현한 확장 프로그램에 액세스하기 위한 확장 프로그램 API를 제공합니다.

### 확장 프로그램 아키텍처

Camera2 애플리케이션 → Camera2 API → Camera Extensions OEM 라이브러리

### 지원되는 확장 프로그램 모드

* 야간 모드 (NIGHT)
* HDR
* 자동 (AUTO)
* 빛망울 효과 (BOKEH)
* 얼굴 보정 (FACE RETOUCH)

### Camera2 Extensions API 호환성 테스트

#### Kotlin 예시

```kotlin
private fun getExtensionCameraIds(cameraManager: CameraManager): List<String> = 
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
        cameraManager.cameraIdList.filter { cameraId ->
            val characteristics = cameraManager.getCameraCharacteristics(cameraId)
            val extensionCharacteristics = cameraManager.getCameraExtensionCharacteristics(cameraId)
            val capabilities = characteristics.get(CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES)
          
            extensionCharacteristics.supportedExtensions.isNotEmpty() && 
            capabilities?.contains(
                CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES_BACKWARD_COMPATIBLE
            ) ?: false
        }
    } else emptyList()
```

#### Java 예시

```java
private List<String> getExtensionCameraIds(CameraManager cameraManager) throws CameraAccessException {
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
        return Arrays.stream(cameraManager.getCameraIdList()).filter(cameraId -> {
            try {
                CameraCharacteristics characteristics = cameraManager.getCameraCharacteristics(cameraId);
                CameraExtensionCharacteristics extensionCharacteristics = 
                    cameraManager.getCameraExtensionCharacteristics(cameraId);
                IntStream capabilities = Arrays.stream(
                    characteristics.get(CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES)
                );
              
                return !extensionCharacteristics.getSupportedExtensions().isEmpty() &&
                    capabilities.anyMatch(capability -> capability == 
                        CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES_BACKWARD_COMPATIBLE);
            } catch (CameraAccessException e) {
                throw new RuntimeException(e);
            }
        }).collect(Collectors.toList());
    } else {
        return Collections.emptyList();
    }
}
```

### CameraExtensionSession 만들기

#### Kotlin 예시

```kotlin
private val captureCallbacks = object : CameraExtensionSession.ExtensionCaptureCallback() {
    // 캡처 콜백 구현
}

private val extensionSessionStateCallback = object : CameraExtensionSession.StateCallback() {
    override fun onConfigured(session: CameraExtensionSession) {
        cameraExtensionSession = session
        try {
            val captureRequest = cameraDevice.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW).apply {
                addTarget(previewSurface)
            }.build()
          
            session.setRepeatingRequest(
                captureRequest,
                Dispatchers.IO.asExecutor(),
                captureCallbacks
            )
        } catch (e: CameraAccessException) {
            Snackbar.make(
                previewView,
                "Failed to preview capture request",
                Snackbar.LENGTH_SHORT
            ).show()
            requireActivity().finish()
        }
    }
  
    override fun onClosed(session: CameraExtensionSession) {
        super.onClosed(session)
        cameraDevice.close()
    }
  
    override fun onConfigureFailed(session: CameraExtensionSession) {
        Snackbar.make(
            previewView,
            "Failed to start camera extension preview",
            Snackbar.LENGTH_SHORT
        ).show()
        requireActivity().finish()
    }
}

private fun startExtensionSession() {
    val outputConfig = arrayListOf(
        OutputConfiguration(stillImageReader.surface),
        OutputConfiguration(previewSurface)
    )
  
    val extensionConfiguration = ExtensionSessionConfiguration(
        CameraExtensionCharacteristics.EXTENSION_NIGHT,
        outputConfig,
        Dispatchers.IO.asExecutor(),
        extensionSessionStateCallback
    )
  
    cameraDevice.createExtensionSession(extensionConfiguration)
}
```

#### Java 예시

```java
private CameraExtensionSession.ExtensionCaptureCallback captureCallbacks = 
    new CameraExtensionSession.ExtensionCaptureCallback() {
        // 캡처 콜백 구현
    };

private CameraExtensionSession.StateCallback extensionSessionStateCallback = 
    new CameraExtensionSession.StateCallback() {
        @Override
        public void onConfigured(@NonNull CameraExtensionSession session) {
            cameraExtensionSession = session;
            try {
                CaptureRequest.Builder captureRequestBuilder = 
                    cameraDevice.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
                captureRequestBuilder.addTarget(previewSurface);
                CaptureRequest captureRequest = captureRequestBuilder.build();
              
                session.setRepeatingRequest(captureRequest, backgroundExecutor, captureCallbacks);
            } catch (CameraAccessException e) {
                Snackbar.make(
                    previewView,
                    "Failed to preview capture request",
                    Snackbar.LENGTH_SHORT
                ).show();
                requireActivity().finish();
            }
        }
      
        @Override
        public void onClosed(@NonNull CameraExtensionSession session) {
            super.onClosed(session);
            cameraDevice.close();
        }
      
        @Override
        public void onConfigureFailed(@NonNull CameraExtensionSession session) {
            Snackbar.make(
                previewView,
                "Failed to start camera extension preview",
                Snackbar.LENGTH_SHORT
            ).show();
            requireActivity().finish();
        }
    };

private void startExtensionSession() {
    ArrayList<OutputConfiguration> outputConfig = new ArrayList<>();
    outputConfig.add(new OutputConfiguration(stillImageReader.getSurface()));
    outputConfig.add(new OutputConfiguration(previewSurface));
  
    ExtensionSessionConfiguration extensionConfiguration = new ExtensionSessionConfiguration(
        CameraExtensionCharacteristics.EXTENSION_NIGHT,
        outputConfig,
        backgroundExecutor,
        extensionSessionStateCallback
    );
  
    cameraDevice.createExtensionSession(extensionConfiguration);
}
```

---

## 추가 리소스 및 참고 문서

### 공식 샘플 및 리소스

1. **Camera2 샘플 프로젝트**
   * [Camera2Basic 샘플](https://github.com/android/camera-samples/tree/master/Camera2Basic)
   * [Camera2Video 샘플](https://github.com/android/camera-samples/tree/master/Camera2Video)
   * [Camera2SlowMotion 샘플](https://github.com/android/camera-samples/tree/master/Camera2SlowMotion)
   * [Camera2Extensions API 샘플](https://github.com/android/camera-samples/tree/master/Camera2Extensions)
2. **관련 가이드**
   * [카메라 라이브러리 선택](https://developer.android.com/training/camera/choose-camera-library?hl=ko)
   * [카메라 확장 프로그램](https://developer.android.com/training/camera/camera-extensions?hl=ko)
   * [지원되는 기기](https://developer.android.com/training/camera/supported-devices?hl=ko)
3. **ChromeOS 관련**
   * [카메라 방향](https://chromeos.dev/en/android/camera-orientation)
4. **폴더블 및 대형 화면**
   * [폴더블 알아보기](https://developer.android.com/develop/ui/compose/layouts/adaptive/foldables/learn-about-foldables?hl=ko)
   * [멀티 윈도우 지원](https://developer.android.com/develop/ui/compose/layouts/adaptive/support-multi-window-mode?hl=ko)

### API 참조

* [Camera2 패키지 요약](https://developer.android.com/reference/android/hardware/camera2/package-summary?hl=ko)
* [CameraDevice](https://developer.android.com/reference/android/hardware/camera2/CameraDevice?hl=ko)
* [CameraCaptureSession](https://developer.android.com/reference/android/hardware/camera2/CameraCaptureSession?hl=ko)
* [CaptureRequest](https://developer.android.com/reference/android/hardware/camera2/CaptureRequest?hl=ko)
* [CameraCharacteristics](https://developer.android.com/reference/android/hardware/camera2/CameraCharacteristics?hl=ko)
* [CameraExtensionCharacteristics](https://developer.android.com/reference/android/hardware/camera2/CameraExtensionCharacteristics?hl=ko)

---

 **작성일** : 2025년 9월 27일

 **근거** : Android Developers 공식 문서 다섯 개 문서를 기반으로 작성

 **출처** :

1. [Camera2 개요](https://developer.android.com/media/camera/camera2?hl=ko)
2. [카메라 캡처 세션 및 요청](https://developer.android.com/media/camera/camera2/capture-sessions-requests?hl=ko)
3. [카메라 미리보기](https://developer.android.com/media/camera/camera2/camera-preview?hl=ko)
4. [다중 카메라 API](https://developer.android.com/media/camera/camera2/multi-camera?hl=ko)
5. [Camera2 확장 프로그램 API](https://developer.android.com/media/camera/camera2/extensions-api?hl=ko)
