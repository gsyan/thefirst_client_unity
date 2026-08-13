// 확보한 보상카드 지속버프 전체를 아이콘 배지로 가로 나열 — 줄바꿈은 m_container의 GridLayoutGroup(Constraint Count)이 알아서 처리하므로 여기선 신경쓰지 않음
using System.Collections.Generic;
using UnityEngine;

public class UIRewardCardBuffDisplay : MonoBehaviour
{
    [SerializeField] private UIRewardCardBuffIcon m_iconPrefab;
    [SerializeField] private Transform m_container; // GridLayoutGroup 부착 — 가로 나열 후 지정한 열 수를 넘으면 자동 줄바꿈

    private readonly List<UIRewardCardBuffIcon> m_iconPool = new();

    public void Refresh(RewardCardSessionState sessionState)
    {
        int usedCount = 0;
        if (sessionState != null)
        {
            foreach (RewardCardBuffEntry entry in sessionState.GetActiveBuffs())
            {
                UIRewardCardBuffIcon icon = GetOrCreateIcon(usedCount);
                icon.SetBuff(entry);
                usedCount++;
            }
        }

        for (int i = usedCount; i < m_iconPool.Count; i++)
            m_iconPool[i].Hide();
    }

    private UIRewardCardBuffIcon GetOrCreateIcon(int index)
    {
        if (index < m_iconPool.Count)
            return m_iconPool[index];

        UIRewardCardBuffIcon icon = Instantiate(m_iconPrefab, m_container);
        m_iconPool.Add(icon);
        return icon;
    }
}
