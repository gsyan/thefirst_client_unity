// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1칸 — UIPlacedShipRow의 전/후방 스위치와 동일하게 UIToggleSlide(슬라이드 토글) 사용
// isLocked면 SetInteractable(false)로 클릭만 막음(항상 on 상태) — 카테고리 slot0 등 공격 모듈 0개 방지용 1차 방어선, 서버가 최종 방어선
// 역할 분리: 이 행의 이름 텍스트(m_nameText)는 "무슨 카테고리 몇 번 슬롯인지"(예: 빔 1), 토글 자체 라벨은 "장착/해제" 상태를 표기
using TMPro;
using UnityEngine;

public class UIModuleSlotToggleRow : MonoBehaviour
{
    [SerializeField] private UIToggleSlide m_toggleSlide;
    [SerializeField] private TMP_Text m_nameText; // "빔 1", "미사일 2"처럼 카테고리+슬롯번호 표기

    private EModuleType m_moduleType;
    private int m_slotIndex;
    private System.Action<EModuleType, int, bool> m_onToggle; // (moduleType, slotIndex, 요청할 install 목표값)

    public void Setup(EModuleType moduleType, int slotIndex, bool installed, bool isLocked, System.Action<EModuleType, int, bool> onToggle)
    {
        m_moduleType = moduleType;
        m_slotIndex = slotIndex;
        m_onToggle = onToggle;

        if (m_nameText != null)
            m_nameText.text = $"{LocalizationManager.Instance.Get(GetModuleTypeLabelKey(moduleType))} {slotIndex + 1}";

        if (m_toggleSlide == null) return;

        m_toggleSlide.SetOn(installed, OnToggleChanged);
        m_toggleSlide.SetInteractable(isLocked == false);
        SetInstalledLabel(installed);
    }

    private void OnToggleChanged(bool isOn)
    {
        SetInstalledLabel(isOn);
        if (m_onToggle != null) m_onToggle(m_moduleType, m_slotIndex, isOn);
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
        return "";
    }
}
