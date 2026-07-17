// Jenkins CLI 빌드 진입점 - Android APK/AAB 생성
// 환경변수로 keystore 정보, 버전 코드 등을 주입받음
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class BuildScript
{
    // Jenkins 에서 호출: Unity.exe -executeMethod BuildScript.BuildAndroid ...
    public static void BuildAndroid()
    {
        string outputPath = GetArg("-outputPath") ?? "build/thefirst.apk";
        string keystorePath = GetEnv("KEYSTORE_PATH");
        string keystorePass = GetEnv("KEYSTORE_PASS");
        string keyaliasName = GetEnv("KEY_ALIAS");
        string keyaliasPass = GetEnv("KEY_ALIAS_PASS");
        string versionCode = GetEnv("BUILD_NUMBER"); // Jenkins BUILD_NUMBER
        string versionName = GetArg("-versionName");
        string productName = GetArg("-productName");

        // 버전 설정
        Debug.Log($"[Build] ARG -versionName = '{versionName ?? "(null)"}'");
        Debug.Log($"[Build] ARG -productName = '{productName ?? "(null)"}'");
        Debug.Log($"[Build] ENV BUILD_NUMBER = '{versionCode ?? "(null)"}'");
        Debug.Log($"[Build] PlayerSettings.bundleVersion (before) = '{PlayerSettings.bundleVersion}'");
        Debug.Log($"[Build] PlayerSettings.bundleVersionCode (before) = {PlayerSettings.Android.bundleVersionCode}");

        if (!string.IsNullOrEmpty(productName))
            PlayerSettings.productName = productName;

        if (!string.IsNullOrEmpty(versionName))
            PlayerSettings.bundleVersion = versionName;

        if (!string.IsNullOrEmpty(versionCode) && int.TryParse(versionCode, out int code))
            PlayerSettings.Android.bundleVersionCode = code;

        Debug.Log($"[Build] PlayerSettings.bundleVersion (after) = '{PlayerSettings.bundleVersion}'");
        Debug.Log($"[Build] PlayerSettings.bundleVersionCode (after) = {PlayerSettings.Android.bundleVersionCode}");

        // Unity-MCP(개발용 AI 에디터 툴) 패키지 리졸버가 플랫폼 전환 시 컴파일 에러를 막으려고
        // UNITY_MCP_READY를 Android 등 모든 빌드 타겟에 자동으로 걸어두는데, 이 상태로 실제 빌드하면
        // 에디터 전용 어셈블리가 플레이어 빌드에 딸려 들어가려다 컴파일 에러가 남 — 실제 빌드 직전에 여기서 확실히 제거
        StripUnityMcpReadyDefine(NamedBuildTarget.Android);

        // 메모리 변경을 디스크에 저장해야 Bee 증분 빌드가 새 값으로 재빌드함
        AssetDatabase.SaveAssets();
        Debug.Log("[Build] AssetDatabase.SaveAssets() 완료");

        // Keystore 설정 (환경변수가 없으면 빌드 중단)
        Debug.Log($"[Build] KEYSTORE_PATH={keystorePath ?? "(null)"}, KEY_ALIAS={keyaliasName ?? "(null)"}");
        if (!string.IsNullOrEmpty(keystorePath))
        {
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyaliasName;
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
            PlayerSettings.Android.useCustomKeystore = true;
            Debug.Log($"[Build] Keystore 적용 완료: {keystorePath}");
        }
        else
        {
            Debug.LogError("[Build] KEYSTORE_PATH 환경변수 없음 - 서명 불가, 빌드 중단");
            EditorApplication.Exit(1);
            return;
        }

        // APK vs AAB
        bool buildAAB = GetArg("-buildAAB") != null;
        EditorUserBuildSettings.buildAppBundle = buildAAB;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        // IS_SHIPPING=false(기본)이면 -isDev 인자가 전달됨 → DEVELOPMENT_BUILD 심볼 활성화
        bool isDev              = GetArg("-isDev") != null;
        bool autoConnectProfiler = GetArg("-autoConnectProfiler") != null;
        var buildOpts = isDev ? (BuildOptions.Development | BuildOptions.AllowDebugging) : BuildOptions.None;
        if (isDev == true && autoConnectProfiler == true)
            buildOpts |= BuildOptions.ConnectWithProfiler;

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = buildOpts,
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[Build] 성공: {outputPath}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[Build] 실패: {report.summary.result}");
            EditorApplication.Exit(1);
        }
    }

    // Unity-MCP 리졸버(RecompileGate)가 걸어둔 UNITY_MCP_READY define을 이 빌드 타겟에서만 제거 —
    // 개발용 AI 에디터 툴 코드가 실제 플레이어 빌드에 절대 포함되면 안 되므로 빌드 스크립트 자체에서 강제
    static void StripUnityMcpReadyDefine(NamedBuildTarget target)
    {
        const string readyDefine = "UNITY_MCP_READY";

        PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
        if (Array.IndexOf(defines, readyDefine) < 0)
        {
            Debug.Log($"[Build] {readyDefine} 없음 - 제거 불필요");
            return;
        }

        var filtered = new System.Collections.Generic.List<string>(defines);
        filtered.Remove(readyDefine);
        PlayerSettings.SetScriptingDefineSymbols(target, filtered.ToArray());
        Debug.Log($"[Build] {readyDefine} 제거 완료 (Unity-MCP는 개발용 에디터 툴 - 빌드에 포함 안 함)");
    }

    static string[] GetEnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }
        return scenes.ToArray();
    }

    static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return null;
    }

    static string GetEnv(string name)
    {
        string val = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(val) ? null : val;
    }
}
