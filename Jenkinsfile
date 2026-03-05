// Unity Android 빌드 파이프라인 (빌드 + GitHub Release 배포)
pipeline {
    agent any

    parameters {
        string(name: 'VERSION_MAJOR', defaultValue: '0', description: '메이저 버전')
        string(name: 'VERSION_MINOR', defaultValue: '1', description: '마이너 버전')
        string(name: 'VERSION_PATCH', defaultValue: '18', description: '패치 버전')
        booleanParam(name: 'BUILD_AAB', defaultValue: false, description: 'APK 대신 AAB 빌드')
        booleanParam(name: 'RELEASE_GITHUB', defaultValue: true, description: 'GitHub Release 에 APK 업로드')
        booleanParam(name: 'RELEASE_PLAY', defaultValue: false, description: 'Google Play 내부 테스트 트랙에 업로드')
    }

    environment {
        // Jenkins Credentials 에 등록한 Secret Text ID
        KEYSTORE_PATH  = credentials('android-keystore-path')
        KEYSTORE_PASS  = credentials('android-keystore-pass')
        KEY_ALIAS      = credentials('android-key-alias')
        KEY_ALIAS_PASS = credentials('android-key-alias-pass')

        UNITY_PATH   = 'C:/Program Files/Unity/Hub/Editor/6000.0.66f1/Editor/Unity.exe'
        PROJECT_PATH = "${WORKSPACE}"
        OUTPUT_APK   = "${WORKSPACE}/build/thefirst.apk"
        OUTPUT_AAB   = "${WORKSPACE}/build/thefirst.aab"
        GH_REPO               = 'gsyan/thefirst_client_unity'
        VERSION_NAME          = "${params.VERSION_MAJOR}.${params.VERSION_MINOR}.${params.VERSION_PATCH}"
        GOOGLE_PLAY_JSON_KEY  = 'D:/BK/thefirst/thefirst_server/tools/google_relate/thefirst-fd116-93d5321f214a.json'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Build Android') {
            steps {
                script {
                    def outputPath = params.BUILD_AAB ? env.OUTPUT_AAB : env.OUTPUT_APK
                    def buildAABFlag = params.BUILD_AAB ? '-buildAAB' : ''

                    bat """
                        "${env.UNITY_PATH}" ^
                          -batchmode ^
                          -quit ^
                          -nographics ^
                          -projectPath "${env.PROJECT_PATH}" ^
                          -executeMethod BuildScript.BuildAndroid ^
                          -outputPath "${outputPath}" ^
                          -versionName "${env.VERSION_NAME}" ^
                          ${buildAABFlag} ^
                          -logFile "${env.WORKSPACE}/build/unity_build.log"
                    """
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
                expression { params.RELEASE_GITHUB == true }
            }
            steps {
                script {
                    def artifact = params.BUILD_AAB ? env.OUTPUT_AAB : env.OUTPUT_APK
                    def tag = "v${env.VERSION_NAME}"

                    bat """
                        gh release create ${tag} "${artifact}" ^
                          --title "${tag}" ^
                          --notes "Build #%BUILD_NUMBER%" ^
                          --repo ${env.GH_REPO} ^
                          || gh release upload ${tag} "${artifact}" ^
                               --repo ${env.GH_REPO} ^
                               --clobber
                    """
                }
            }
        }
    }

        stage('Google Play') {
            when {
                expression { params.RELEASE_PLAY == true }
            }
            steps {
                script {
                    def artifact = params.BUILD_AAB ? env.OUTPUT_AAB : env.OUTPUT_APK
                    bat """
                        set APK_PATH=${artifact}
                        set GOOGLE_PLAY_JSON_KEY=${env.GOOGLE_PLAY_JSON_KEY}
                        fastlane android internal
                    """
                }
            }
        }
    }

    post {
        failure {
            echo '빌드 실패. build/unity_build.log 확인'
        }
    }
}
