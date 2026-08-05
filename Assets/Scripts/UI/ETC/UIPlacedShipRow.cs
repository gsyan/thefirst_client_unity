// 함대편성 UI — 배치된 함선 슬롯 1개. 빈 슬롯 상태(SetEmpty)와 채워진 상태(Setup)를 모두 표현 —
// 항상 일정 개수의 슬롯이 보여야 드래그로 놓을 자리가 눈에 보이고, 컨테이너 크기가 0으로 줄어드는 것도 방지됨
// 이름 표시 + 전방/후방 슬라이드 토글(UIToggleSlide) + 행 클릭(성능 컬럼에 이 함선 스탯 표시) + 드래그 드롭 시 하이라이트
using UnityEngine;
using UnityEngine.UI;

public class UIPlacedShipRow : MonoBehaviour
{
    [SerializeField] private RowLabelValue m_rowLabelValue;
    


    [SerializeField] private UIToggleSlide m_frontToggleSlide; // on = 전방, off = 후방
    [SerializeField] private Button m_rowButton; // 행 클릭 — 토글과는 별개 영역
    [SerializeField] private Image m_backgroundImage; // 빈 슬롯 표시 + 드래그 호버 하이라이트용 배경
    [SerializeField] private Image m_borderImage; // 성능 컬럼에 선택된 행임을 표시하는 외곽선 — 드래그 하이라이트(m_backgroundImage)와 별개

    private Color m_defaultBackgroundColor;
    private Color m_highlightColor;
    private Color m_lockedColor;
    private Color m_borderDefaultColor;
    private Color m_borderSelectedColor;
    private bool m_hasShip;
    private bool m_isLocked;

    private int m_index;
    private string m_shipPresetId;
    private System.Action<int, bool> m_onFrontToggled;
    private System.Action<int, string> m_onRowClicked;

    private void Awake()
    {
        m_highlightColor = CommonUtility.PaletteColor("Unlocked");
        m_lockedColor = CommonUtility.PaletteColor("Locked");
        m_borderDefaultColor = CommonUtility.PaletteColor("General.Dark1");
        m_borderSelectedColor = CommonUtility.PaletteColor("Selected");

        if (m_backgroundImage != null)
            m_defaultBackgroundColor = m_backgroundImage.color;
        if (m_rowButton != null)
            m_rowButton.onClick.AddListener(OnRowClicked);
        if (m_borderImage != null)
        {
            m_borderImage.gameObject.SetActive(true);
            m_borderImage.color = m_borderDefaultColor;
        }
    }

    // 빈 슬롯 — 배치된 함선 없음. 드래그 드롭 타겟으로만 존재
    public void SetEmpty(int index)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_shipPresetId = null;
        m_hasShip = false;
        m_isLocked = false;

        if (m_rowLabelValue != null)
        {
            m_rowLabelValue.SetRow("-", "", rawLabel: true, rawValue: true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_rowLabelValue.transform as RectTransform);
        }
        if (m_frontToggleSlide != null)
            m_frontToggleSlide.gameObject.SetActive(false);

        SetHighlighted(false);
        SetSelected(false);
    }

    // 잠긴 슬롯 — 커맨더 레벨이 아직 이 슬롯을 열지 않음. 드롭 타겟 아님, 흐림 배경으로만 존재 표시
    public void SetLocked(int index)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_shipPresetId = null;
        m_hasShip = false;
        m_isLocked = true;

        if (m_rowLabelValue != null)
        {
            m_rowLabelValue.SetRow("-", "", rawLabel: true, rawValue: true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_rowLabelValue.transform as RectTransform);
        }
        if (m_frontToggleSlide != null)
            m_frontToggleSlide.gameObject.SetActive(false);

        if (m_backgroundImage != null)
            m_backgroundImage.color = m_lockedColor;

        SetSelected(false);
    }

    public bool IsLocked()
    {
        return m_isLocked;
    }

    // showFrontToggle=false면 전방/후방을 편집 불가능한 라벨 텍스트로만 표시(적 함대 정보 열람 등 읽기전용 목적)
    public void Setup(int index, string shipPresetId, bool isFront, System.Action<int, bool> onFrontToggled, System.Action<int, string> onRowClicked, bool showFrontToggle = true)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_shipPresetId = shipPresetId;
        m_hasShip = true;
        m_isLocked = false;
        m_onFrontToggled = onFrontToggled;
        m_onRowClicked = onRowClicked;

        string positionKey = isFront ? "UIFleet_Front" : "UIFleet_Rear";

        if (showFrontToggle == true)
        {
            // 라벨 칸엔 슬롯 인덱스 기반 "Ship1"(1-based) — 함선 이름 로컬라이즈는 아직 미정이라 프리셋 코드(presetId)를 값 칸에 그대로 표시
            // 위치(전방/후방)는 토글 라벨로 표시하므로 값 칸은 preset id 전용으로 씀
            if (m_rowLabelValue != null)
            {
                m_rowLabelValue.SetRow($"Ship{index + 1}", shipPresetId, rawLabel: true, rawValue: true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_rowLabelValue.transform as RectTransform);
            }

            if (m_frontToggleSlide != null)
            {
                m_frontToggleSlide.gameObject.SetActive(true);
                // UIToggleSlide는 on=오른쪽/off=왼쪽인데, 이 스위치는 왼쪽=전방/오른쪽=후방이라 값을 반전해서 넘김
                m_frontToggleSlide.SetOn(isFront == false, OnToggleSlideChanged);
                m_frontToggleSlide.SetLabelText(positionKey);
            }
        }
        else
        {
            // 읽기전용 — 토글 대신 라벨 Value 칸에 전/후방 텍스트를 그대로 표기
            if (m_rowLabelValue != null)
            {
                m_rowLabelValue.SetRow(shipPresetId, LocalizationManager.Instance.Get(positionKey), rawLabel: true, rawValue: true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_rowLabelValue.transform as RectTransform);
            }

            if (m_frontToggleSlide != null)
                m_frontToggleSlide.gameObject.SetActive(false);
        }

        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (m_backgroundImage != null)
            m_backgroundImage.color = highlighted ? m_highlightColor : m_defaultBackgroundColor;
    }

    // 성능 컬럼에 이 행의 스탯이 표시 중임을 외곽선으로 표시 — 드래그 하이라이트(SetHighlighted)와 별개
    public void SetSelected(bool selected)
    {
        if (m_borderImage == null) return;
        m_borderImage.color = selected == true ? m_borderSelectedColor : m_borderDefaultColor;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // isOn=true는 스위치가 오른쪽(후방)에 있다는 뜻이라 전방 여부로 다시 반전
    private void OnToggleSlideChanged(bool isOn)
    {
        if (m_hasShip == false) return;
        bool isFront = isOn == false;
        if (m_onFrontToggled != null) m_onFrontToggled(m_index, isFront);
    }

    private void OnRowClicked()
    {
        if (m_hasShip == false) return;

        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onRowClicked != null) m_onRowClicked(m_index, m_shipPresetId);
    }
}
