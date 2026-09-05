// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1칸 — UIPlacedShipRow의 전/후방 스위치와 동일하게 UIToggleSlide(슬라이드 토글) 사용
// isLocked면 SetInteractable(false)로 클릭만 막음(항상 on 상태) — 카테고리 slot0 등 공격 모듈 0개 방지용 1차 방어선, 서버가 최종 방어선
// 역할 분리: 이 행의 이름 텍스트(m_nameText)는 "무슨 카테고리 몇 번 슬롯인지"(예: 빔 1), 토글 자체 라벨은 "장착/해제" 상태를 표기
// 강화 편집(Up/Down)은 이 행이 아니라 ManageButton으로 여는 UIPopupModuleReinforce에서 처리 — 이 행은 요약(Invested CP/Tier)과 선택 표시만 담당
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIModuleSlotToggleRow : MonoBehaviour
{
    [SerializeField] private UIToggleSlide m_toggleSlide;
    [SerializeField] private TMP_Text m_nameText; // "빔 1", "미사일 2"처럼 카테고리+슬롯번호 표기
    [SerializeField] private TMP_Text m_investedPointsText; // "Invested CP : N" — 미설치 슬롯이면 숨김
    [SerializeField] private TMP_Text m_tierText; // 현재/팬딩 티어 — 선택 여부와 무관하게 항상 표시(실드 제외)
    [SerializeField] private Button m_manageButton; // 강화 관리 팝업(UIPopupModuleReinforce) 오픈 — 미설치 슬롯이면 숨김
    [SerializeField] private Button m_selectButton; // 행 선택(하이라이트) 클릭 영역 — 토글/매니지 버튼과 별개
    [SerializeField] private GameObject m_selectedImage; // 선택된 행 강조 이미지

    private EModuleType m_moduleType;
    private int m_slotIndex;
    private System.Action<EModuleType, int, bool> m_onToggle; // (moduleType, slotIndex, 요청할 install 목표값)
    private System.Action<EModuleType, int> m_onRowSelected;
    private System.Action<EModuleType, int> m_onManageClicked;

    public void Setup(EModuleType moduleType, int slotIndex, bool installed, bool isLocked,
        int investedPoints, bool isSelected, string moduleSubType,
        System.Action<EModuleType, int, bool> onToggle,
        System.Action<EModuleType, int> onRowSelected,
        System.Action<EModuleType, int> onManageClicked)
    {
        m_moduleType = moduleType;
        m_slotIndex = slotIndex;
        m_onToggle = onToggle;
        m_onRowSelected = onRowSelected;
        m_onManageClicked = onManageClicked;

        if (m_nameText != null)
            m_nameText.text = $"{LocalizationManager.Instance.Get(GetModuleTypeLabelKey(moduleType))} {slotIndex + 1}";

        if (m_toggleSlide != null)
        {
            m_toggleSlide.SetOn(installed, OnToggleChanged);
            m_toggleSlide.SetInteractable(isLocked == false);
            SetInstalledLabel(installed);
        }

        // 실드는 강화 포인트 개념이 없음(on/off만 지원) — Invested CP/ManageButton 노출 대상에서 제외
        bool showReinforceControls = installed == true && moduleType != EModuleType.shield;
        if (m_investedPointsText != null)
        {
            m_investedPointsText.gameObject.SetActive(showReinforceControls);
            m_investedPointsText.text = $"Invested CP : {investedPoints}";
        }

        if (m_manageButton != null)
        {
            m_manageButton.gameObject.SetActive(showReinforceControls);
            m_manageButton.onClick.RemoveAllListeners();
            m_manageButton.onClick.AddListener(OnManageButtonClicked);
        }

        // 실드를 제외한 카테고리는 설치 여부와 무관하게 항상 표시 — 미설치 슬롯도 장착 시 어떤 티어가 될지 알 수 있어야 함
        bool showTier = moduleType != EModuleType.shield;
        if (m_tierText != null)
        {
            m_tierText.gameObject.SetActive(showTier);
            if (showTier == true)
                m_tierText.text = $"Tier {CommonUtility.ParseTier(moduleSubType)}";
        }

        SetSelected(isSelected);

        if (m_selectButton != null)
        {
            m_selectButton.onClick.RemoveAllListeners();
            m_selectButton.onClick.AddListener(OnSelectButtonClicked);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (m_selectedImage != null)
            m_selectedImage.SetActive(isSelected);
    }

    private void OnToggleChanged(bool isOn)
    {
        SetInstalledLabel(isOn);
        if (m_onToggle != null) m_onToggle(m_moduleType, m_slotIndex, isOn);
    }

    private void OnSelectButtonClicked()
    {
        if (m_onRowSelected != null) m_onRowSelected(m_moduleType, m_slotIndex);
    }

    private void OnManageButtonClicked()
    {
        if (m_onManageClicked != null) m_onManageClicked(m_moduleType, m_slotIndex);
    }

    private void SetInstalledLabel(bool installed)
    {
        if (m_toggleSlide == null) return;
        m_toggleSlide.SetLabelText(installed ? "UIFleet_ModuleSlot_Installed" : "UIFleet_ModuleSlot_NotInstalled");
    }

    private string GetModuleTypeLabelKey(EModuleType moduleType)
    {
        if (moduleType == EModuleType.beam) return "module_type_beam";
        if (moduleType == EModuleType.missile) return "module_type_missile";
        if (moduleType == EModuleType.hangar) return "module_type_hangar";
        if (moduleType == EModuleType.shield) return "module_type_shield";
        return "";
    }
}
