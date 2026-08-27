// 진행 중인 탐험 런에서 적립(미확정)된 보상 저장소 — 탈출/포기 확정 전까지 종류별 수량을 누적 추적
// 향후 보상 종류가 늘어나도(미네랄 등) 이 구조를 고치지 않도록 ECostType과 동일하게 enum 키 + Dictionary 방식을 사용
using System.Collections.Generic;

public enum EBankedRewardType
{
    ExplorationPoint,
    Exp,
}

public class BankedRunReward
{
    private readonly Dictionary<EBankedRewardType, int> m_amounts = new();

    public int Get(EBankedRewardType type)
    {
        int amount;
        bool found = m_amounts.TryGetValue(type, out amount);
        return found == true ? amount : 0;
    }

    public void Add(EBankedRewardType type, int amount)
    {
        if (amount == 0) return;
        m_amounts[type] = Get(type) + amount;
    }

    public void Set(EBankedRewardType type, int amount)
    {
        m_amounts[type] = amount;
    }

    public void Clear()
    {
        m_amounts.Clear();
    }

    // 스냅샷 복사(존 브라우징 중 이탈/복귀 등) — 값만 그대로 옮겨 담음
    public void CopyFrom(BankedRunReward other)
    {
        m_amounts.Clear();
        if (other == null) return;
        foreach (KeyValuePair<EBankedRewardType, int> pair in other.m_amounts)
            m_amounts[pair.Key] = pair.Value;
    }
}
