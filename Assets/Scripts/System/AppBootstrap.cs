using UnityEngine;

// 안드로이드는 Application.targetFrameRate 미지정(-1) 시 기기가 자체 기본값(화면 주사율의 절반 등)으로
// 프레임을 캡하는 경우가 흔함 — 60으로 명시 지정해 그 기본 캡이 원인인지 확인하기 위한 실험적 설정
public static class AppBootstrap
{
    private const int k_targetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetTargetFrameRate()
    {
        Application.targetFrameRate = k_targetFrameRate;
    }
}
