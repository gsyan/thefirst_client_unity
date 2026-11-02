// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1칸 — 모듈은 항상 설치된 상태로 취급(설치/해제 토글 없음)
// 역할 분리: 이 행의 이름 텍스트(m_nameText)는 "무슨 카테고리 몇 번 슬롯인지"(예: 빔 1), m_levelText는 현재/팬딩 레벨 표기
// 모듈 레벨업(ManageButton)은 업적포인트 기반 아키타입 레벨업(M6) — UIPopupModuleReinforce를 엶
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIModuleSlotToggleRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_nameText; // "빔 1", "미사일 2"처럼 카테고리+슬롯번호 표기
    [SerializeField] private TMP_Text m_levelText; // 현재/팬딩 레벨 — 선택 여부와 무관하게 항상 표시
    [SerializeField] private Button m_manageButton; // 레벨업 팝업(UIPopupModuleReinforce) 오픈
    [SerializeField] private Button m_selectButton; // 행 선택(하이라이트) 클릭 영역 — 매니지 버튼과 별개
    [SerializeField] private GameObject m_selectedImage; // 선택된 행 강조 이미지

    private EModuleType m_moduleType;
    private int m_slotIndex;
    private System.Action<EModuleType, int> m_onRowSelected;
    private System.Action<EModuleType, int> m_onManageClicked;

    public void Setup(EModuleType moduleType, int slotIndex,
        bool isSelected, string moduleSubType,
        System.Action<EModuleType, int> onRowSelected,
        System.Action<EModuleType, int> onManageClicked)
    {
        m_moduleType = moduleType;
        m_slotIndex = slotIndex;
        m_onRowSelected = onRowSelected;
        m_onManageClicked = onManageClicked;

        if (m_nameText != null)
            m_nameText.text = $"{LocalizationManager.Instance.Get(CommonUtility.GetModuleTypeLabelKey(moduleType))} {slotIndex + 1}";

        if (m_levelText != null)
        {
            m_levelText.gameObject.SetActive(true);
            m_levelText.text = LocalizationManager.Instance.Get("UI_TierLabel", CommonUtility.ParseTier(moduleSubType));
        }

        if (m_manageButton != null)
        {
            m_manageButton.gameObject.SetActive(true);
            m_manageButton.onClick.RemoveAllListeners();
            m_manageButton.onClick.AddListener(OnManageButtonClicked);
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

    private void OnSelectButtonClicked()
    {
        if (m_onRowSelected != null) m_onRowSelected(m_moduleType, m_slotIndex);
    }

    private void OnManageButtonClicked()
    {
        if (m_onManageClicked != null) m_onManageClicked(m_moduleType, m_slotIndex);
    }
}
