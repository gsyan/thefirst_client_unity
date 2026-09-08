// 업적 항목 1개 — DataTableAchievement가 리스트로 보유. 완료 판정/현재값은 서버가 계산(AchievementService), 여기는 정적 정의만
[System.Serializable]
public class AchievementData
{
    public string achievementId;                  // 고유 키
    public EAchievementConditionType conditionType;
    public string conditionParam;                  // 타입별 파라미터(존번호/이벤트종류/티어 등) — EAchievementConditionType 주석 참고
    public int threshold;                          // 이 값 이상이면 완료
    public int achievementPointReward;             // 수령 시 지급되는 업적포인트
    public string nameKey;                         // 로컬라이즈 키
    public string descKey;                         // 로컬라이즈 키
}
