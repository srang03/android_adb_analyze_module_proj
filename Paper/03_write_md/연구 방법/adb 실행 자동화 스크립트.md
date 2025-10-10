#### 1. 일반 카메라 실행

1. 백그라운드 앱 모두 종료

2. 앱 실행

- adb -s 192.168.219.119:45209 shell monkey -p com.sec.android.app.camera -c android.intent.category.LAUNCHER 1

3. 앱 종료 (뒤로 가기 클릭)

- adb -s 192.168.219.119:45209 shell input tap 850 2280

4. 백그라운드 앱 모두 종료


#### 2. 일반 카메라 촬영

- 백그라운드 앱 모두 종료
- 앱 실행

  - adb -s 192.168.219.119:45209 shell monkey -p com.sec.android.app.camera -c android.intent.category.LAUNCHER 1
- 촬영

  - 537 1940
- 앱 종료 (뒤로 가기 클릭)
- 백그라운드 앱 모두 종료


#### 3. 카카오톡 카메라 실행

- 백그라운드 앱 모두 종료
- 앱 실행

  - adb -s 192.168.219.119:45209 shell monkey -p com.kakao.talk -c android.intent.category.LAUNCHER 1
- 대화방 목록

  - 356 2148
- 대화방 입장

  - 83 2148
- 플러스 버튼

  - 60 2156
- 카메라 클릭

  - 425 1316
- 사진 촬영 클릭

  - 510 1140
- 뒤로 가기 클릭
- 앱 종료 (뒤로 가기 클릭)
- 백그라운드 앱 모두 종료


#### 4. 카카오톡 카메라 촬영

- 백그라운드 앱 모두 종료
- 앱 실행
  - adb -s 192.168.219.119:45209 shell monkey -p com.kakao.talk -c android.intent.category.LAUNCHER 1
- 대화방 목록
- 대화방 입장
- 플러스 버튼
- 카메라 클릭
- 사진 촬영 클릭
- 사진 촬영
  - 535 1930
- 확인
  - 772 2150
- 백그라운드 앱 모두 종료


#### 5. 텔레그램 카메라 실행

백그라운드 앱 모두 종료

앱 실행

adb -s 192.168.219.119:45209 shell monkey -p org.telegram.messenger -c android.intent.category.LAUNCHER 1

대화방 입장

506 349

클립 메뉴 버튼 클릭

870 2158

카메라 클릭

181 1160

뒤로 가기 클릭

뒤로 가기  클릭

앱 종료 (뒤로 가기 클릭)

백그라운드 앱 모두 종료


#### 6. 텔레그램 카메라 촬영

백그라운드 앱 모두 종료

앱 실행

adb -s 192.168.219.119:45209 shell monkey -p org.telegram.messenger -c android.intent.category.LAUNCHER 1

대화방 입장

클립 메뉴 버튼 클릭

카메라 클릭

사진 촬영

532 1945

앱 종료 (뒤로 가기 클릭)

백그라운드 앱 모두 종료


#### 7. 무음 카메라 실행

백그라운드 앱 모두 종료

앱 실행

adb -s 192.168.219.119:45209 shell monkey -p com.peace.SilentCamera -c android.intent.category.LAUNCHER 1

앱 종료 (뒤로 가기 클릭)

백그라운드 앱 모두 종료


#### 8. 무음 카메라 촬영

백그라운드 앱 모두 종료

앱실행

adb -s 192.168.219.119:45209 shell monkey -p com.peace.SilentCamera -c android.intent.category.LAUNCHER 1

촬영

550 1920

앱 종료 (뒤로 가기 클릭)

백그라운드 앱 모두 종료



- 백그라운드 앱 모두 종료
  - adb -s 192.168.219.119:45209 shell monkey -p com.sec.android.app.camera -c android.intent.category.LAUNCHER 1
  - adb -s 192.168.219.119:45209 shell monkey -p com.kakao.talk -c android.intent.category.LAUNCHER 1
  - adb -s 192.168.219.119:45209 shell monkey -p org.telegram.messenger -c android.intent.category.LAUNCHER 1
  - adb -s 192.168.219.119:45209 shell monkey -p com.peace.SilentCamera -c android.intent.category.LAUNCHER 1



- 백그라운드 앱 모두 종료
  - adb -s 192.168.219.119:45209 shell input tap 240 2292
  - adb -s 192.168.219.119:45209 shell input tap 510 1840
