// 셀 클리어 보상카드 1종의 데이터 — DataTableRewardCard의 리스트 원소
// value1/value2의 의미는 effectType마다 다름(ECardEffectType 주석 참고)
[System.Serializable]
public class RewardCardData
{
    public string cardId;
    public string nameKey;      // 로컬라이즈 키
    public string descKey;      // 로컬라이즈 키(설명 포맷 문자열, {0}/{1}로 value1/value2 대입)
    public string iconName;     // UISpriteCache.Get()에 넘길 스프라이트 이름(UIAtlas 소속, 확장자 없음)
    public int rarity;          // 등급(색상 표시/밸런스 그룹핑용, 수치 자체는 value1/value2가 결정)
    public ECardEffectType effectType;
    public bool isPersistent;   // true=지속버프(세션 유지), false=즉시효과(선택 즉시 소모)
    public float value1;
    public float value2;
    public int weight;          // 서버 후보 추첨 가중치(클수록 잘 뽑힘)
}
