// Unity Android 빌드 파이프라인 (빌드 + GitHub Release + Google Play + Firebase 배포)
pipeline {
    agent any

    parameters {
        string(name: 'VERSION_MAJOR', defaultValue: '0', description: '메이저 버전')
        string(name: 'VERSION_MINOR', defaultValue: '1', description: '마이너 버전')
        string(name: 'VERSION_PATCH', defaultValue: '18', description: '패치 버전')
        booleanParam(name: 'IS_SHIPPING',          defaultValue: false, description: '배포용 빌드 (체크 시 개발자 도구/콘솔 비활성화, 미체크 = 개발 빌드)')
        booleanParam(name: 'RELEASE_PLAY',         defaultValue: false, description: 'Google Play 내부 테스트 트랙에 AAB 업로드')
        booleanParam(name: 'RELEASE_GITHUB',   defaultValue: false, description: 'GitHub Release 에 APK 업로드')
        booleanParam(name: 'RELEASE_FIREBASE', defaultValue: false, description: 'Firebase App Distribution에 APK 업로드')
        booleanParam(name: 'RELEASE_NAS',      defaultValue: true,  description: 'NAS(\\\\bk_server\\bk\\thefirst_client_build\\dev|release)에 APK 업로드')
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
        GOOGLE_PLAY_JSON_KEY  = 'D:/BK/thefirst/thefirst_server/tools/google_relate/thefirst-fd116-93d5321f214a.json'
        FIREBASE_APP_ID       = '1:527468162306:android:fdd9d29003b29326e2261b'
        FIREBASE_JSON_KEY     = 'D:/BK/thefirst/thefirst_server/tools/google_relate/firebase-service-account.json'
        FIREBASE_CMD          = 'C:\\Users\\gsyan\\AppData\\Roaming\\npm\\firebase.cmd'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                script {
                    // 현재 파라미터 값으로 Job의 defaultValue 갱신 → 다음 빌드에 반영
                    properties([
                        parameters([
                            string(name: 'VERSION_MAJOR', defaultValue: "${params.VERSION_MAJOR}", description: '메이저 버전'),
                            string(name: 'VERSION_MINOR', defaultValue: "${params.VERSION_MINOR}", description: '마이너 버전'),
                            string(name: 'VERSION_PATCH', defaultValue: "${params.VERSION_PATCH}", description: '패치 버전'),
                            booleanParam(name: 'IS_SHIPPING',          defaultValue: false, description: '배포용 빌드 (체크 시 개발자 도구/콘솔 비활성화, 미체크 = 개발 빌드)'),
                            booleanParam(name: 'RELEASE_PLAY',         defaultValue: false, description: 'Google Play 내부 테스트 트랙에 AAB 업로드'),
                            booleanParam(name: 'RELEASE_GITHUB',   defaultValue: false, description: 'GitHub Release 에 APK 업로드'),
                            booleanParam(name: 'RELEASE_FIREBASE', defaultValue: false, description: 'Firebase App Distribution에 APK 업로드'),
                            booleanParam(name: 'RELEASE_NAS',      defaultValue: true,  description: 'NAS(\\\\\\\\bk_server\\\\bk\\\\thefirst_client_build\\\\dev|release)에 APK 업로드'),
                        ])
                    ])
                }
            }
        }

        stage('Build APK') {
            // RELEASE_PLAY 만 단독 체크된 경우가 아니면 APK 빌드
            when {
                expression { params.RELEASE_PLAY == false || params.RELEASE_GITHUB == true || params.RELEASE_FIREBASE == true }
            }
            steps {
                script {
                    def devFlag = params.IS_SHIPPING ? "" : "-isDev"
                    bat """
                        "${env.UNITY_PATH}" ^
                          -batchmode -quit -nographics ^
                          -projectPath "${env.PROJECT_PATH}" ^
                          -executeMethod BuildScript.BuildAndroid ^
                          -outputPath "${env.OUTPUT_APK}" ^
                          -versionName "${env.VERSION_NAME}" ^
                          ${devFlag} ^
                          -logFile "${env.WORKSPACE}/build/unity_build_apk.log"
                    """
                }
            }
            post {
                always {
                    archiveArtifacts artifacts: 'build/unity_build_apk.log', allowEmptyArchive: true
                }
                success {
                    archiveArtifacts artifacts: 'build/*.apk', allowEmptyArchive: true
                }
            }
        }

        stage('Build AAB') {
            when {
                expression { params.RELEASE_PLAY == true }
            }
            steps {
                script {
                    def devFlag = params.IS_SHIPPING ? "" : "-isDev"
                    bat """
                        "${env.UNITY_PATH}" ^
                          -batchmode -quit -nographics ^
                          -projectPath "${env.PROJECT_PATH}" ^
                          -executeMethod BuildScript.BuildAndroid ^
                          -outputPath "${env.OUTPUT_AAB}" ^
                          -versionName "${env.VERSION_NAME}" ^
                          -buildAAB ^
                          ${devFlag} ^
                          -logFile "${env.WORKSPACE}/build/unity_build_aab.log"
                    """
                }
            }
            post {
                always {
                    archiveArtifacts artifacts: 'build/unity_build_aab.log', allowEmptyArchive: true
                }
                success {
                    archiveArtifacts artifacts: 'build/*.aab', allowEmptyArchive: true
                    echo "빌드 성공: v${env.VERSION_NAME}"
                }
            }
        }

        stage('GitHub Release') {
            when {
                expression { params.RELEASE_GITHUB == true }
            }
            steps {
                script {
                    def tag = "v${env.VERSION_NAME}"
                    bat """
                        gh release create ${tag} "${env.OUTPUT_APK}" ^
                          --title "${tag}" ^
                          --notes "Build #%BUILD_NUMBER%" ^
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
                expression { params.RELEASE_PLAY == true }
            }
            steps {
                bat """
                    set APK_PATH=${env.OUTPUT_AAB}
                    set GOOGLE_PLAY_JSON_KEY=${env.GOOGLE_PLAY_JSON_KEY}
                    C:\\Ruby34-x64\\bin\\fastlane android internal
                """
            }
        }

        stage('Firebase Distribution') {
            when {
                expression { params.RELEASE_FIREBASE == true }
            }
            steps {
                bat """
                    set GOOGLE_APPLICATION_CREDENTIALS=${env.FIREBASE_JSON_KEY}
                    "${env.FIREBASE_CMD}" appdistribution:distribute "${env.OUTPUT_APK}" ^
                      --app ${env.FIREBASE_APP_ID} ^
                      --groups "fidforge,testers" ^
                      --release-notes "v${env.VERSION_NAME} Build #%BUILD_NUMBER%"
                """
            }
        }

        stage('NAS Upload') {
            when {
                expression { params.RELEASE_NAS == true && params.RELEASE_PLAY == false }
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
