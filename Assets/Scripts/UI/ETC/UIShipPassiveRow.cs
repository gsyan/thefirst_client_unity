// 함체 선택 팝업(UIHullPickerView) 패시브 탭 1행 — 단계 번호 + 설명, 해금된 단계만 강조 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIShipPassiveRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_indexText;
    [SerializeField] private TMP_Text m_descriptionText;
    [SerializeField] private GameObject m_unlockImage; // 풀스트레치 회색 오버레이 — 잠긴 단계를 덮어 어둡게 표시(언락되면 비활성화해서 걷어냄)

    public void SetPassiveRow(int stage, string description, bool isUnlocked)
    {
        gameObject.SetActive(true);
        if (m_indexText != null) m_indexText.text = $"{stage}";
        if (m_descriptionText != null) m_descriptionText.text = description;
        if (m_unlockImage != null) m_unlockImage.SetActive(isUnlocked == false);

        RebuildSelf();
    }

    private void RebuildSelf()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
