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
    private readonly Dictionary<string, int> m_purchaseCounts = new Dictionary<string, int>(); // cardId별 이번 런에서 구매한 횟수 — 가격 단계 계산에 사용

    public void Reset()
    {
        m_buffs.Clear();
        m_purchaseCounts.Clear();
    }

    // 이번 런에서 이 cardId를 지금까지 구매한 횟수(0부터 시작) — 가격 계산(baseMineralCost + mineralCostStep * 구매횟수)에 사용
    public int GetPurchaseCount(string cardId)
    {
        int count;
        return m_purchaseCounts.TryGetValue(cardId, out count) ? count : 0;
    }

    public void IncrementPurchaseCount(string cardId)
    {
        m_purchaseCounts[cardId] = GetPurchaseCount(cardId) + 1;
    }

    // 이 카드의 다음 구매 비용 — baseMineralCost에 지금까지 구매 횟수만큼 mineralCostStep을 더함(구매할수록 비싸짐)
    public int GetCurrentCost(RewardCardData card)
    {
        return card.baseMineralCost + card.mineralCostStep * GetPurchaseCount(card.cardId);
    }

    // 즉시효과(isPersistent == false) 카드는 여기 누적하지 않음 — 선택 즉시 별도 처리(회복 등)로 소모됨
    // appliedValue를 생략하면 card.value1을 그대로 사용 — Buff_MineralFlatBonus처럼 구매 단계에 따라 실제 적용값이 달라지는 카드는
    // 호출부가 그 시점의 비용을 직접 계산해서 넘겨야 함(card.value1은 그런 카드의 기준값일 뿐, 실제 적용값이 아님)
    public void ApplyCard(RewardCardData card, float? appliedValue = null)
    {
        if (card == null) return;
        if (card.isPersistent == false) return;

        float value = appliedValue ?? card.value1;

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
        // Buff_MineralFlatBonus는 재구매할 때마다 더 비싼 단계로 "갈아타는" 개념이라 이전 값을 대체함(합산하면 50+70+90처럼 끝없이 불어남) — 나머지 %버프는 그대로 합산
        if (card.effectType == ECardEffectType.Buff_MineralFlatBonus)
            entry.valueSum = value;
        else
            entry.valueSum += value;
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

    public float GetMineralRateMultiplier() { return GetMultiplier(ECardEffectType.Buff_MineralRate); }

    // 서버가 돌려준 이번 런 구매 카드 이력(cardId 목록) 복원 — 지속버프는 재적용하고(즉시효과는 이미 소모되어 재적용하지 않음),
    // 구매 횟수는 지속/즉시 구분 없이 전부 누적(다음 가격 계산이 재접속 전과 동일하게 이어지도록)
    // (재접속 시 로그인 시점에 진행 중인 런의 카드를 미리 복원할 때 사용)
    public void ApplyPersistentCardIds(List<string> cardIds)
    {
        if (cardIds == null || cardIds.Count == 0) return;

        DataTableRewardCard table = DataManager.Instance.m_dataTableRewardCard;
        foreach (string cardId in cardIds)
        {
            RewardCardData card = table.GetCard(cardId);
            if (card == null) continue;

            // 이 구매가 몇 번째였는지(0부터) — GetCurrentCost와 동일한 기준으로, 증가 전 카운트를 그 시점의 "구매 단계"로 사용
            int purchaseIndexAtThatTime = GetPurchaseCount(cardId);
            if (card.isPersistent == true)
            {
                float? appliedValue = card.effectType == ECardEffectType.Buff_MineralFlatBonus
                    ? card.baseMineralCost + card.mineralCostStep * purchaseIndexAtThatTime
                    : (float?)null;
                ApplyCard(card, appliedValue);
            }
            IncrementPurchaseCount(cardId);
        }
    }
}
