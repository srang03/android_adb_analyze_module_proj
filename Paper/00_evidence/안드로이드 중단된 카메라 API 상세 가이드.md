# 지원 중단된 카메라 API 상세 가이드

## 목차

1. [카메라 API 개요](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EC%B9%B4%EB%A9%94%EB%9D%BC-api-%EA%B0%9C%EC%9A%94)
2. [사진 촬영 기본사항](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EC%82%AC%EC%A7%84-%EC%B4%AC%EC%98%81-%EA%B8%B0%EB%B3%B8%EC%82%AC%ED%95%AD)
3. [카메라 직접 제어](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EC%B9%B4%EB%A9%94%EB%9D%BC-%EC%A7%81%EC%A0%91-%EC%A0%9C%EC%96%B4)
4. [카메라 API 상세 가이드](https://claude.ai/chat/35cf6761-f634-4574-bff5-e134f7089790#%EC%B9%B4%EB%A9%94%EB%9D%BC-api-%EC%83%81%EC%84%B8-%EA%B0%80%EC%9D%B4%EB%93%9C)

---

 **⚠️ 중요한 주의사항** : 이 문서에서 다루는 모든 내용은 **지원 중단된** Camera 클래스에 관한 것입니다. **CameraX** 또는 특정 사용 사례의 경우 **Camera2**를 사용하는 것이 강력히 권장됩니다. CameraX와 Camera2는 모두 Android 5.0(API 수준 21) 이상을 지원합니다.

---

## 카메라 API 개요

 **출처** : [카메라 - Android Developers](https://developer.android.com/media/camera/camera-deprecated?hl=ko)

### 기본 개념

Android는 기기의 카메라 하드웨어에 대한 전체 액세스 권한을 제공하므로 광범위한 카메라 앱 또는 비전 기반 앱을 빌드할 수 있습니다. 또는 사용자가 사진을 캡처하는 방법만 필요하다면 간단히 기존 카메라 앱에 사진을 캡처하고 반환하도록 요청하면 됩니다.

### 권장사항

* **새로운 프로젝트** : CameraX Jetpack 라이브러리 사용 권장
* **특정 고급 기능 필요시** : Camera2 API 사용
* **지원 중단된 Camera API** : 새로운 개발에는 사용 권장하지 않음

---

## 사진 촬영 기본사항

 **출처** : [사진 촬영 - Android Developers](https://developer.android.com/media/camera/camera-deprecated/photobasics?hl=ko)

### 개요

이 과정에서는 기기의 다른 카메라 앱에 사진 촬영 작업을 위임하여 사진을 캡처하는 방법을 설명합니다. 자체 카메라 기능을 빌드하려면 카메라 제어 섹션을 참고하세요.

### 카메라 기능 요청

애플리케이션의 필수 기능이 사진 촬영이라면 Google Play에서 공개 상태를 카메라가 있는 기기로 제한하세요.

#### 매니페스트 설정

```xml
<manifest ... >
    <uses-feature android:name="android.hardware.camera" android:required="true" />
    ...
</manifest>
```

 **선택적 카메라 사용** : 카메라를 사용하지만 꼭 필요한 것이 아니라면 `android:required="false"`로 설정하고 런타임에 `hasSystemFeature(PackageManager.FEATURE_CAMERA_ANY)`를 호출하여 카메라 사용 가능성을 확인합니다.

### 썸네일 가져오기

Android 카메라 애플리케이션은 `onActivityResult()`에 전달된 반환 Intent의 "data" 키 아래 extras에 작은 Bitmap으로 사진을 인코딩합니다.

#### Kotlin 예시

```kotlin
override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
    if (requestCode == REQUEST_IMAGE_CAPTURE && resultCode == RESULT_OK) {
        val imageBitmap = data.extras.get("data") as Bitmap
        imageView.setImageBitmap(imageBitmap)
    }
}
```

#### Java 예시

```java
@Override
protected void onActivityResult(int requestCode, int resultCode, Intent data) {
    if (requestCode == REQUEST_IMAGE_CAPTURE && resultCode == RESULT_OK) {
        Bundle extras = data.getExtras();
        Bitmap imageBitmap = (Bitmap) extras.get("data");
        imageView.setImageBitmap(imageBitmap);
    }
}
```

 **참고** : "data"에서 가져온 썸네일 이미지는 아이콘으로 사용하기에는 좋지만, 그 이상은 아닙니다.

### 원본 크기의 사진 저장

Android 카메라 애플리케이션은 저장할 파일을 받으면 원본 크기의 사진을 저장합니다.

#### 저장소 권한

##### Android 9 이하

```xml
<manifest ...>
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
    ...
</manifest>
```

##### Android 10 이상

Android 10(API 수준 29) 이상에서 사진 공유를 위한 적절한 디렉터리는 `MediaStore.Images` 테이블입니다.

#### 파일 생성

##### Kotlin 예시

```kotlin
lateinit var currentPhotoPath: String

@Throws(IOException::class)
private fun createImageFile(): File {
    // 이미지 파일 이름 생성
    val timeStamp: String = SimpleDateFormat("yyyyMMdd_HHmmss").format(Date())
    val storageDir: File = getExternalFilesDir(Environment.DIRECTORY_PICTURES)
    return File.createTempFile(
        "JPEG_${timeStamp}_", /* prefix */
        ".jpg", /* suffix */
        storageDir /* directory */
    ).apply {
        // ACTION_VIEW 인텐트와 함께 사용할 파일 경로 저장
        currentPhotoPath = absolutePath
    }
}
```

##### Java 예시

```java
String currentPhotoPath;

private File createImageFile() throws IOException {
    // 이미지 파일 이름 생성
    String timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss").format(new Date());
    String imageFileName = "JPEG_" + timeStamp + "_";
    File storageDir = getExternalFilesDir(Environment.DIRECTORY_PICTURES);
    File image = File.createTempFile(
        imageFileName,  /* prefix */
        ".jpg",         /* suffix */
        storageDir      /* directory */
    );

    // ACTION_VIEW 인텐트와 함께 사용할 파일 경로 저장
    currentPhotoPath = image.getAbsolutePath();
    return image;
}
```

#### 카메라 Intent 실행

##### Kotlin 예시

```kotlin
private fun dispatchTakePictureIntent() {
    Intent(MediaStore.ACTION_IMAGE_CAPTURE).also { takePictureIntent ->
        // 인텐트를 처리할 카메라 활동이 있는지 확인
        takePictureIntent.resolveActivity(packageManager)?.also {
            // 사진이 저장될 파일 생성
            val photoFile: File? = try {
                createImageFile()
            } catch (ex: IOException) {
                // 파일 생성 중 오류 발생
                null
            }
            // 파일이 성공적으로 생성된 경우에만 계속
            photoFile?.also {
                val photoURI: Uri = FileProvider.getUriForFile(
                    this,
                    "com.example.android.fileprovider",
                    it
                )
                takePictureIntent.putExtra(MediaStore.EXTRA_OUTPUT, photoURI)
                startActivityForResult(takePictureIntent, REQUEST_IMAGE_CAPTURE)
            }
        }
    }
}
```

#### FileProvider 구성

##### 매니페스트 설정

```xml
<application>
    ...
    <provider
        android:name="androidx.core.content.FileProvider"
        android:authorities="com.example.android.fileprovider"
        android:exported="false"
        android:grantUriPermissions="true">
        <meta-data
            android:name="android.support.FILE_PROVIDER_PATHS"
            android:resource="@xml/file_paths"></meta-data>
    </provider>
    ...
</application>
```

##### file_paths.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<paths xmlns:android="http://schemas.android.com/apk/res/android">
    <external-files-path name="my_images" path="Pictures" />
</paths>
```

### 갤러리에 사진 추가

#### Kotlin 예시

```kotlin
private fun galleryAddPic() {
    Intent(Intent.ACTION_MEDIA_SCANNER_SCAN_FILE).also { mediaScanIntent ->
        val f = File(currentPhotoPath)
        mediaScanIntent.data = Uri.fromFile(f)
        sendBroadcast(mediaScanIntent)
    }
}
```

#### Java 예시

```java
private void galleryAddPic() {
    Intent mediaScanIntent = new Intent(Intent.ACTION_MEDIA_SCANNER_SCAN_FILE);
    File f = new File(currentPhotoPath);
    Uri contentUri = Uri.fromFile(f);
    mediaScanIntent.setData(contentUri);
    this.sendBroadcast(mediaScanIntent);
}
```

### 크기가 조정된 이미지 디코딩

#### Kotlin 예시

```kotlin
private fun setPic() {
    // View의 크기 가져오기
    val targetW: Int = imageView.width
    val targetH: Int = imageView.height

    val bmOptions = BitmapFactory.Options().apply {
        // 비트맵의 크기 가져오기
        inJustDecodeBounds = true
        BitmapFactory.decodeFile(currentPhotoPath, bmOptions)
        val photoW: Int = outWidth
        val photoH: Int = outHeight

        // 이미지를 얼마나 축소할지 결정
        val scaleFactor: Int = Math.max(1, Math.min(photoW / targetW, photoH / targetH))

        // View를 채우도록 크기가 조정된 비트맵으로 이미지 파일 디코딩
        inJustDecodeBounds = false
        inSampleSize = scaleFactor
        inPurgeable = true
    }
    BitmapFactory.decodeFile(currentPhotoPath, bmOptions)?.also { bitmap ->
        imageView.setImageBitmap(bitmap)
    }
}
```

---

## 카메라 직접 제어

 **출처** : [카메라 제어하기 - Android Developers](https://developer.android.com/media/camera/camera-deprecated/cameradirect?hl=ko)

### 개요

기기의 카메라를 직접 제어하려면 기존 카메라 애플리케이션에 사진이나 동영상을 요청하는 것보다 더 많은 코드가 필요합니다. 그러나 전문 카메라 애플리케이션 또는 앱 UI에 완전히 통합된 기능을 빌드하려는 경우 이 과정에서 그 방법을 보여줍니다.

### 카메라 객체 열기

Camera 객체에서 인스턴스를 가져오는 것이 카메라를 직접 제어하는 절차의 첫 번째 단계입니다.

#### Kotlin 예시

```kotlin
private fun safeCameraOpen(id: Int): Boolean {
    return try {
        releaseCameraAndPreview()
        mCamera = Camera.open(id)
        true
    } catch (e: Exception) {
        Log.e(getString(R.string.app_name), "failed to open Camera")
        e.printStackTrace()
        false
    }
}

private fun releaseCameraAndPreview() {
    preview?.setCamera(null)
    mCamera?.also { camera ->
        camera.release()
        mCamera = null
    }
}
```

#### Java 예시

```java
private boolean safeCameraOpen(int id) {
    boolean qOpened = false;
  
    try {
        releaseCameraAndPreview();
        camera = Camera.open(id);
        qOpened = (camera != null);
    } catch (Exception e) {
        Log.e(getString(R.string.app_name), "failed to open Camera");
        e.printStackTrace();
    }
    return qOpened;
}

private void releaseCameraAndPreview() {
    preview.setCamera(null);
    if (camera != null) {
        camera.release();
        camera = null;
    }
}
```

 **참고** : API 수준 9 이후로 카메라 프레임워크는 다중 카메라를 지원합니다. 기존 API를 사용하고 인수 없이 `open()`을 호출하면 첫 번째 후면 카메라를 가져옵니다.

### 카메라 미리보기 만들기

일반적으로 사진을 촬영하려면 사용자가 셔터를 클릭하기 전에 피사체를 미리 봐야 합니다. 그렇게 하려면 SurfaceView를 사용하여 카메라 센서에 포착된 피사체의 미리보기를 생성하면 됩니다.

#### 미리보기 클래스

##### Kotlin 예시

```kotlin
class Preview(
    context: Context,
    val surfaceView: SurfaceView = SurfaceView(context)
) : ViewGroup(context), SurfaceHolder.Callback {

    var mHolder: SurfaceHolder = surfaceView.holder.apply {
        addCallback(this@Preview)
        setType(SurfaceHolder.SURFACE_TYPE_PUSH_BUFFERS)
    }
  
    // ... 기타 구현
}
```

##### Java 예시

```java
class Preview extends ViewGroup implements SurfaceHolder.Callback {
    SurfaceView surfaceView;
    SurfaceHolder holder;

    Preview(Context context) {
        super(context);

        surfaceView = new SurfaceView(context);
        addView(surfaceView);

        // SurfaceHolder.Callback 설치하여 기본 표면이 생성되고 파괴될 때 알림을 받습니다.
        holder = surfaceView.getHolder();
        holder.addCallback(this);
        holder.setType(SurfaceHolder.SURFACE_TYPE_PUSH_BUFFERS);
    }
  
    // ... 기타 구현
}
```

### 미리보기 설정 및 시작

#### Kotlin 예시

```kotlin
fun setCamera(camera: Camera?) {
    if (mCamera == camera) {
        return
    }

    stopPreviewAndFreeCamera()

    mCamera = camera

    mCamera?.apply {
        mSupportedPreviewSizes = parameters.supportedPreviewSizes
        requestLayout()

        try {
            setPreviewDisplay(holder)
        } catch (e: IOException) {
            e.printStackTrace()
        }

        // 중요: startPreview()를 호출하여 미리보기 표면 업데이트를 시작합니다.
        // 사진을 찍기 전에 미리보기를 시작해야 합니다.
        startPreview()
    }
}
```

#### Java 예시

```java
public void setCamera(Camera camera) {
    if (mCamera == camera) { return; }

    stopPreviewAndFreeCamera();

    mCamera = camera;

    if (mCamera != null) {
        List<Size> localSizes = mCamera.getParameters().getSupportedPreviewSizes();
        supportedPreviewSizes = localSizes;
        requestLayout();

        try {
            mCamera.setPreviewDisplay(holder);
        } catch (IOException e) {
            e.printStackTrace();
        }

        // 중요: startPreview()를 호출하여 미리보기 표면 업데이트를 시작합니다.
        // 사진을 찍기 전에 미리보기를 시작해야 합니다.
        mCamera.startPreview();
    }
}
```

### 카메라 설정 수정

#### Kotlin 예시

```kotlin
override fun surfaceChanged(holder: SurfaceHolder, format: Int, w: Int, h: Int) {
    mCamera?.apply {
        // 이제 크기가 알려졌으므로 카메라 매개변수를 설정하고 미리보기를 시작합니다.
        parameters?.also { params ->
            params.setPreviewSize(previewSize.width, previewSize.height)
            requestLayout()
            parameters = params
        }

        // 중요: startPreview()를 호출하여 미리보기 표면 업데이트를 시작합니다.
        // 사진을 찍기 전에 미리보기를 시작해야 합니다.
        startPreview()
    }
}
```

### 미리보기 방향 설정

카메라 센서의 자연스러운 방향은 가로 모드이기 때문에 대부분의 카메라 애플리케이션은 화면을 가로 모드로 잠급니다. `setCameraDisplayOrientation()` 메서드를 사용하면 이미지가 저장되는 방법에 영향을 주지 않고 미리보기 표시 방법을 변경할 수 있습니다.

### 사진 촬영

미리보기가 시작되면 `Camera.takePicture()` 메서드를 사용하여 사진을 촬영하세요. `Camera.ShutterCallback`과 `Camera.PictureCallback` 객체를 만들고 `Camera.takePicture()`로 전달할 수 있습니다.

### 미리보기 다시 시작

사진을 촬영한 후에는 사용자가 다른 사진을 촬영하기 전에 미리보기를 다시 시작해야 합니다.

#### Kotlin 예시

```kotlin
fun onClick(v: View) {
    previewState = if (previewState == K_STATE_FROZEN) {
        camera?.startPreview()
        K_STATE_PREVIEW
    } else {
        camera?.takePicture(null, rawCallback, null)
        K_STATE_BUSY
    }
    shutterBtnConfig()
}
```

### 미리보기 중지 및 카메라 해제

#### Kotlin 예시

```kotlin
override fun surfaceDestroyed(holder: SurfaceHolder) {
    // 반환할 때 표면이 파괴되므로 미리보기를 중지합니다.
    // stopPreview()를 호출하여 미리보기 표면 업데이트를 중지합니다.
    mCamera?.stopPreview()
}

/**
 * 이 함수가 반환되면 mCamera는 null이 됩니다.
 */
private fun stopPreviewAndFreeCamera() {
    mCamera?.apply {
        // stopPreview()를 호출하여 미리보기 표면 업데이트를 중지합니다.
        stopPreview()

        // 중요: release()를 호출하여 다른 애플리케이션에서 사용할 수 있도록 카메라를 해제합니다.
        // 애플리케이션은 onPause() 중에 즉시 카메라를 해제하고 onResume() 중에 다시 열어야 합니다.
        release()
        mCamera = null
    }
}
```

---

## 카메라 API 상세 가이드

 **출처** : [카메라 API - Android Developers](https://developer.android.com/media/camera/camera-deprecated/camera-api?hl=ko)

### 고려사항

Android 기기에서 카메라를 사용하도록 애플리케이션을 활성화하기 전에 앱이 이 하드웨어 기능을 어떻게 사용할 것인지 몇 가지 질문을 고민해야 합니다:

* **카메라 요구사항** : 카메라가 없는 기기에는 애플리케이션 설치를 원하지 않을 정도로 애플리케이션에 카메라 사용이 중요한가요?
* **빠른 사진 또는 맞춤설정 카메라** : 애플리케이션은 카메라를 어떻게 사용할 예정인가요?
* **포그라운드 서비스 요구사항** : 앱은 언제 카메라와 상호작용하나요? Android 9(API 수준 28) 이상에서는 백그라운드에서 실행되는 앱이 카메라에 액세스할 수 없습니다.
* **저장소** : 애플리케이션에서 생성한 이미지나 동영상이 애플리케이션에만 표시되기를 원하시나요?

### 기본 사항

Android 프레임워크는 `android.hardware.camera2` API 또는 카메라 `Intent`를 통해 이미지와 동영상 캡처를 지원합니다.

#### 관련 클래스

* **android.hardware.camera2** : 이 패키지는 기기 카메라를 제어하기 위한 기본 API입니다.
* **Camera** : 이 클래스는 기기 카메라를 제어하는 API로, 이미 지원이 중단되었습니다.
* **SurfaceView** : 이 클래스는 사용자에게 실시간 카메라 미리보기를 보여주는 데 사용합니다.
* **MediaRecorder** : 이 클래스는 카메라에서 동영상을 녹화하는 데 사용합니다.
* **Intent** : 인텐트 작업 유형 `MediaStore.ACTION_IMAGE_CAPTURE` 또는 `MediaStore.ACTION_VIDEO_CAPTURE`를 사용하면 Camera 객체를 직접 사용하지 않고도 이미지나 동영상을 캡처할 수 있습니다.

### 매니페스트 선언

#### 카메라 권한

```xml
<uses-permission android:name="android.permission.CAMERA" />
```

 **참고** : 기존 카메라 앱을 호출하여 카메라를 사용하고 있는 경우, 애플리케이션이 이 권한을 요청하지 않아도 됩니다.

#### 카메라 기능

```xml
<uses-feature android:name="android.hardware.camera" />
```

#### 저장소 권한

```xml
<!-- Android 10(API 수준 29) 이하를 대상으로 하는 경우 -->
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

#### 오디오 녹음 권한

```xml
<uses-permission android:name="android.permission.RECORD_AUDIO" />
```

#### 위치 권한

```xml
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<!-- Android 5.0(API 수준 21) 이상을 대상으로 하는 경우 -->
<uses-feature android:name="android.hardware.location.gps" />
```

### 카메라 하드웨어 감지

#### Kotlin 예시

```kotlin
/** 이 기기에 카메라가 있는지 확인 */
private fun checkCameraHardware(context: Context): Boolean {
    return if (context.packageManager.hasSystemFeature(PackageManager.FEATURE_CAMERA)) {
        // 이 기기에는 카메라가 있습니다
        true
    } else {
        // 이 기기에는 카메라가 없습니다
        false
    }
}
```

#### Java 예시

```java
/** 이 기기에 카메라가 있는지 확인 */
private boolean checkCameraHardware(Context context) {
    if (context.getPackageManager().hasSystemFeature(PackageManager.FEATURE_CAMERA)){
        // 이 기기에는 카메라가 있습니다
        return true;
    } else {
        // 이 기기에는 카메라가 없습니다
        return false;
    }
}
```

### 카메라 액세스

#### Kotlin 예시

```kotlin
/** Camera 객체의 인스턴스를 가져오는 안전한 방법 */
fun getCameraInstance(): Camera? {
    return try {
        Camera.open() // Camera 인스턴스 가져오기 시도
    } catch (e: Exception) {
        // 카메라를 사용할 수 없음 (사용 중이거나 존재하지 않음)
        null // 카메라를 사용할 수 없는 경우 null 반환
    }
}
```

#### Java 예시

```java
/** Camera 객체의 인스턴스를 가져오는 안전한 방법 */
public static Camera getCameraInstance(){
    Camera c = null;
    try {
        c = Camera.open(); // Camera 인스턴스 가져오기 시도
    }
    catch (Exception e){
        // 카메라를 사용할 수 없음 (사용 중이거나 존재하지 않음)
    }
    return c; // 카메라를 사용할 수 없는 경우 null 반환
}
```

### 미리보기 클래스 만들기

#### Kotlin 예시

```kotlin
/** 기본 카메라 미리보기 클래스 */
class CameraPreview(
    context: Context,
    private val mCamera: Camera
) : SurfaceView(context), SurfaceHolder.Callback {

    private val mHolder: SurfaceHolder = holder.apply {
        // SurfaceHolder.Callback을 설치하여 기본 표면이 생성되고 파괴될 때 알림을 받습니다.
        addCallback(this@CameraPreview)
        // Android 3.0 이전 버전에서 필요한 지원 중단된 설정
        setType(SurfaceHolder.SURFACE_TYPE_PUSH_BUFFERS)
    }

    override fun surfaceCreated(holder: SurfaceHolder) {
        // 표면이 생성되었으므로 이제 카메라에 미리보기를 그릴 위치를 알려줍니다.
        mCamera.apply {
            try {
                setPreviewDisplay(holder)
                startPreview()
            } catch (e: IOException) {
                Log.d(TAG, "Error setting camera preview: ${e.message}")
            }
        }
    }

    override fun surfaceDestroyed(holder: SurfaceHolder) {
        // empty. 활동에서 카메라 미리보기 해제를 처리합니다.
    }

    override fun surfaceChanged(holder: SurfaceHolder, format: Int, w: Int, h: Int) {
        // 미리보기가 변경되거나 회전할 수 있는 경우 여기서 해당 이벤트를 처리합니다.
        // 크기 조정이나 다시 포맷하기 전에 미리보기를 중지해야 합니다.
        if (mHolder.surface == null) {
            // 미리보기 표면이 존재하지 않음
            return
        }

        // 변경하기 전에 미리보기 중지
        try {
            mCamera.stopPreview()
        } catch (e: Exception) {
            // ignore: 존재하지 않는 미리보기를 중지하려고 시도함
        }

        // 여기서 미리보기 크기를 설정하고 크기 조정, 회전 또는 다시 포맷 변경을 수행합니다

        // 새 설정으로 미리보기 시작
        mCamera.apply {
            try {
                setPreviewDisplay(mHolder)
                startPreview()
            } catch (e: Exception) {
                Log.d(TAG, "Error starting camera preview: ${e.message}")
            }
        }
    }
}
```

### 레이아웃에 미리보기 배치

#### XML 레이아웃 예시

```xml
<?xml version="1.0" encoding="utf-8"?>
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android"
    android:orientation="horizontal"
    android:layout_width="fill_parent"
    android:layout_height="fill_parent">
    <FrameLayout
        android:id="@+id/camera_preview"
        android:layout_width="fill_parent"
        android:layout_height="fill_parent"
        android:layout_weight="1" />

    <Button
        android:id="@+id/button_capture"
        android:text="Capture"
        android:layout_width="wrap_content"
        android:layout_height="wrap_content"
        android:layout_gravity="center" />
</LinearLayout>
```

#### Activity에서 미리보기 추가

##### Kotlin 예시

```kotlin
class CameraActivity : Activity() {
    private var mCamera: Camera? = null
    private var mPreview: CameraPreview? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        // Camera 인스턴스 생성
        mCamera = getCameraInstance()

        mPreview = mCamera?.let {
            // 미리보기 뷰 생성
            CameraPreview(this, it)
        }

        // 미리보기 뷰를 활동의 콘텐츠로 설정합니다.
        mPreview?.also {
            val preview: FrameLayout = findViewById(R.id.camera_preview)
            preview.addView(it)
        }
    }
}
```

### 사진 캡처

#### Kotlin 예시

```kotlin
private val mPicture = Camera.PictureCallback { data, _ ->
    val pictureFile: File = getOutputMediaFile(MEDIA_TYPE_IMAGE) ?: run {
        Log.d(TAG, ("Error creating media file, check storage permissions"))
        return@PictureCallback
    }

    try {
        val fos = FileOutputStream(pictureFile)
        fos.write(data)
        fos.close()
    } catch (e: FileNotFoundException) {
        Log.d(TAG, "File not found: ${e.message}")
    } catch (e: IOException) {
        Log.d(TAG, "Error accessing file: ${e.message}")
    }
}

// 캡처 버튼에 리스너 추가
val captureButton: Button = findViewById(R.id.button_capture)
captureButton.setOnClickListener {
    // 카메라에서 이미지 가져오기
    mCamera?.takePicture(null, null, mPicture)
}
```

### 동영상 캡처

동영상을 캡처하려면 Camera 객체를 신중하게 관리하고 MediaRecorder 클래스와 조정해야 합니다.

#### 동영상 녹화 순서

1. **카메라 열기** : `Camera.open()`을 사용하여 카메라 객체의 인스턴스를 가져옵니다.
2. **미리보기 연결** : `Camera.setPreviewDisplay()`를 사용하여 SurfaceView를 카메라에 연결합니다.
3. **미리보기 시작** : `Camera.startPreview()`를 호출하여 실시간 카메라 이미지를 표시하기 시작합니다.
4. **동영상 녹화 시작** : 다음 단계를 순서대로 완료해야 합니다:

* 카메라 잠금 해제: `Camera.unlock()`을 호출하여 MediaRecorder에서 사용할 카메라의 잠금을 해제합니다.
* MediaRecorder 구성: 특정 순서에 따라 구성 단계를 실행합니다.
* MediaRecorder 준비: `MediaRecorder.prepare()`를 호출합니다.
* MediaRecorder 시작: `MediaRecorder.start()`를 호출합니다.

1. **동영상 녹화 중지** : 다음 메서드를 순서대로 호출합니다:

* MediaRecorder 중지: `MediaRecorder.stop()`
* MediaRecorder 재설정: `MediaRecorder.reset()`
* MediaRecorder 해제: `MediaRecorder.release()`
* 카메라 잠금: `Camera.lock()`

#### MediaRecorder 구성 예시

##### Kotlin 예시

```kotlin
private fun prepareVideoRecorder(): Boolean {
    mediaRecorder = MediaRecorder()

    mCamera?.let { camera ->
        // 1단계: 카메라 잠금 해제 및 MediaRecorder에 설정
        camera.unlock()

        mediaRecorder?.run {
            setCamera(camera)

            // 2단계: 소스 설정
            setAudioSource(MediaRecorder.AudioSource.CAMCORDER)
            setVideoSource(MediaRecorder.VideoSource.CAMERA)

            // 3단계: CamcorderProfile 설정 (API 수준 8 이상 필요)
            setProfile(CamcorderProfile.get(CamcorderProfile.QUALITY_HIGH))

            // 4단계: 출력 파일 설정
            setOutputFile(getOutputMediaFile(MEDIA_TYPE_VIDEO).toString())

            // 5단계: 미리보기 출력 설정
            setPreviewDisplay(mPreview?.holder?.surface)

            // 6단계: 구성된 MediaRecorder 준비
            return try {
                prepare()
                true
            } catch (e: IllegalStateException) {
                Log.d(TAG, "IllegalStateException preparing MediaRecorder: ${e.message}")
                releaseMediaRecorder()
                false
            } catch (e: IOException) {
                Log.d(TAG, "IOException preparing MediaRecorder: ${e.message}")
                releaseMediaRecorder()
                false
            }
        }
    }
    return false
}
```

### 카메라 해제

#### Kotlin 예시

```kotlin
class CameraActivity : Activity() {
    private var mCamera: Camera? = null
    private var preview: SurfaceView? = null
    private var mediaRecorder: MediaRecorder? = null

    override fun onPause() {
        super.onPause()
        releaseMediaRecorder() // MediaRecorder를 사용하는 경우 먼저 해제
        releaseCamera() // 일시중지 이벤트에서 즉시 카메라 해제
    }

    private fun releaseMediaRecorder() {
        mediaRecorder?.reset() // 녹음기 구성 지우기
        mediaRecorder?.release() // 녹음기 객체 해제
        mediaRecorder = null
        mCamera?.lock() // 나중에 사용할 수 있도록 카메라 잠금
    }

    private fun releaseCamera() {
        mCamera?.release() // 다른 애플리케이션용으로 카메라 해제
        mCamera = null
    }
}
```

### 미디어 파일 저장

#### Kotlin 예시

```kotlin
val MEDIA_TYPE_IMAGE = 1
val MEDIA_TYPE_VIDEO = 2

/** 이미지 또는 동영상 저장용 파일 Uri 생성 */
private fun getOutputMediaFileUri(type: Int): Uri {
    return Uri.fromFile(getOutputMediaFile(type))
}

/** 이미지 또는 동영상 저장용 파일 생성 */
private fun getOutputMediaFile(type: Int): File? {
    // 안전을 위해 Environment.getExternalStorageState()를 사용하여 
    // SDCard가 마운트되었는지 확인해야 합니다.

    val mediaStorageDir = File(
        Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_PICTURES),
        "MyCameraApp"
    )

    // 이 위치는 생성된 이미지를 애플리케이션 간에 공유하고 
    // 앱을 제거한 후에도 유지하려는 경우에 가장 적합합니다.

    // 저장소 디렉터리가 존재하지 않는 경우 생성
    mediaStorageDir.apply {
        if (!exists()) {
            if (!mkdirs()) {
                Log.d("MyCameraApp", "failed to create directory")
                return null
            }
        }
    }

    // 미디어 파일 이름 생성
    val timeStamp = SimpleDateFormat("yyyyMMdd_HHmmss").format(Date())
    return when (type) {
        MEDIA_TYPE_IMAGE -> {
            File("${mediaStorageDir.path}${File.separator}IMG_$timeStamp.jpg")
        }
        MEDIA_TYPE_VIDEO -> {
            File("${mediaStorageDir.path}${File.separator}VID_$timeStamp.mp4")
        }
        else -> null
    }
}
```

### 카메라 기능

#### 보편적인 카메라 기능

| 기능                  | API 수준 | 설명                                                   |
| --------------------- | -------- | ------------------------------------------------------ |
| Face Detection        | 14       | 이미지에서 인간의 얼굴을 식별하고 초점을 맞춤          |
| Metering Areas        | 14       | 이미지의 하나 이상 영역에서 광량 측정                  |
| Focus Areas           | 14       | 이미지의 하나 이상 영역에서 카메라 초점 설정           |
| White Balance Lock    | 14       | 자동 화이트 밸런스 중지 및 시작                        |
| Exposure Lock         | 14       | 자동 노출 중지 및 시작                                 |
| Video Snapshot        | 14       | 동영상을 녹화하는 동안 사진 촬영                       |
| Time lapse video      | 11       | 설정된 간격으로 프레임을 캡처하여 타임랩스 동영상 생성 |
| Multiple Cameras      | 9        | 기기에서 둘 이상의 카메라 지원 (전면 및 후면 포함)     |
| Focus Distance        | 9        | 객체가 선명한 초점에 나타나는 거리 보고                |
| Zoom                  | 8        | 이미지 확대/축소 설정                                  |
| Exposure Compensation | 8        | 더 밝거나 어둡게 노출 수준 증가 또는 감소              |
| GPS Data              | 5        | 이미지에 지리적 위치 정보 포함 또는 생략               |
| White Balance         | 5        | 조명 조건에 따라 색상 효과 설정                        |
| Focus Mode            | 5        | 매크로, 고정 또는 자동초점 등의 카메라 초점 방법 설정  |
| Scene Mode            | 5        | 특정 유형의 사진 시나리오에 사전 설정된 모드 적용      |
| JPEG Quality          | 5        | JPEG 이미지의 압축 수준 설정                           |
| Flash Mode            | 5        | 자동, 켜기, 끄기 또는 빨간 눈 감소로 플래시 설정       |
| Color Effects         | 5        | 세피아, 포스터라이즈, 아쿠아 등의 색상 효과 적용       |
| Anti-Banding          | 5        | WVGA 카메라 미리보기의 색상 그라데이션 밴딩을 줄임     |
| Picture Format        | 1        | 이미지의 파일 형식 지정                                |
| Picture Size          | 1        | 저장된 이미지의 픽셀 크기 지정                         |

#### 기능 가용성 확인

##### Kotlin 예시

```kotlin
val params: Camera.Parameters? = camera?.parameters
val focusModes: List<String>? = params?.supportedFocusModes
if (focusModes?.contains(Camera.Parameters.FOCUS_MODE_AUTO) == true) {
    // 자동 초점 모드가 지원됩니다
}
```

##### Java 예시

```java
// 카메라 매개변수 가져오기
Camera.Parameters params = camera.getParameters();
List<String> focusModes = params.getSupportedFocusModes();
if (focusModes.contains(Camera.Parameters.FOCUS_MODE_AUTO)) {
    // 자동 초점 모드가 지원됩니다
}
```

#### 카메라 기능 사용

##### Kotlin 예시

```kotlin
val params: Camera.Parameters? = camera?.parameters
params?.focusMode = Camera.Parameters.FOCUS_MODE_AUTO
camera?.parameters = params
```

##### Java 예시

```java
// 카메라 매개변수 가져오기
Camera.Parameters params = camera.getParameters();
// 초점 모드 설정
params.setFocusMode(Camera.Parameters.FOCUS_MODE_AUTO);
// 카메라 매개변수 설정
camera.setParameters(params);
```

### 고급 기능

#### 측정 및 초점 영역

##### Kotlin 예시

```kotlin
// Camera 인스턴스 생성
camera = getCameraInstance()

// 카메라 매개변수 설정
val params: Camera.Parameters? = camera?.parameters
params?.apply {
    if (maxNumMeteringAreas > 0) { // 측정 영역이 지원되는지 확인
        meteringAreas = ArrayList<Camera.Area>().apply {
            val areaRect1 = Rect(-100, -100, 100, 100) // 이미지 중앙의 영역 지정
            add(Camera.Area(areaRect1, 600)) // 가중치를 60%로 설정
            val areaRect2 = Rect(800, -1000, 1000, -800) // 이미지 오른쪽 상단의 영역 지정
            add(Camera.Area(areaRect2, 400)) // 가중치를 40%로 설정
        }
    }
    camera?.parameters = this
}
```

#### 얼굴 인식

##### 리스너 구현

```kotlin
internal class MyFaceDetectionListener : Camera.FaceDetectionListener {
    override fun onFaceDetection(faces: Array<Camera.Face>, camera: Camera) {
        if (faces.isNotEmpty()) {
            Log.d("FaceDetection", ("face detected: ${faces.size}" + 
                " Face 1 Location X: ${faces[0].rect.centerX()}" +
                "Y: ${faces[0].rect.centerY()}"))
        }
    }
}
```

##### 얼굴 인식 시작

```kotlin
fun startFaceDetection() {
    // 얼굴 인식 시작 시도
    val params = mCamera?.parameters
    // *미리보기 시작 후에만* 얼굴 인식 시작
    params?.apply {
        if (maxNumDetectedFaces > 0) {
            // 카메라에서 얼굴 인식을 지원하므로 시작할 수 있습니다:
            mCamera?.startFaceDetection()
        }
    }
}
```

#### 타임랩스 동영상

##### Kotlin 예시

```kotlin
mediaRecorder.setProfile(CamcorderProfile.get(CamcorderProfile.QUALITY_TIME_LAPSE_HIGH))
mediaRecorder.setCaptureRate(0.1) // 10초마다 프레임 캡처
```

### 권한이 필요한 카메라 필드

Android 10(API 수준 29) 이상을 실행하는 앱은 `getCameraCharacteristics()` 메서드가 반환하는 다음 필드의 값에 액세스하려면 `CAMERA` 권한이 있어야 합니다:

* LENS_POSE_ROTATION
* LENS_POSE_TRANSLATION
* LENS_INTRINSIC_CALIBRATION
* LENS_RADIAL_DISTORTION
* LENS_POSE_REFERENCE
* LENS_DISTORTION
* LENS_INFO_HYPERFOCAL_DISTANCE
* LENS_INFO_MINIMUM_FOCUS_DISTANCE
* SENSOR_REFERENCE_ILLUMINANT1
* SENSOR_REFERENCE_ILLUMINANT2
* SENSOR_CALIBRATION_TRANSFORM1
* SENSOR_CALIBRATION_TRANSFORM2
* SENSOR_COLOR_TRANSFORM1
* SENSOR_COLOR_TRANSFORM2
* SENSOR_FORWARD_MATRIX1
* SENSOR_FORWARD_MATRIX2

---

## 추가 리소스 및 참고 문서

### 공식 샘플

1. **Camera2Basic 샘플** : [GitHub 링크](https://github.com/android/camera-samples/tree/main/Camera2Basic/#readme)
2. **공식 CameraX 샘플 앱** : [GitHub 링크](https://github.com/android/camera-samples/tree/main/CameraXBasic)
3. **Camera2Video 샘플** : [GitHub 링크](https://github.com/android/camera-samples/tree/main/Camera2Video#readme)
4. **HdrViewfinder 샘플** : [GitHub 링크](https://github.com/android/camera-samples/tree/main/HdrViewfinder/)

### 관련 가이드

* [카메라 라이브러리 선택](https://developer.android.com/training/camera/choose-camera-library?hl=ko)
* [간단한 사진 촬영](https://developer.android.com/training/camera/photobasics?hl=ko)
* [간단한 동영상 녹화](https://developer.android.com/training/camera/videobasics?hl=ko)
* [데이터 저장소](https://developer.android.com/guide/topics/data/data-storage?hl=ko)
* [위치 전략](https://developer.android.com/guide/topics/location/strategies?hl=ko)

### API 참조

* [Camera 클래스](https://developer.android.com/reference/android/hardware/Camera?hl=ko) (지원 중단됨)
* [Camera.Parameters](https://developer.android.com/reference/android/hardware/Camera.Parameters?hl=ko)
* [MediaRecorder](https://developer.android.com/reference/android/media/MediaRecorder?hl=ko)
* [SurfaceView](https://developer.android.com/reference/android/view/SurfaceView?hl=ko)
* [Intent](https://developer.android.com/reference/android/content/Intent?hl=ko)

---

 **작성일** : 2025년 9월 27일

 **근거** : Android Developers 공식 문서 네 개 문서를 기반으로 작성

 **출처** :

1. [카메라](https://developer.android.com/media/camera/camera-deprecated?hl=ko)
2. [사진 촬영](https://developer.android.com/media/camera/camera-deprecated/photobasics?hl=ko)
3. [카메라 제어하기](https://developer.android.com/media/camera/camera-deprecated/cameradirect?hl=ko)
4. [카메라 API](https://developer.android.com/media/camera/camera-deprecated/camera-api?hl=ko)

 **⚠️ 재차 강조** : 이 가이드의 모든 내용은 **지원 중단된** API에 관한 것입니다. 새로운 개발에는 **CameraX** 또는 **Camera2** 사용을 강력히 권장합니다.
