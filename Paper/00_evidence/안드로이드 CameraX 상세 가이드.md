# CameraX 상세 가이드

## 목차

1. [CameraX 아키텍처](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#camerax-%EC%95%84%ED%82%A4%ED%85%8D%EC%B2%98)
2. [구성 옵션](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EA%B5%AC%EC%84%B1-%EC%98%B5%EC%85%98)
3. [확장 프로그램 API](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%ED%99%95%EC%9E%A5-%ED%94%84%EB%A1%9C%EA%B7%B8%EB%9E%A8-api)

---

## CameraX 아키텍처

 **출처** : [CameraX 아키텍처 - Android Developers](https://developer.android.com/media/camera/camerax/architecture?hl=ko)

### CameraX 구조

CameraX를 통해 **사용 사례(Use Case)**라는 추상화를 통해 기기의 카메라와 연결할 수 있습니다.

#### 지원되는 사용 사례

1. **미리보기 (Preview)** : PreviewView 같은 미리보기를 표시할 영역을 허용합니다
2. **이미지 분석 (Image Analysis)** : 머신러닝 등의 분석을 위해 CPU에서 액세스할 수 있는 버퍼를 제공합니다
3. **이미지 캡처 (Image Capture)** : 사진을 캡처하고 저장합니다
4. **동영상 캡처 (Video Capture)** : VideoCapture로 동영상 및 오디오를 캡처합니다

### API 모델

CameraX 라이브러리를 작동시키기 위해서는 다음을 지정해야 합니다:

* 원하는 사용 사례와 구성 옵션 지정
* 리스너를 첨부하여 출력 데이터로 할 일 지정
* 사용 사례를 Android 아키텍처 수명 주기에 결합하여 카메라 사용 시기 및 데이터 생성 시기와 같은 의도된 흐름 지정

### CameraX 앱 작성 방법

#### 1. CameraController (간단한 방법)

 **특징** :

* 설정 코드가 거의 필요하지 않음
* 카메라 초기화, 사용 사례 관리, 타겟 회전, 탭하여 초점 맞추기, 손가락을 모으거나 펼쳐 확대/축소 등을 자동으로 처리
* 구체적인 클래스는 `LifecycleCameraController`

##### Kotlin 예시

```kotlin
val previewView: PreviewView = viewBinding.previewView
var cameraController = LifecycleCameraController(baseContext)
cameraController.bindToLifecycle(this)
cameraController.cameraSelector = CameraSelector.DEFAULT_BACK_CAMERA
previewView.controller = cameraController
```

##### Java 예시

```java
PreviewView previewView = viewBinding.previewView;
LifecycleCameraController cameraController = new LifecycleCameraController(baseContext);
cameraController.bindToLifecycle(this);
cameraController.setCameraSelector(CameraSelector.DEFAULT_BACK_CAMERA);
previewView.setController(cameraController);
```

 **기본 UseCase** : Preview, ImageCapture, ImageAnalysis

#### 2. CameraProvider (더 많은 유연성 필요시)

 **특징** :

* 더 많은 구성을 맞춤설정할 수 있음
* 카메라 미리보기를 위해 맞춤 Surface를 사용하여 더 많은 유연성 확보 가능
* 출력 이미지 회전 사용 설정, ImageAnalysis에서 출력 이미지 형식 설정 등 가능

### CameraX 수명 주기

CameraX는 카메라를 여는 시점, 캡처 세션을 생성할 시점, 중지 및 종료할 시점을 결정하기 위해 수명 주기를 따릅니다.

#### 맞춤 LifecycleOwners

고급 사용 사례의 경우 맞춤 LifecycleOwner를 만들어 앱이 명시적으로 CameraX 세션 수명 주기를 관리하도록 할 수 있습니다.

##### Kotlin 예시

```kotlin
class CustomLifecycle : LifecycleOwner {
    private val lifecycleRegistry: LifecycleRegistry
  
    init {
        lifecycleRegistry = LifecycleRegistry(this);
        lifecycleRegistry.markState(Lifecycle.State.CREATED)
    }
  
    fun doOnResume() {
        lifecycleRegistry.markState(State.RESUMED)
    }
  
    override fun getLifecycle(): Lifecycle {
        return lifecycleRegistry
    }
}
```

### 동시 사용 사례

 **제한사항** :

* Preview, VideoCapture, ImageAnalysis, ImageCapture의 인스턴스를 각각 하나씩 동시에 사용 가능
* 확장 프로그램 사용 시 ImageCapture 및 Preview 조합만 작동 보장
* 카메라 하드웨어 수준이 FULL 이하인 기기에서는 스트림 공유가 필요할 수 있어 지연 시간과 배터리 수명에 영향

### 권한 요구사항

* **필수** : `CAMERA` 권한
* **조건부** : Android 10 미만 기기에서 파일에 이미지 저장 시 `WRITE_EXTERNAL_STORAGE` 권한

### 시스템 요구사항

* **최소 Android API 수준** : 21
* **Android 아키텍처 구성요소** : 1.1.1
* **권장 활동 클래스** : FragmentActivity 또는 AppCompatActivity

### 종속 항목 설정

#### Gradle 설정

```kotlin
dependencies {
    val camerax_version = "1.5.0"
  
    // CameraX core library using the camera2 implementation
    implementation("androidx.camera:camera-core:${camerax_version}")
    implementation("androidx.camera:camera-camera2:${camerax_version}")
  
    // CameraX Lifecycle library
    implementation("androidx.camera:camera-lifecycle:${camerax_version}")
  
    // CameraX VideoCapture library
    implementation("androidx.camera:camera-video:${camerax_version}")
  
    // CameraX View class
    implementation("androidx.camera:camera-view:${camerax_version}")
  
    // CameraX ML Kit Vision Integration
    implementation("androidx.camera:camera-mlkit-vision:${camerax_version}")
  
    // CameraX Extensions library
    implementation("androidx.camera:camera-extensions:${camerax_version}")
}
```

### Camera2와의 상호 운용성

CameraX는 Camera2를 기반으로 하며 Camera2 구현에서 속성을 읽고 쓸 수 있습니다:

* `Camera2CameraInfo`를 사용하여 기본 CameraCharacteristics 읽기
* `Camera2CameraControl`을 사용하여 기본 CaptureRequest에 속성 설정
* `Camera2Interop.Extender`를 사용하여 UseCase에 속성 설정

---

## 구성 옵션

 **출처** : [구성 옵션 - Android Developers](https://developer.android.com/media/camera/camerax/configuration?hl=ko)

### 기본 구성 방법

각 CameraX 사용 사례를 구성하여 사용 사례 작업의 여러 측면을 제어할 수 있습니다.

##### Kotlin 예시

```kotlin
val imageCapture = ImageCapture.Builder()
    .setFlashMode(...)
    .setTargetAspectRatio(...)
    .build()
```

### CameraXConfig

CameraX에는 대부분의 사용 시나리오에 적합한 기본 구성이 있지만, 특별한 요구사항이 있는 경우 `CameraXConfig`를 사용하여 맞춤설정할 수 있습니다.

#### CameraXConfig로 가능한 작업

* `setAvailableCameraLimiter()`로 시작 지연 시간 최적화
* `setCameraExecutor()`를 사용하여 애플리케이션의 실행자 제공
* `setSchedulerHandler()`로 기본 스케줄러 핸들러 교체
* `setMinimumLoggingLevel()`로 로깅 수준 변경

#### 사용 모델

##### Kotlin 예시

```kotlin
class CameraApplication : Application(), CameraXConfig.Provider {
    override fun getCameraXConfig(): CameraXConfig {
        return CameraXConfig.Builder.fromConfig(Camera2Config.defaultConfig())
            .setMinimumLoggingLevel(Log.ERROR).build()
    }
}
```

### 카메라 리미터

애플리케이션이 기기의 특정 카메라만 사용하는 경우, 다른 카메라를 무시하도록 CameraX를 설정하여 시작 지연 시간을 줄일 수 있습니다.

##### Kotlin 예시

```kotlin
class MainApplication : Application(), CameraXConfig.Provider {
    override fun getCameraXConfig(): CameraXConfig {
        return CameraXConfig.Builder.fromConfig(Camera2Config.defaultConfig())
            .setAvailableCamerasLimiter(CameraSelector.DEFAULT_BACK_CAMERA)
            .build()
    }
}
```

### 스레드 관리

#### 카메라 실행자

모든 내부 Camera 플랫폼 API 호출과 콜백에 사용됩니다.

#### 스케줄러 핸들러

카메라를 사용할 수 없을 때 카메라 열기를 다시 시도하는 등 내부 작업을 고정된 간격으로 예약하는 데 사용됩니다.

### 로깅

CameraX는 4가지 로깅 수준을 지원합니다:

1. `Log.DEBUG` (기본값)
2. `Log.INFO`
3. `Log.WARN`
4. `Log.ERROR`

### 자동 선택

CameraX는 앱이 실행되는 기기에 적합한 기능을 자동으로 제공합니다. 해상도를 지정하지 않거나 지정한 해상도가 지원되지 않는 경우 사용할 최적의 해상도를 자동으로 결정합니다.

### 회전 처리

기본적으로 카메라 회전은 사용 사례가 생성되는 동안 기본 디스플레이의 회전과 일치하도록 설정됩니다.

##### Kotlin 회전 설정 예시

```kotlin
override fun onCreate() {
    val imageCapture = ImageCapture.Builder().build()
    val orientationEventListener = object : OrientationEventListener(this as Context) {
        override fun onOrientationChanged(orientation : Int) {
            val rotation : Int = when (orientation) {
                in 45..134 -> Surface.ROTATION_270
                in 135..224 -> Surface.ROTATION_180
                in 225..314 -> Surface.ROTATION_90
                else -> Surface.ROTATION_0
            }
            imageCapture.targetRotation = rotation
        }
    }
    orientationEventListener.enable()
}
```

### 자르기 rect

`ViewPort`와 `UseCaseGroup`을 사용하여 자르기 rect를 맞춤설정할 수 있습니다.

##### Kotlin 예시

```kotlin
val viewPort = ViewPort.Builder(Rational(width, height), display.rotation).build()
val useCaseGroup = UseCaseGroup.Builder()
    .addUseCase(preview)
    .addUseCase(imageAnalysis)
    .addUseCase(imageCapture)
    .setViewPort(viewPort)
    .build()
cameraProvider.bindToLifecycle(lifecycleOwner, cameraSelector, useCaseGroup)
```

### 카메라 선택

#### 기본 선택 옵션

* `CameraSelector.DEFAULT_FRONT_CAMERA`: 기본 전면 카메라
* `CameraSelector.DEFAULT_BACK_CAMERA`: 기본 후면 카메라
* `CameraSelector.Builder.addCameraFilter()`: CameraCharacteristics별로 필터링

#### 동시 카메라 선택 (CameraX 1.3부터)

##### Kotlin 예시

```kotlin
val primary = ConcurrentCamera.SingleCameraConfig(
    primaryCameraSelector, useCaseGroup, lifecycleOwner
)
val secondary = ConcurrentCamera.SingleCameraConfig(
    secondaryCameraSelector, useCaseGroup, lifecycleOwner
)
val concurrentCamera = cameraProvider.bindToLifecycle(
    listOf(primary, secondary)
)
val primaryCamera = concurrentCamera.cameras[0]
val secondaryCamera = concurrentCamera.cameras[1]
```

### 카메라 해상도

#### 자동 해상도

CameraX는 지정된 사용 사례를 바탕으로 최적의 해상도 설정을 자동으로 결정할 수 있습니다.

#### 해상도 지정

##### Kotlin 예시

```kotlin
val imageAnalysis = ImageAnalysis.Builder()
    .setTargetResolution(Size(1280, 720))
    .build()
```

 **주의사항** : 동일한 사용 사례에 타겟 가로세로 비율과 타겟 해상도를 모두 설정할 수는 없습니다.

### 카메라 출력 제어

#### CameraControl 지원 기능

* 확대/축소
* 토치
* 초점 및 측정(탭하여 초점 맞추기)
* 노출 보정

#### CameraControl 및 CameraInfo 인스턴스 가져오기

##### Kotlin 예시

```kotlin
val camera = processCameraProvider.bindToLifecycle(lifecycleOwner, cameraSelector, preview)

// 모든 출력에 영향을 주는 작업 수행용
val cameraControl = camera.cameraControl

// 정보 및 상태 쿼리용
val cameraInfo = camera.cameraInfo
```

#### 확대/축소 제어

##### setZoomRatio()

확대/축소 비율로 확대/축소를 설정합니다.

##### setLinearZoom()

현재 확대/축소를 0에서 1.0 사이의 선형 확대/축소 값으로 설정합니다.

#### 토치 제어

##### Kotlin 예시

```kotlin
// 토치 사용 가능 여부 확인
val hasFlashUnit = camera.cameraInfo.hasFlashUnit()

// 토치 사용 설정/해제
camera.cameraControl.enableTorch(true)

// 현재 토치 상태 쿼리
val torchState = camera.cameraInfo.torchState
```

#### 초점 및 측정

`MeteringPoint`를 생성하고 `FocusMeteringAction`을 사용하여 자동 초점 및 노출 측정을 트리거할 수 있습니다.

##### Kotlin 예시

```kotlin
val meteringPoint1 = meteringPointFactory.createPoint(x1, y1)
val meteringPoint2 = meteringPointFactory.createPoint(x2, y2)
val action = FocusMeteringAction.Builder(meteringPoint1)
    .addPoint(meteringPoint2, FLAG_AF | FLAG_AE)
    .setAutoCancelDuration(3, TimeUnit.SECONDS)
    .build()
val result = cameraControl.startFocusAndMetering(action)
```

#### 노출 보정

노출 보정은 자동 노출(AE) 출력 결과보다 높게 노출 값(EV)을 미세 조정할 때 유용합니다.

##### Kotlin 예시

```kotlin
camera.cameraControl.setExposureCompensationIndex(exposureCompensationIndex)
    .addListener({
        val currentExposureIndex = camera.cameraInfo.exposureState.exposureCompensationIndex
        // 처리 로직
    }, mainExecutor)
```

---

## 확장 프로그램 API

 **출처** : [CameraX 확장 프로그램 API - Android Developers](https://developer.android.com/media/camera/camerax/extensions-api?hl=ko)

### 개요

CameraX는 기기 제조업체가 다양한 Android 기기에 구현한 확장 프로그램에 액세스하기 위한 Extensions API를 제공합니다.

### 지원되는 확장 프로그램 모드

* 야간 모드 (NIGHT)
* HDR
* 자동 (AUTO)
* 빛망울 효과 (BOKEH/세로 모드)
* 얼굴 보정 (FACE RETOUCH)

### 확장 프로그램 아키텍처

CameraX 애플리케이션 → CameraX Extensions API → Camera Extensions OEM 라이브러리

### 이미지 캡처 및 미리보기 확장 프로그램 사용

#### 기본 구현 단계

1. `ExtensionsManager` 인스턴스 검색
2. 확장 프로그램 가용성 확인
3. 확장 프로그램 지원 `CameraSelector` 검색
4. `bindToLifecycle()` 메서드로 확장 프로그램 적용

#### Kotlin 구현 예시

```kotlin
import androidx.camera.extensions.ExtensionMode
import androidx.camera.extensions.ExtensionsManager

override fun onCreate(savedInstanceState: Bundle?) {
    super.onCreate(savedInstanceState)
    val lifecycleOwner = this
    val cameraProviderFuture = ProcessCameraProvider.getInstance(applicationContext)
  
    cameraProviderFuture.addListener({
        val cameraProvider = cameraProviderFuture.get()
        val extensionsManagerFuture = ExtensionsManager.getInstanceAsync(applicationContext, cameraProvider)
      
        extensionsManagerFuture.addListener({
            val extensionsManager = extensionsManagerFuture.get()
            val cameraSelector = CameraSelector.DEFAULT_BACK_CAMERA
          
            // 확장 프로그램 사용 가능 여부 확인
            if (extensionsManager.isExtensionAvailable(cameraSelector, ExtensionMode.NIGHT)) {
                // 모든 사용 사례 바인딩 해제
                try {
                    cameraProvider.unbindAll()
                  
                    // 야간 확장 프로그램 지원 카메라 선택기 검색
                    val nightCameraSelector = extensionsManager.getExtensionEnabledCameraSelector(
                        cameraSelector, ExtensionMode.NIGHT
                    )
                  
                    // 확장 프로그램 지원 카메라 선택기로 이미지 캡처 및 미리보기 사용 사례 바인딩
                    val imageCapture = ImageCapture.Builder().build()
                    val preview = Preview.Builder().build()
                  
                    preview.setSurfaceProvider(surfaceProvider)
                  
                    val camera = cameraProvider.bindToLifecycle(
                        lifecycleOwner, nightCameraSelector, imageCapture, preview
                    )
                } catch (e: Exception) {
                    Log.e(TAG, "Use case binding failed", e)
                }
            }
        }, ContextCompat.getMainExecutor(this))
    }, ContextCompat.getMainExecutor(this))
}
```

#### Java 구현 예시

```java
import androidx.camera.extensions.ExtensionMode;
import androidx.camera.extensions.ExtensionsManager;

@Override
protected void onCreate(Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    final LifecycleOwner lifecycleOwner = this;
    final ListenableFuture<ProcessCameraProvider> cameraProviderFuture = 
        ProcessCameraProvider.getInstance(getApplicationContext());
  
    cameraProviderFuture.addListener(() -> {
        try {
            final ProcessCameraProvider cameraProvider = cameraProviderFuture.get();
            final ListenableFuture<ExtensionsManager> extensionsManagerFuture = 
                ExtensionsManager.getInstanceAsync(getApplicationContext(), cameraProvider);
          
            extensionsManagerFuture.addListener(() -> {
                try {
                    final ExtensionsManager extensionsManager = extensionsManagerFuture.get();
                    final CameraSelector cameraSelector = CameraSelector.DEFAULT_BACK_CAMERA;
                  
                    if (extensionsManager.isExtensionAvailable(cameraSelector, ExtensionMode.NIGHT)) {
                        cameraProvider.unbindAll();
                      
                        final CameraSelector nightCameraSelector = extensionsManager
                            .getExtensionEnabledCameraSelector(cameraSelector, ExtensionMode.NIGHT);
                      
                        final ImageCapture imageCapture = new ImageCapture.Builder().build();
                        final Preview preview = new Preview.Builder().build();
                      
                        preview.setSurfaceProvider(surfaceProvider);
                      
                        cameraProvider.bindToLifecycle(
                            lifecycleOwner, nightCameraSelector, imageCapture, preview
                        );
                    }
                } catch (ExecutionException | InterruptedException e) {
                    throw new RuntimeException(e);
                }
            }, ContextCompat.getMainExecutor(this));
        } catch (ExecutionException | InterruptedException e) {
            throw new RuntimeException(e);
        }
    }, ContextCompat.getMainExecutor(this));
}
```

### 확장 프로그램 사용 중지

공급업체 확장 프로그램을 사용 중지하려면:

1. 모든 사용 사례의 바인딩 해제
2. 이미지 캡처 및 미리보기 사용 사례를 일반 카메라 선택기로 다시 바인딩
   * 예: `CameraSelector.DEFAULT_BACK_CAMERA` 사용

### 종속 항목

#### Gradle 설정

##### Kotlin

```kotlin
dependencies {
    val camerax_version = "1.2.0-rc01"
    implementation("androidx.camera:camera-core:${camerax_version}")
    implementation("androidx.camera:camera-camera2:${camerax_version}")
    implementation("androidx.camera:camera-lifecycle:${camerax_version}")
  
    // CameraX Extensions library
    implementation("androidx.camera:camera-extensions:${camerax_version}")
}
```

##### Groovy

```groovy
dependencies {
    def camerax_version = "1.2.0-rc01"
    implementation "androidx.camera:camera-core:${camerax_version}"
    implementation "androidx.camera:camera-camera2:${camerax_version}"
    implementation "androidx.camera:camera-lifecycle:${camerax_version}"
  
    // CameraX Extensions library
    implementation "androidx.camera:camera-extensions:${camerax_version}"
}
```

### 기존 API 삭제 안내

* **중요** : 1.0.0-alpha26 버전부터 새 Extensions API가 출시됨
* 2019년 8월에 출시된 기존 Extensions API는 지원 중단됨
* 1.0.0-alpha28 버전부터 기존 Extensions API가 라이브러리에서 삭제됨
* 기존 Extensions API를 사용하는 애플리케이션은 새로운 Extensions API로 이전 필요

---

## 추가 리소스 및 참고 문서

### 공식 리소스

1. **Codelab** : [CameraX 시작하기](https://codelabs.developers.google.com/codelabs/camerax-getting-started?hl=ko)
2. **코드 샘플** : [CameraX 샘플 앱](https://github.com/android/camera-samples/)
3. **QR 코드 스캐너 샘플** : [CameraX-MLKit](https://github.com/android/camera-samples/tree/main/CameraX-MLKit)
4. **CameraController 기본사항 동영상** : [유튜브 링크](https://www.youtube.com/watch?v=fazzQs-O31U&hl=ko)

### 기타 참고 문서

* [CameraX 공급업체 확장 프로그램 유효성 검사 도구](https://source.android.com/devices/camera/camerax-vendor-extensions-validation-tool?hl=ko)
* [카메라 확장 프로그램 문서](https://developer.android.com/training/camera/camera-extensions?hl=ko)
* [지원되는 기기 목록](https://developer.android.com/training/camera/supported-devices?hl=ko)

---

 **작성일** : 2025년 9월 27일

 **근거** : Android Developers 공식 문서 세 개 문서를 기반으로 작성

 **출처** :

1. [CameraX 아키텍처](https://developer.android.com/media/camera/camerax/architecture?hl=ko)
2. [구성 옵션](https://developer.android.com/media/camera/camerax/configuration?hl=ko)
3. [CameraX 확장 프로그램 API](https://developer.android.com/media/camera/camerax/extensions-api?hl=ko)
