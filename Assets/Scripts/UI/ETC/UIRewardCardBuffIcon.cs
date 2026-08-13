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
        // valueSum은 카드에 적힌 % 그대로 합산된 값(0.11 = 11%) — 몇 장을 뽑았는지가 아니라 실제 적용되는 효과치를 그대로 보여줌. 단위(%)는 표시하지 않고 숫자만
        m_stackCountText.text = $"{entry.valueSum * 100f:F0}";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
