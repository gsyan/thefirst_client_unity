// Unity Android 빌드 파이프라인 (빌드 + GitHub Release + Google Play + Firebase 배포)
pipeline {
    agent any

    parameters {
        string(name: 'VERSION_MAJOR', defaultValue: '0', description: '메이저 버전')
        string(name: 'VERSION_MINOR', defaultValue: '1', description: '마이너 버전')
        string(name: 'VERSION_PATCH', defaultValue: '18', description: '패치 버전')
        string(name: 'PRODUCT_NAME',  defaultValue: 'Space Fleet', description: '앱 표시 이름 (Google Play 등록명)')
        booleanParam(name: 'IS_SHIPPING',              defaultValue: false, description: 'true = AAB 빌드(release), false = APK 빌드(development)')
        booleanParam(name: 'BUILD',                    defaultValue: true,  description: '프로젝트 빌드 여부')
        booleanParam(name: 'GOOGLE_PLAY_INNER_TEST',   defaultValue: false, description: '독립적으로 Google Play 내부 테스트 트랙에 AAB 업로드 (워크스페이스의 build/thefirst.aab 사용)')
        booleanParam(name: 'GOOGLE_PLAY_CLOSED_TEST',  defaultValue: false, description: '독립적으로 Google Play 비공개 테스트 트랙으로 최신 내부 테스트 버전 승격 (재업로드 없음)')
        booleanParam(name: 'RELEASE_GITHUB',   defaultValue: false, description: 'GitHub Release 에 APK 업로드 (IS_SHIPPING=false 필요)')
        booleanParam(name: 'RELEASE_FIREBASE', defaultValue: false, description: 'Firebase App Distribution에 APK 업로드 (IS_SHIPPING=false 필요)')
        booleanParam(name: 'RELEASE_NAS',      defaultValue: true,  description: 'NAS(\\\\bk_server\\bk\\thefirst_client_build\\dev|release)에 APK 업로드 (IS_SHIPPING=false 필요)')
        string(name: 'LAST_VERSION_NAME', defaultValue: '', description: '[자동관리] 릴리즈노트 기준점이 되었던 마지막 버전. 직접 수정 금지')
        string(name: 'BASELINE_COMMIT',   defaultValue: '', description: '[자동관리] 릴리즈노트 기준 커밋 해시. 직접 수정 금지')
    }

    environment {
        KEYSTORE_PATH  = credentials('android-keystore-path')
        KEYSTORE_PASS  = credentials('android-keystore-pass')
        KEY_ALIAS      = credentials('android-key-alias')
        KEY_ALIAS_PASS = credentials('android-key-alias-pass')

        UNITY_PATH            = 'C:/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Unity.exe'
        PROJECT_PATH          = "${WORKSPACE}"
        OUTPUT_APK            = "${WORKSPACE}/build/thefirst.apk"
        OUTPUT_AAB            = "${WORKSPACE}/build/thefirst.aab"
        GH_REPO               = 'gsyan/thefirst_client_unity'
        VERSION_NAME          = "${params.VERSION_MAJOR}.${params.VERSION_MINOR}.${params.VERSION_PATCH}"
        FIREBASE_APP_ID       = '1:527468162306:android:fdd9d29003b29326e2261b'
        FIREBASE_CMD          = 'C:\\Users\\gsyan\\AppData\\Roaming\\npm\\firebase.cmd'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                script {
                    // 릴리즈 노트 생성: 버전이 바뀐 시점(BASELINE_COMMIT)부터 HEAD까지의 "R: " 커밋만 누적 정리
                    // 같은 버전으로 재빌드(테스트 실패 후 재시도 등)해도 기준점이 그대로라 계속 누적됨
                    def previousVersion = params.LAST_VERSION_NAME
                    def baselineCommit  = params.BASELINE_COMMIT?.trim()
                    def logRange        = baselineCommit ? "${baselineCommit}..HEAD" : "HEAD"

                    def rawLog = bat(script: "@git log ${logRange} --grep=\"^R: \" --pretty=format:\"%%s\"", returnStdout: true).trim()
                    def noteLines = rawLog ? rawLog.readLines().collect { it.replaceFirst('^R: ', '- ') } : []
                    def notesText = noteLines ? noteLines.join('\n') : '- 이번 버전에 사용자 대상 변경사항 없음'

                    bat 'if not exist build mkdir build'
                    writeFile file: 'build/release_notes.txt', text: "v${env.VERSION_NAME}\n\n${notesText}\n"
                    echo "[ReleaseNotes] 범위: ${logRange}\n${notesText}"

                    // 버전이 바뀐 첫 빌드일 때만 다음 기준점을 현재 HEAD로 갱신 (동일 버전 재빌드 시엔 유지)
                    env.NEW_BASELINE_COMMIT = previousVersion != env.VERSION_NAME
                        ? bat(script: '@git rev-parse HEAD', returnStdout: true).trim()
                        : baselineCommit

                    // 현재 파라미터 값으로 Job의 defaultValue 갱신 → 다음 빌드에 반영
                    properties([
                        parameters([
                            string(name: 'VERSION_MAJOR', defaultValue: "${params.VERSION_MAJOR}", description: '메이저 버전'),
                            string(name: 'VERSION_MINOR', defaultValue: "${params.VERSION_MINOR}", description: '마이너 버전'),
                            string(name: 'VERSION_PATCH', defaultValue: "${params.VERSION_PATCH}", description: '패치 버전'),
                            string(name: 'PRODUCT_NAME',  defaultValue: "${params.PRODUCT_NAME}",  description: '앱 표시 이름 (Google Play 등록명)'),
                            booleanParam(name: 'IS_SHIPPING',              defaultValue: false, description: 'true = AAB 빌드(release), false = APK 빌드(development)'),
                            booleanParam(name: 'BUILD',                    defaultValue: true,  description: '프로젝트 빌드 여부'),
                            booleanParam(name: 'GOOGLE_PLAY_INNER_TEST',   defaultValue: false, description: '독립적으로 Google Play 내부 테스트 트랙에 AAB 업로드 (워크스페이스의 build/thefirst.aab 사용)'),
                            booleanParam(name: 'GOOGLE_PLAY_CLOSED_TEST',  defaultValue: false, description: '독립적으로 Google Play 비공개 테스트 트랙으로 최신 내부 테스트 버전 승격 (재업로드 없음)'),
                            booleanParam(name: 'RELEASE_GITHUB',   defaultValue: false, description: 'GitHub Release 에 APK 업로드 (IS_SHIPPING=false 필요)'),
                            booleanParam(name: 'RELEASE_FIREBASE', defaultValue: false, description: 'Firebase App Distribution에 APK 업로드 (IS_SHIPPING=false 필요)'),
                            booleanParam(name: 'RELEASE_NAS',      defaultValue: true,  description: 'NAS(\\\\\\\\bk_server\\\\bk\\\\thefirst_client_build\\\\dev|release)에 APK 업로드 (IS_SHIPPING=false 필요)'),
                            string(name: 'LAST_VERSION_NAME', defaultValue: "${env.VERSION_NAME}", description: '[자동관리] 릴리즈노트 기준점이 되었던 마지막 버전. 직접 수정 금지'),
                            string(name: 'BASELINE_COMMIT',   defaultValue: "${env.NEW_BASELINE_COMMIT}", description: '[자동관리] 릴리즈노트 기준 커밋 해시. 직접 수정 금지'),
                        ])
                    ])
                }
            }
        }

        stage('Build') {
            // IS_SHIPPING=true → AAB(release), false → APK(development)
            when {
                expression { params.BUILD == true }
            }
            steps {
                script {
                    if (params.IS_SHIPPING == true) {
                        bat """
                            "${env.UNITY_PATH}" ^
                              -batchmode -quit -nographics ^
                              -projectPath "${env.PROJECT_PATH}" ^
                              -executeMethod BuildScript.BuildAndroid ^
                              -outputPath "${env.OUTPUT_AAB}" ^
                              -versionName "${env.VERSION_NAME}" ^
                              -productName "${params.PRODUCT_NAME}" ^
                              -buildAAB ^
                              -logFile "${env.WORKSPACE}/build/unity_build.log"
                        """
                    } else {
                        bat """
                            "${env.UNITY_PATH}" ^
                              -batchmode -quit -nographics ^
                              -projectPath "${env.PROJECT_PATH}" ^
                              -executeMethod BuildScript.BuildAndroid ^
                              -outputPath "${env.OUTPUT_APK}" ^
                              -versionName "${env.VERSION_NAME}" ^
                              -productName "${params.PRODUCT_NAME}" ^
                              -isDev ^
                              -logFile "${env.WORKSPACE}/build/unity_build.log"
                        """
                    }
                }
            }
            post {
                always {
                    archiveArtifacts artifacts: 'build/unity_build.log', allowEmptyArchive: true
                }
                success {
                    archiveArtifacts artifacts: 'build/*.apk,build/*.aab', allowEmptyArchive: true
                    echo "빌드 성공: v${env.VERSION_NAME}"
                }
            }
        }

        stage('GitHub Release') {
            when {
                expression { params.RELEASE_GITHUB == true && params.IS_SHIPPING == false }
            }
            steps {
                script {
                    def tag = "v${env.VERSION_NAME}"
                    bat """
                        gh release create ${tag} "${env.OUTPUT_APK}" ^
                          --title "${tag}" ^
                          --notes-file "${env.WORKSPACE}/build/release_notes.txt" ^
                          --repo ${env.GH_REPO} ^
                          || gh release upload ${tag} "${env.OUTPUT_APK}" ^
                               --repo ${env.GH_REPO} ^
                               --clobber
                    """
                }
            }
        }

        stage('Google Play') {
            when {
                expression { params.GOOGLE_PLAY_INNER_TEST == true }
            }
            steps {
                withCredentials([file(credentialsId: 'GOOGLE_PLAY_DEPLOY', variable: 'GOOGLE_PLAY_JSON_KEY')]) {
                    bat """
                        set APK_PATH=${env.OUTPUT_AAB}
                        set GOOGLE_PLAY_JSON_KEY=%GOOGLE_PLAY_JSON_KEY%
                        set VERSION_NAME=${env.VERSION_NAME}
                        C:\\Ruby34-x64\\bin\\fastlane android internal
                    """
                }
            }
        }

        stage('Google Play Closed Testing') {
            when {
                expression { params.GOOGLE_PLAY_CLOSED_TEST == true }
            }
            steps {
                withCredentials([file(credentialsId: 'GOOGLE_PLAY_DEPLOY', variable: 'GOOGLE_PLAY_JSON_KEY')]) {
                    bat """
                        set APK_PATH=${env.OUTPUT_AAB}
                        set GOOGLE_PLAY_JSON_KEY=%GOOGLE_PLAY_JSON_KEY%
                        set VERSION_NAME=${env.VERSION_NAME}
                        C:\\Ruby34-x64\\bin\\fastlane android closed_testing
                    """
                }
            }
        }

        stage('Firebase Distribution') {
            when {
                expression { params.RELEASE_FIREBASE == true && params.IS_SHIPPING == false }
            }
            steps {
                withCredentials([file(credentialsId: 'FIREBASE_SERVICE_ACCOUNT', variable: 'FIREBASE_JSON_KEY')]) {
                    bat """
                    set GOOGLE_APPLICATION_CREDENTIALS=%FIREBASE_JSON_KEY%
                    "${env.FIREBASE_CMD}" appdistribution:distribute "${env.OUTPUT_APK}" ^
                      --app ${env.FIREBASE_APP_ID} ^
                      --groups "fidforge,testers" ^
                      --release-notes-file "${env.WORKSPACE}/build/release_notes.txt"
                    """
                }
            }
        }

        stage('NAS Upload') {
            when {
                expression { params.RELEASE_NAS == true && params.IS_SHIPPING == false }
            }
            steps {
                withCredentials([usernamePassword(credentialsId: 'nas-smb-cred', usernameVariable: 'NAS_USER', passwordVariable: 'NAS_PASS')]) {
                    script {
                        def nasSubDir = params.IS_SHIPPING ? "release" : "dev"
                        def nasTarget = "\\\\bk_server\\bk\\thefirst_client_build\\${nasSubDir}\\v${env.VERSION_NAME}"
                        bat """
                            net use \\\\bk_server\\bk /user:%NAS_USER% %NAS_PASS% /persistent:no
                            if not exist "${nasTarget}" mkdir "${nasTarget}"
                            robocopy "${env.WORKSPACE}\\build" "${nasTarget}" thefirst.apk /r:3 /w:5
                            net use \\\\bk_server\\bk /delete
                        """
                    }
                }
            }
        }
    }

    post {
        failure {
            echo '빌드 실패. 각 스테이지 로그 확인'
        }
    }
}
