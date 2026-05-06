// 함선 개별 모듈 슬롯 UI 버튼 컴포넌트
// ModuleBase와 1:1 매칭되며, 선택 여부를 Outline / 잠금 여부를 배경색으로 시각화
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ModuleSelector : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_borderImage;
    [SerializeField] private Image m_bgImage;
    [SerializeField] private TMP_Text m_buttonText;

    [Header("상태별 색상")]
    // locked
    [SerializeField] private Color m_colorLockedOutLine = new Color(0.59f, 0f, 0f, 0.25f); // 150,0,0,64
    [SerializeField] private Color m_colorLockedBg = new Color(0.59f, 0f, 0f, 0.03f); // 150,0,0,8
    [SerializeField] private Color m_colorLockedOutLineSelected = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color m_colorLockedBgSelected = new Color(0.59f, 0f, 0f, 0.25f);
    // unlocked
    [SerializeField] private Color m_colorUnlockedOutLine = new Color(0f, 0.59f, 0.25f, 0.25f);
    [SerializeField] private Color m_colorUnlockedBg = new Color( 0f, 0.59f, 0.25f, 0.03f);    
    [SerializeField] private Color m_colorUnlockedOutLineSelected = new Color(0f, 1f, 0.5f, 1f);
    [SerializeField] private Color m_colorUnlockedBgSelected = new Color(0f, 0.59f, 0.25f, 0.25f);

    public ModuleBase Module { get; private set; }

    public void InitializeModuleSelector(ModuleBase module, UnityEngine.Events.UnityAction onClick)
    {
        Module = module;

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);
        m_button.interactable = true;

        if (m_borderImage == null)
            m_borderImage = m_button.GetComponent<Image>();
        if (m_bgImage == null)
            m_bgImage = m_button.GetComponentInChildren<Image>();

        // 잠금 여부에 따라 색 설정
        m_borderImage.color = (module is ModulePlaceholder) ? m_colorLockedOutLine : m_colorUnlockedOutLine;
        m_bgImage.color = (module is ModulePlaceholder) ? m_colorLockedBg : m_colorUnlockedBg;

        if (m_buttonText == null)
            m_buttonText = m_button.GetComponentInChildren<TMP_Text>();

        // 슬롯 번호 표시
        m_buttonText.text = (module.GetModuleSlotIndex() + 1).ToString();
        bool bModuleUnlocked = !(module is ModulePlaceholder);
        m_buttonText.color = (module is ModulePlaceholder) ? m_colorLockedOutLineSelected : m_colorUnlockedOutLineSelected;
    }

    // 현재 함체에 해당 슬롯이 없는 경우: 시각적 유지, 기능 비활성화
    public void SetNotExist()
    {
        Module = null;
        m_button.onClick.RemoveAllListeners();
        m_button.interactable = false;
        m_buttonText.gameObject.SetActive(false);
    }

    public void SetModuleSelected(bool selected)
    {
        if(Module == null) return;
        Color m_colorOutLine = (Module is ModulePlaceholder) ? m_colorLockedOutLine : m_colorUnlockedOutLine;
        Color m_colorBg = (Module is ModulePlaceholder) ? m_colorLockedBg : m_colorUnlockedBg;
        Color m_colorOutLineSelected = (Module is ModulePlaceholder) ? m_colorLockedOutLineSelected : m_colorUnlockedOutLineSelected;
        Color m_colorBgSelected = (Module is ModulePlaceholder) ? m_colorLockedBgSelected : m_colorUnlockedBgSelected;

        m_borderImage.color = (selected == true) ? m_colorOutLineSelected : m_colorOutLine;
        m_bgImage.color = (selected == true) ? m_colorBgSelected : m_colorBg;
        //m_buttonText.text.color
    }
}
