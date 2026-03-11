// 진형 선택 스크롤뷰 아이템 — 버튼 클릭 콜백 + Outline 선택 시각화
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewFormationItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_text;

    [Header("선택 외곽선")]
    [SerializeField] private Color m_colorSelected = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private float m_outlineWidth  = 4f;

    private UnityEngine.UI.Outline m_outline;

    public void InitializeScrollViewFormationItem(UnityEngine.Events.UnityAction actionSelect, string formationName)
    {
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        CommonUtility.SetUILocText(m_text, formationName);

        if (m_outline == null)
        {
            m_outline = m_selectButton.GetComponent<UnityEngine.UI.Outline>();
            if (m_outline == null)
                m_outline = m_selectButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
        }
        m_outline.effectColor    = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
    }
}
