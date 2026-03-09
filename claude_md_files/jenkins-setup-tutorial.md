# Unity Android Jenkins CI/CD 구성 튜토리얼

## 전제 조건
- Jenkins 서버 설치 완료
- Unity Editor 설치 완료
- Git 설치 완료
- GitHub 계정 및 gh CLI 설치 완료
- Android keystore 파일 보유

---

## 1단계: Unity 빌드 스크립트

`Assets/Scripts/Editor/BuildScript.cs` 생성 (이미 repo에 있음)

---

## 2단계: Jenkins 서비스 계정 설정

Jenkins가 SYSTEM 계정으로 실행되면 Unity 라이선스를 인식 못함.
반드시 실제 사용자 계정으로 변경 필요.

1. `services.msc` 실행
2. Jenkins 더블클릭 > **로그온** 탭
3. `이 계정` 선택 > 빌드 PC 사용자 계정 입력 (예: `.\gsyan`)
4. Windows 로그인 비밀번호 입력
5. 서비스 재시작

---

## 3단계: Unity 캐시 디렉토리 생성

PowerShell **관리자**로 실행:
```powershell
New-Item -ItemType Directory -Force -Path "C:\WINDOWS\system32\config\systemprofile\AppData\Local\Unity\Caches"
```

---

## 4단계: Jenkins Credentials 등록

Jenkins > 관리 > Credentials > System > Global > Add Credentials

| ID | Kind | 값 |
|---|---|---|
| `android-keystore-path` | Secret text | keystore 절대경로 |
| `android-keystore-pass` | Secret text | keystore 비밀번호 |
| `android-key-alias` | Secret text | key alias 이름 |
| `android-key-alias-pass` | Secret text | key alias 비밀번호 |
| `GIT_SSH_KEY` | SSH Username with private key | GitHub SSH 키 |

---

## 5단계: gh CLI 인증 확인

빌드 계정으로 로그인된 상태여야 함:
```powershell
gh auth status
```
로그인 안 되어 있으면: `gh auth login`

---

## 6단계: Ruby + fastlane 설치

1. https://rubyinstaller.org/downloads 에서 **Ruby+Devkit 3.4.x (x64)** 설치
2. 설치 완료 후 PowerShell에서:
```powershell
gem install fastlane
```

---

## 7단계: Google Play 서비스 계정 설정

### 7-1. Google Cloud Console에서 서비스 계정 생성
1. https://console.cloud.google.com 접속
2. 프로젝트 선택
3. IAM 및 관리자 > 서비스 계정 > 서비스 계정 만들기
4. 이름: `jenkins-play-deploy`
5. 완료 후 **키** 탭 > **새 키 만들기** > JSON 다운로드

### 7-2. Google Play Android Developer API 활성화
1. Cloud Console > API 라이브러리 검색: `Google Play Android Developer`
2. **사용** 클릭

### 7-3. Play Console에서 권한 부여
1. https://play.google.com/console 접속
2. **사용자 및 권한** > **신규 사용자 초대**
3. 서비스 계정 이메일 입력 (예: `jenkins-play-deploy@프로젝트.iam.gserviceaccount.com`)
4. **앱** 탭 > **앱 추가** > 앱 선택
5. **앱을 테스트 트랙으로 출시** 체크 > 저장

> ⚠️ 서비스 계정은 초대 수락 불필요, 자동 활성화

---

## 8단계: Jenkinsfile 설정

`Jenkinsfile` 내 환경변수 수정:
```groovy
UNITY_PATH           = 'C:/Program Files/Unity/Hub/Editor/버전/Editor/Unity.exe'
GH_REPO              = 'GitHub계정/repo이름'
GOOGLE_PLAY_JSON_KEY = 'JSON키파일 절대경로'
```

fastlane 경로 확인:
```powershell
where.exe fastlane
# 예: C:\Ruby34-x64\bin\fastlane
```
다른 경로라면 Jenkinsfile의 `C:\\Ruby34-x64\\bin\\fastlane` 수정

---

## 9단계: Jenkins Pipeline Job 생성

1. Jenkins > 새로운 Item > **Pipeline**
2. Pipeline Definition: **Pipeline script from SCM**
3. SCM: Git
4. Repository URL: `git@github.com:계정/repo.git`
5. Credentials: GIT_SSH_KEY
6. Branch: `*/main`
7. Script Path: `Jenkinsfile`
8. 저장

---

## 10단계: 첫 빌드 실행

1. **지금 빌드** 클릭 (첫 실행 - 파라미터 인식용)
2. 이후 **파라미터와 함께 빌드** 로 실행

### 파라미터 설명
| 파라미터 | 설명 |
|---|---|
| VERSION_MAJOR / MINOR / PATCH | 버전 (마지막 입력값 자동 기억) |
| BUILD_AAB | 체크 = AAB 빌드 (Google Play용), 미체크 = APK |
| RELEASE_GITHUB | GitHub Release에 APK 업로드 |
| RELEASE_PLAY | Google Play 내부 테스트 트랙에 AAB 업로드 |

> ⚠️ Google Play 배포 시 BUILD_AAB 반드시 체크

---

## 추가 설정 (Firebase App Distribution)

1. Node.js 설치 후 시스템 환경변수 PATH에 `C:\Program Files\nodejs` 추가
2. Jenkins 서비스 재시작
3. `npm install -g firebase-tools`
4. Google Cloud Console > IAM > `firebase-adminsdk-...` 서비스 계정에 `Firebase App Distribution Admin` 역할 추가

## 주의사항

- **프로덕션 트랙** 추가 시 `fastlane/Fastfile`의 `release_status`를 `"draft"`로 변경할 것
- Jenkinsfile 파라미터 구조 변경 시 Jenkins가 defaultValue로 리셋됨 (한 번 입력 후 빌드하면 이후 기억)
- Mac Mini로 이전 시 변경 사항:
  - `UNITY_PATH` → macOS Unity 경로
  - `FIREBASE_CMD` → macOS firebase 경로
  - `GOOGLE_PLAY_JSON_KEY`, `FIREBASE_JSON_KEY` → Mac 경로
  - Jenkinsfile `bat` → `sh` 변경
  - keystore 경로 Credential 값 → Mac 경로로 변경
