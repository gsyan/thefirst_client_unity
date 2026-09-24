// 전투 중 게임 속도(timeScale) 순환 토글 제어 — 0.5x~3.0x, 전투 종료 시 Reset() 필수. 선택한 단계는 PlayerPrefs에 저장
using UnityEngine;

public static class GameSpeedController
{
    private const string k_prefKey = "BattleSpeedIndex";
    private const int k_defaultIndex = 1; // 기본 x1.0

    private static readonly float[] k_speedSteps = { 0.5f, 1.0f, 1.5f, 2.0f, 3.0f };
    private static readonly float[] k_pitchSteps = { 0.75f, 1.0f, 1.20f, 1.40f, 1.60f };

    private static int m_currentIndex = LoadSavedIndex();

    public static float CurrentSpeed => k_speedSteps[GetAppliedIndex()];
    public static float CurrentPitch => k_pitchSteps[GetAppliedIndex()];

    // 배속 변경은 VIP 전용 — 에디터에서는 VIP가 아니어도 허용
    public static bool IsSpeedChangeAllowed()
    {
        bool isAllowed = IAPManager.Instance.IsVipActive();
#if UNITY_EDITOR
        isAllowed = true;
#endif
        return isAllowed;
    }

    // 다음 속도 단계로 순환 (x0.5 → x1.0 → x1.5 → x2.0 → x3.0 → x0.5)
    public static void CycleNext()
    {
        m_currentIndex = (m_currentIndex + 1) % k_speedSteps.Length;
        PlayerPrefs.SetInt(k_prefKey, m_currentIndex);
        PlayerPrefs.Save();
        Apply();
    }

    // 전투 종료 시 호출 — timeScale만 1.0 복원, 인덱스는 유지해 다음 전투에서 재사용
    public static void Reset()
    {
        Time.timeScale = 1.0f;
        EventManager.Trigger_GameSpeedChanged(k_speedSteps[GetAppliedIndex()], 1.0f);
    }

    // 전투 시작 시 호출 — 이전에 설정한 배속 복원
    public static void RestoreSpeed()
    {
        Apply();
    }

    private static void Apply()
    {
        int appliedIndex = GetAppliedIndex();
        Time.timeScale = k_speedSteps[appliedIndex];
        EventManager.Trigger_GameSpeedChanged(k_speedSteps[appliedIndex], k_pitchSteps[appliedIndex]);
    }

    // 저장된 단계는 유지하되, 배속 변경 권한이 없으면 기본 배속을 적용
    private static int GetAppliedIndex()
    {
        if (IsSpeedChangeAllowed() == false)
            return k_defaultIndex;
        return m_currentIndex;
    }

    private static int LoadSavedIndex()
    {
        int savedIndex = PlayerPrefs.GetInt(k_prefKey, k_defaultIndex);
        bool isInRange = savedIndex >= 0 && savedIndex < k_speedSteps.Length;
        return isInRange == true ? savedIndex : k_defaultIndex;
    }
}
