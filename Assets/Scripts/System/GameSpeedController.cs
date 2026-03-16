// 전투 중 게임 속도(timeScale) 순환 토글 제어 — 0.5x~3.0x, 전투 종료 시 Reset() 필수
using UnityEngine;

public static class GameSpeedController
{
    private static readonly float[] k_speedSteps = { 0.5f, 1.0f, 1.5f, 2.0f, 3.0f };
    private static readonly float[] k_pitchSteps = { 0.75f, 1.0f, 1.20f, 1.40f, 1.60f };

    private static int m_currentIndex = 1; // 기본 x1.0

    public static float CurrentSpeed => k_speedSteps[m_currentIndex];
    public static float CurrentPitch => k_pitchSteps[m_currentIndex];

    // 다음 속도 단계로 순환 (x0.5 → x1.0 → x1.5 → x2.0 → x3.0 → x0.5)
    public static void CycleNext()
    {
        m_currentIndex = (m_currentIndex + 1) % k_speedSteps.Length;
        Apply();
    }

    // 전투 종료 시 호출 — timeScale만 1.0 복원, 인덱스는 유지해 다음 전투에서 재사용
    public static void Reset()
    {
        Time.timeScale = 1.0f;
        EventManager.Trigger_GameSpeedChanged(k_speedSteps[m_currentIndex], 1.0f);
    }

    // 전투 시작 시 호출 — 이전에 설정한 배속 복원
    public static void RestoreSpeed()
    {
        Apply();
    }

    private static void Apply()
    {
        Time.timeScale = k_speedSteps[m_currentIndex];
        EventManager.Trigger_GameSpeedChanged(k_speedSteps[m_currentIndex], k_pitchSteps[m_currentIndex]);
    }
}
