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
    [SerializeField] private TMP_Text m_buttonText;

    // locked
    private Color m_colorLocked;
    private Color m_colorLockedSelected;
    // unlocked
    private Color m_colorUnlocked;
    private Color m_colorUnlockedSelected;
    
    public ModuleBase Module { get; private set; }

    public void InitializeModuleSelector(ModuleBase module, UnityEngine.Events.UnityAction onClick)
    {
        Module = module;

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);
        m_button.interactable = true;

        if (m_borderImage == null)
            m_borderImage = m_button.GetComponent<Image>();

        var palette = Resources.Load<ColorPalette>("DataTable/ColorPalette");
        if (palette != null)
        {
            m_colorLocked = palette.GetColor("Locked");
            m_colorLockedSelected = palette.GetColor("LockedSelected");
            m_colorUnlocked = palette.GetColor("Unlocked");
            m_colorUnlockedSelected = palette.GetColor("UnlockedSelected");
        }
        
        // 잠금 여부에 따라 색 설정
        m_borderImage.color = (module is ModulePlaceholder) ? m_colorLocked : m_colorUnlocked;
        
        if (m_buttonText == null)
            m_buttonText = m_button.GetComponentInChildren<TMP_Text>();

        // 슬롯 번호 표시
        m_buttonText.text = (module.GetModuleSlotIndex() + 1).ToString();
        m_buttonText.color = (module is ModulePlaceholder) ? m_colorLockedSelected : m_colorUnlockedSelected;
    }

    // 현재 함체에 해당 슬롯이 없는 경우: 기능 비활성화 + Locked 색상으로 표시
    public void SetNotExist()
    {
        Module = null;
        m_button.onClick.RemoveAllListeners();
        m_button.interactable = false;
        m_buttonText.gameObject.SetActive(false);
        m_borderImage.color = m_colorLocked;
    }

    public void SetModuleSelected(bool selected)
    {
        if(Module == null) return;
        Color m_colorBase = (Module is ModulePlaceholder) ? m_colorLocked : m_colorUnlocked;
        Color m_colorSelected = (Module is ModulePlaceholder) ? m_colorLockedSelected : m_colorUnlockedSelected;        
        m_borderImage.color = (selected == true) ? m_colorSelected : m_colorBase;
    }
}
