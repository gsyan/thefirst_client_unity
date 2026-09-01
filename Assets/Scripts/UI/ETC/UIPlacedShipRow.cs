// 함대편성 UI — 배치된 함선 슬롯 1개. 빈 슬롯 상태(SetEmpty)와 채워진 상태(Setup)를 모두 표현 —
// 항상 일정 개수의 슬롯이 보여야 드래그로 놓을 자리가 눈에 보이고, 컨테이너 크기가 0으로 줄어드는 것도 방지됨
// 함선 이름 텍스트 + 함선 타입 선택 버튼(누르면 UIShipPresetPickerView가 뜸) + 전방/후방 슬라이드 토글(UIToggleSlide) + 행 클릭(성능 컬럼에 이 함선 스탯 표시)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

public class UIPlacedShipRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_shipNameText;
    [SerializeField] private Button m_shipTypeSelectButton;
    [SerializeField] private TMP_Text m_shipTypeButtonText;

    [SerializeField] private UIToggleSlide m_frontToggleSlide; // on = 전방, off = 후방
    [SerializeField] private Button m_rowButton; // 행 클릭 — 토글과는 별개 영역
    [SerializeField] private Image m_backgroundImage; // 빈 슬롯 표시 + 드래그 호버 하이라이트용 배경
    // 성능 컬럼에 선택된 행임을 표시 — 드래그 하이라이트(m_backgroundImage)와 별개, 색 변경이 아니라 오브젝트 자체를 켜고 끔
    //[FormerlySerializedAs("m_borderImage")]
    [SerializeField] private Image m_selectedImage;

    private Color m_defaultBackgroundColor;
    private Color m_highlightColor;
    private Color m_lockedColor;
    private bool m_hasShip;
    private bool m_isLocked;

    private int m_index;
    private string m_hullSubType;
    private System.Action<int, bool> m_onFrontToggled;
    private System.Action<int, string> m_onRowClicked;
    private System.Action<int> m_onTypeSelectClicked;

    private void Awake()
    {
        m_highlightColor = CommonUtility.PaletteColor("Unlocked");
        m_lockedColor = CommonUtility.PaletteColor("Ship.Locked");

        if (m_backgroundImage != null)
            m_defaultBackgroundColor = m_backgroundImage.color;
        if (m_rowButton != null)
            m_rowButton.onClick.AddListener(OnRowClicked);
        if (m_shipTypeSelectButton != null)
            m_shipTypeSelectButton.onClick.AddListener(OnTypeSelectButtonClicked);
        if (m_selectedImage != null)
            m_selectedImage.gameObject.SetActive(false);
    }

    // 빈 슬롯 — 배치된 함선 없음. 타입선택 버튼을 눌러 바로 배치 가능
    public void SetEmpty(int index, System.Action<int> onTypeSelectClicked)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_hullSubType = null;
        m_hasShip = false;
        m_isLocked = false;
        m_onTypeSelectClicked = onTypeSelectClicked;

        if (m_shipNameText != null)
            m_shipNameText.text = "-";
        if (m_shipTypeSelectButton != null)
        {
            m_shipTypeSelectButton.gameObject.SetActive(true);
            m_shipTypeSelectButton.interactable = onTypeSelectClicked != null; // 전투 중 등 콜백이 없으면 시각적으로도 비활성화
        }
        if (m_shipTypeButtonText != null)
            CommonUtility.SetUILocText(m_shipTypeButtonText, "UIFleet_PlaceShip");
        if (m_frontToggleSlide != null)
            m_frontToggleSlide.gameObject.SetActive(false);

        RebuildTypeSelectButtonLayout();
        SetHighlighted(false);
        SetSelected(false);
    }

    // 잠긴 슬롯 — 커맨더 레벨이 아직 이 슬롯을 열지 않음. 타입선택 버튼도 숨김
    public void SetLocked(int index)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_hullSubType = null;
        m_hasShip = false;
        m_isLocked = true;
        m_onTypeSelectClicked = null;

        if (m_shipNameText != null)
            m_shipNameText.text = "-";
        if (m_shipTypeSelectButton != null)
            m_shipTypeSelectButton.gameObject.SetActive(false);
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

    // showFrontToggle=false면 전방/후방을 편집 불가능한 라벨 텍스트로만 표시하고 타입선택 버튼도 숨김(적 함대 정보 열람 등 읽기전용 목적)
    public void Setup(int index, string hullSubType, bool isFront, System.Action<int, bool> onFrontToggled, System.Action<int, string> onRowClicked, System.Action<int> onTypeSelectClicked, bool showFrontToggle = true)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_hullSubType = hullSubType;
        m_hasShip = true;
        m_isLocked = false;
        m_onFrontToggled = onFrontToggled;
        m_onRowClicked = onRowClicked;
        m_onTypeSelectClicked = onTypeSelectClicked;

        string positionKey = isFront ? "UIFleet_Front" : "UIFleet_Rear";

        if (showFrontToggle == true)
        {
            // 이름 칸엔 슬롯 인덱스 기반 "Ship1"(1-based) — 함선 이름 로컬라이즈는 아직 미정. 타입선택 버튼엔 현재 함체 코드(hullSubType)를 그대로 표시
            if (m_shipNameText != null)
                m_shipNameText.text = $"Ship{index + 1}";
            if (m_shipTypeButtonText != null)
                m_shipTypeButtonText.text = hullSubType;
            if (m_shipTypeSelectButton != null)
            {
                m_shipTypeSelectButton.gameObject.SetActive(true);
                m_shipTypeSelectButton.interactable = onTypeSelectClicked != null; // 전투 중 등 콜백이 없으면 시각적으로도 비활성화
            }

            if (m_frontToggleSlide != null)
            {
                m_frontToggleSlide.gameObject.SetActive(true);
                // UIToggleSlide는 on=오른쪽/off=왼쪽인데, 이 스위치는 왼쪽=전방/오른쪽=후방이라 값을 반전해서 넘김
                m_frontToggleSlide.SetOn(isFront == false, OnToggleSlideChanged);
                m_frontToggleSlide.SetLabelText(positionKey);
            }

            RebuildTypeSelectButtonLayout();
        }
        else
        {
            // 읽기전용 — 이름 칸에 함체 코드, 전/후방은 그대로 텍스트로만 표기, 타입선택 버튼은 숨김
            if (m_shipNameText != null)
                m_shipNameText.text = hullSubType;
            if (m_shipTypeSelectButton != null)
                m_shipTypeSelectButton.gameObject.SetActive(false);

            if (m_frontToggleSlide != null)
                m_frontToggleSlide.gameObject.SetActive(false);
        }

        SetHighlighted(false);
    }

    // 재사용된(이미 active였던) 풀 오브젝트는 SetActive(true) 호출이 no-op이라 ContentSizeFitter의 OnEnable 재계산이
    // 자동으로 걸리지 않을 수 있음 — 텍스트를 바꾼 직후 버튼 자신의 RectTransform을 직접 강제 리빌드해서 폭을 확정시킴
    private void RebuildTypeSelectButtonLayout()
    {
        if (m_shipTypeSelectButton == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipTypeSelectButton.transform as RectTransform);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (m_backgroundImage != null)
            m_backgroundImage.color = highlighted ? m_highlightColor : m_defaultBackgroundColor;
    }

    // 성능 컬럼에 이 행의 스탯이 표시 중임을 표시 — 드래그 하이라이트(SetHighlighted)와 별개, 오브젝트 자체를 켜고 끔
    public void SetSelected(bool selected)
    {
        if (m_selectedImage == null) return;
        m_selectedImage.gameObject.SetActive(selected);
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

        // OnShipFrontToggled 콜백 쪽에서 더 이상 이 행을 재바인딩하지 않으므로(슬라이드 애니메이션이 끊기는 걸 막기 위함),
        // 라벨 텍스트("전방"/"후방")는 여기서 직접 갱신해야 함
        if (m_frontToggleSlide != null)
            m_frontToggleSlide.SetLabelText(isFront ? "UIFleet_Front" : "UIFleet_Rear");

        if (m_onFrontToggled != null) m_onFrontToggled(m_index, isFront);
    }

    private void OnRowClicked()
    {
        if (m_hasShip == false) return;

        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onRowClicked != null) m_onRowClicked(m_index, m_hullSubType);
    }

    private void OnTypeSelectButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onTypeSelectClicked != null) m_onTypeSelectClicked(m_index);
    }
}
