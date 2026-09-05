// 이번 존 런에서 선택 확정한 보상카드 지속버프 누적 상태 — 존 런 시작 시 초기화, 탈출/포기 시 폐기(세션 스코프)
// 같은 effectType 카드를 여러 장 선택하면 %는 합산(1%+2%=3%, 곱연산 아님) — UI에 "몇 단"을 그대로 보여주기 쉬운 단순한 계산 방식
using System.Collections.Generic;

// effectType 하나에 대한 누적 상태 — 배지 UI(아이콘+단수)가 그대로 쓸 수 있는 표시 단위
public struct RewardCardBuffEntry
{
    public ECardEffectType effectType;
    public string iconName;
    public int stackCount;  // 이 effectType 카드를 선택한 횟수(1~5% 카드 몇 장을 뽑았는지와 무관하게 "장 수")
    public float valueSum;  // value1 합산 — 최종 배율은 1+valueSum
}

public class RewardCardSessionState
{
    private readonly Dictionary<ECardEffectType, RewardCardBuffEntry> m_buffs = new Dictionary<ECardEffectType, RewardCardBuffEntry>();

    public void Reset()
    {
        m_buffs.Clear();
    }

    // 즉시효과(isPersistent == false) 카드는 여기 누적하지 않음 — 선택 즉시 별도 처리(회복 등)로 소모됨
    public void ApplyCard(RewardCardData card)
    {
        if (card == null) return;
        if (card.isPersistent == false) return;

        RewardCardBuffEntry entry;
        bool found = m_buffs.TryGetValue(card.effectType, out entry);
        if (found == false)
        {
            entry = new RewardCardBuffEntry
            {
                effectType = card.effectType,
                iconName = card.iconName,
                stackCount = 0,
                valueSum = 0f,
            };
        }

        entry.stackCount += 1;
        entry.valueSum += card.value1;
        m_buffs[card.effectType] = entry;

        //UnityEngine.Debug.Log($"[RewardCard] ApplyCard effectType={card.effectType} stackCount={entry.stackCount} valueSum={entry.valueSum}");
    }

    public float GetMultiplier(ECardEffectType effectType)
    {
        RewardCardBuffEntry entry;
        bool found = m_buffs.TryGetValue(effectType, out entry);
        return found ? 1f + entry.valueSum : 1f;
    }

    // 현재 누적된 지속버프 전체 — UI(배지 나열)가 순회용으로 사용
    public IEnumerable<RewardCardBuffEntry> GetActiveBuffs()
    {
        return m_buffs.Values;
    }

    public float GetExplorationPointRateMultiplier() { return GetMultiplier(ECardEffectType.Buff_ExplorationPointRate); }

    // 서버가 돌려준 이번 런 선택 카드 이력(cardId 목록) 중 지속버프만 걸러 재적용 — 즉시효과 카드는 이미 소모되어 재적용하면 안 됨
    // (재접속 시 로그인 시점에 진행 중인 런의 카드를 미리 복원할 때 사용)
    public void ApplyPersistentCardIds(List<string> cardIds)
    {
        if (cardIds == null || cardIds.Count == 0) return;

        DataTableRewardCard table = DataManager.Instance.m_dataTableRewardCard;
        foreach (string cardId in cardIds)
        {
            RewardCardData card = table.GetCard(cardId);
            if (card != null && card.isPersistent == true)
                ApplyCard(card);
        }
    }
}
