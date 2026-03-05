// Unity Android 빌드 파이프라인 (빌드 + GitHub Release 배포)
pipeline {
    agent any

    parameters {
        string(name: 'VERSION_NAME', defaultValue: '', description: '버전 이름 (예: 1.0.0). 비워두면 ProjectSettings 값 사용')
        booleanParam(name: 'BUILD_AAB', defaultValue: false, description: 'APK 대신 AAB 빌드')
        booleanParam(name: 'RELEASE_GITHUB', defaultValue: true, description: 'GitHub Release 에 APK 업로드')
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
        GH_REPO       = 'gsyan/thefirst_client_unity'
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

        stage('GitHub Release') {
            when {
                expression { params.RELEASE_GITHUB == true }
            }
            steps {
                script {
                    def version = params.VERSION_NAME ?: bat(returnStdout: true, script: '@powershell -Command "(Select-String -Path \\"ProjectSettings/ProjectSettings.asset\\" -Pattern \\"bundleVersion:\\s*(.+)\\").Matches[0].Groups[1].Value.Trim()"').trim()
                    def artifact = params.BUILD_AAB ? env.OUTPUT_AAB : env.OUTPUT_APK
                    def tag = "v${version}"

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

    post {
        failure {
            echo '빌드 실패. build/unity_build.log 확인'
        }
    }
}
