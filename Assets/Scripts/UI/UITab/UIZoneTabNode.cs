using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 존 탭 스크롤 내 노드 1개 (Z1, Z2 ...) — InfiniteScrollViewH의 onItemBind로 바인딩
public class UIZoneTabNode : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private TextMeshProUGUI m_label;
    [SerializeField] private Image m_bgImage;
    [SerializeField] private Image m_selectedImage; // 현재 보고 있는 존임을 표시 — 색 변경이 아니라 오브젝트 자체를 켜고 끔

    private int m_groupIndex;

    public void SetData(int groupIndex, Action<int> onClicked)
    {
        m_groupIndex = groupIndex;
        if (m_label != null) m_label.text = $"Z{groupIndex + 1}";

        if (m_button != null)
        {
            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(() =>
            {
                SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
                onClicked?.Invoke(m_groupIndex);
            });
        }
    }

    // cleared(클리어 완료) > locked(진입 불가) > 그 외(진행 중) 순으로 색 결정. selected(현재 보고 있는 존)는
    // 색이 아니라 m_selectedImage 오브젝트를 켜고 끔으로 별도 표시
    public void SetState(bool selected, bool isCleared, bool isLocked)
    {
        string colorKey = "General";
        if (isCleared == true)
            colorKey = "Unlocked";
        else if (isLocked == true)
            colorKey = "Zone.Locked";

        Color color = CommonUtility.PaletteColor(colorKey);
        if (m_label != null)   m_label.color   = color;
        if (m_bgImage != null) m_bgImage.color = color;

        if (m_selectedImage != null)
            m_selectedImage.gameObject.SetActive(selected);
    }
}
