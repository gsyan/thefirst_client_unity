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

        if (m_backgroundImage == null)
            m_backgroundImage = m_button.GetComponent<Image>();

        if (module.GetModuleType() == EModuleType.body)
            CommonUtility.SetUILocText(m_typeText, "ship_module_select_body");
        else if (module.GetModuleType() == EModuleType.engine)
            CommonUtility.SetUILocText(m_typeText, "ship_module_select_engine");
        else if (module.GetModuleType() == EModuleType.beam)
            CommonUtility.SetUILocText(m_typeText, "ship_module_select_beam");
        else if (module.GetModuleType() == EModuleType.missile)
            CommonUtility.SetUILocText(m_typeText, "ship_module_select_missile");
        else if (module.GetModuleType() == EModuleType.hanger)
            CommonUtility.SetUILocText(m_typeText, "ship_module_select_hanger");



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

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
    }
}
