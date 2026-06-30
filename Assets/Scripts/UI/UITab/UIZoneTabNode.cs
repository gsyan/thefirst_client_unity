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

    [SerializeField] private Color m_colorSelected = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color m_colorNormal   = Color.white;
    
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

    public void SetSelected(bool selected)
    {
        Color labelColor = selected ? m_colorSelected : m_colorNormal;
        if (m_label != null) m_label.color = labelColor;

        if (m_bgImage != null)
        {
            Color bg = labelColor;
            m_bgImage.color = bg;
        }
    }
}
