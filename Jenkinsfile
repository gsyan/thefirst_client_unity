// Unity Android 빌드 파이프라인 (단순 빌드 단계)
pipeline {
    agent any

    parameters {
        string(name: 'VERSION_NAME', defaultValue: '', description: '버전 이름 (예: 1.0.0). 비워두면 ProjectSettings 값 사용')
        booleanParam(name: 'BUILD_AAB', defaultValue: false, description: 'APK 대신 AAB 빌드')
    }

    environment {
        // Jenkins Credentials 에 등록한 Secret Text ID
        KEYSTORE_PATH = credentials('android-keystore-path')
        KEYSTORE_PASS = credentials('android-keystore-pass')
        KEY_ALIAS     = credentials('android-key-alias')
        KEY_ALIAS_PASS = credentials('android-key-alias-pass')

        UNITY_PATH    = 'C:/Program Files/Unity/Hub/Editor/6000.0.66f1/Editor/Unity.exe'
        PROJECT_PATH  = "${WORKSPACE}"
        OUTPUT_APK    = "${WORKSPACE}/build/thefirst.apk"
        OUTPUT_AAB    = "${WORKSPACE}/build/thefirst.aab"
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
                    def versionFlag = params.VERSION_NAME ? "-versionName ${params.VERSION_NAME}" : ''

                    bat """
                        "${env.UNITY_PATH}" ^
                          -batchmode ^
                          -quit ^
                          -nographics ^
                          -projectPath "${env.PROJECT_PATH}" ^
                          -executeMethod BuildScript.BuildAndroid ^
                          -outputPath "${outputPath}" ^
                          ${versionFlag} ^
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
                    echo "빌드 성공: ${params.BUILD_AAB ? OUTPUT_AAB : OUTPUT_APK}"
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
