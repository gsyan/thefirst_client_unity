// 함선 프리셋 선택 팝업(UIShipPresetPickerView) — 배치 가능한 함선 프리셋 1행. 클릭으로 선택
using UnityEngine;
using UnityEngine.UI;

public class UIAvailablePresetRow : MonoBehaviour
{
    [SerializeField] private RowLabelValue m_nameRow;
    [SerializeField] private RowLabelValue m_costRow; // 라벨은 "비용"만, 단위(지휘력)는 값 쪽에 숫자와 함께 표시(레이아웃 균형용)
    [SerializeField] private Button m_button; // 클릭(선택) — 눌림 시각 피드백까지 기본 제공

    private ShipPresetData m_preset;
    private System.Action<ShipPresetData> m_onClick;

    private Color m_buttonDefaultColor;
    private Color m_buttonSelectedColor;
    private Color m_textDefaultColor;
    private Color m_textSelectedColor;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnButtonClicked);

        m_buttonDefaultColor = CommonUtility.PaletteColor("Cyan");
        m_buttonSelectedColor = CommonUtility.PaletteColor("Green");
        if (m_button != null && m_button.targetGraphic != null)
            m_button.targetGraphic.color = m_buttonDefaultColor;

        m_textDefaultColor = CommonUtility.PaletteColor("Text.Dark1");
        m_textSelectedColor = Color.black;
    }

    // 이 프리셋이 현재 선택 상태임을 표시 — 버튼 이미지 색 + 라벨/값 텍스트 색을 함께 토글
    public void SetSelectedAvailablePresetRow(bool selected)
    {
        if (m_button != null && m_button.targetGraphic != null)
            m_button.targetGraphic.color = selected == true ? m_buttonSelectedColor : m_buttonDefaultColor;

        // Color textColor = selected == true ? m_textSelectedColor : m_textDefaultColor;
        // if (m_nameRow != null) m_nameRow.SetTextColor(textColor);
        // if (m_costRow != null) m_costRow.SetTextColor(textColor);
    }

    public void Setup(ShipPresetData preset, System.Action<ShipPresetData> onClick)
    {
        gameObject.SetActive(true);
        m_preset = preset;
        m_onClick = onClick;

        // 함선 이름은 preset.presetId를 UI.csv 로컬라이즈 키로 그대로 사용(별도 displayNameKey 없음)
        if (m_nameRow != null)
            m_nameRow.SetRow("UIAvailablePresetRow_Name", preset.presetId, rawValue: false);
        if (m_costRow != null)
        {
            string commandPowerLabel = LocalizationManager.Instance.Get("UITabCommander_CommandPower");
            m_costRow.SetRow("UIAvailablePresetRow_Cost", $"{preset.commandCost}({commandPowerLabel})", rawValue: true);
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClick != null) m_onClick(m_preset);
    }
}
