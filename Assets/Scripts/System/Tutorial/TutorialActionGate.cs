// 튜토리얼 중 서버 왕복 없이 로컬로만 시뮬레이션해야 하는 액션(함선 추가, 모듈 해금, 레벨업/등급업 등)의
// 판단(어느 튜토리얼인지)과 로컬 자원 차감을 한 곳에 모아둔 게이트
public static class TutorialActionGate
{
    // 튜토리얼 진행 중 여부
    public static bool IsActive()
    {
        return TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying;
    }

    // 특정 튜토리얼(tutorialId) 진행 중인지 확인
    public static bool IsTutorial(string tutorialId)
    {
        if (IsActive() == false) return false;
        return TutorialManager.Instance.GetCurrentTutorialId() == tutorialId;
    }

    // 로컬 모듈포인트 차감 (서버 호출 없음) — 튜토리얼 중 지급된 임시 값에서만 차감됨
    public static bool TryConsumeModulePoint(int cost)
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return false;
        if (commander.GetModulePoint() < cost) return false;

        commander.UpdateModulePoint(commander.GetModulePoint() - cost);
        return true;
    }
}
