// 함선 개별 모듈 슬롯 UI 버튼 컴포넌트
// ModuleBase와 1:1 매칭되며, 선택 여부를 Outline / 잠금 여부를 배경색으로 시각화
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModuleSelector : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_backgroundImage;
    [SerializeField] private TMP_Text m_typeText;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorNotExist = new Color(0.2f, 0.2f, 0.2f, 1f);  // 현재 함체에는 없음
    [SerializeField] private Color m_colorLocked   = new Color(0.8f, 0.2f, 0.2f, 1f);  // placeholder
    [SerializeField] private Color m_colorUnlocked = new Color(0.2f, 0.8f, 0.4f, 1f);  // 일반 모듈
    [SerializeField] private Color m_colorSelected = new Color(1f,   0.8f, 0.2f, 1f);  // 선택 테두리
    [SerializeField] private float m_outlineWidth  = 4f;

    private UnityEngine.UI.Outline m_outline;

    public ModuleBase Module { get; private set; }

    public void Initialize(ModuleBase module, UnityEngine.Events.UnityAction onClick)
    {
        Module = module;

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);
        m_button.interactable = true;

        if (m_backgroundImage == null)
            m_backgroundImage = m_button.GetComponent<Image>();

        // 슬롯 번호 표시 (행 레이블이 타입 아이콘을 담당)
        if (m_typeText != null)
            m_typeText.text = (module.GetModuleSlotIndex() + 1).ToString();



        // 잠금 여부에 따라 배경색 설정
        if (m_backgroundImage != null)
            m_backgroundImage.color = (module is ModulePlaceholder) ? m_colorLocked : m_colorUnlocked;

        m_outline = m_button.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = m_button.gameObject.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled = false;
    }

    // 현재 함체에 해당 슬롯이 없는 경우: 시각적 유지, 기능 비활성화
    public void SetNotExist()
    {
        Module = null;
        m_button.onClick.RemoveAllListeners();
        m_button.interactable = false;

        if (m_backgroundImage != null)
            m_backgroundImage.color = m_colorNotExist;

        if (m_typeText != null)
            m_typeText.text = "";

        if (m_outline != null)
            m_outline.enabled = false;
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
    }
}
