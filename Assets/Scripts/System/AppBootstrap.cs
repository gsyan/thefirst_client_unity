using UnityEngine;

// Application.targetFrameRate 미지정(-1) 시 기기가 자체 기본값(화면 주사율의 절반 등)으로 30 근처에 캡을 걸고,
// 명시적으로 지정하면 60까지 오르는 것을 실기기로 확인함 — 발열 절감을 위해 30으로 명시 고정
public static class AppBootstrap
{
    private const int k_targetFrameRate = 30;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetTargetFrameRate()
    {
        Application.targetFrameRate = k_targetFrameRate;
    }
}
