// 확보한 보상카드 지속버프 1종을 나타내는 배지 — 아이콘 + 우하단 누적 효과치(%) 숫자
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRewardCardBuffIcon : MonoBehaviour
{
    [SerializeField] private Image m_iconImage;
    [SerializeField] private TMP_Text m_stackCountText;

    public void SetBuff(RewardCardBuffEntry entry)
    {
        gameObject.SetActive(true);
        m_iconImage.sprite = UISpriteCache.Get(entry.iconName);
        // valueSum은 대부분 카드에 적힌 % 그대로 합산된 값(0.11 = 11%)이라 100을 곱해 보여줌 — 몇 장을 뽑았는지가 아니라 실제 적용되는 효과치를 그대로 보여줌.
        // Buff_MineralFlatBonus만 예외: %가 아니라 "앞으로 클리어마다 가산할 광물량" 그대로의 값이라 100을 곱하면 안 됨
        bool isFlatAmount = entry.effectType == ECardEffectType.Buff_MineralFlatBonus;
        m_stackCountText.text = isFlatAmount ? $"{entry.valueSum:F0}" : $"{entry.valueSum * 100f:F0}";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
