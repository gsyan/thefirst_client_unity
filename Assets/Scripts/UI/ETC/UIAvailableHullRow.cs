// 함체 선택 팝업(UIHullPickerView) — 배치 가능한 함체 1행. 클릭으로 선택
using UnityEngine;
using UnityEngine.UI;

public class UIAvailableHullRow : MonoBehaviour
{
    [SerializeField] private RowLabelValue m_nameRow;
    [SerializeField] private RowLabelValue m_costRow; // 라벨은 "비용"만, 단위(지휘력)는 값 쪽에 숫자와 함께 표시(레이아웃 균형용)
    [SerializeField] private Button m_button; // 클릭(선택) — 눌림 시각 피드백까지 기본 제공
    [SerializeField] private Image m_selectedImage; // 이 함체가 현재 선택 상태임을 표시 — 색 변경이 아니라 오브젝트 자체를 켜고 끔

    // 증감 표시 색상 - 지휘력이 늘어나면(더 비싼 함체) 경고색, 줄어들면(여유 확보) 강조색
    private const string k_increaseColorHex = "#FF5555";
    private const string k_decreaseColorHex = "#4CD97B";

    private ModuleData m_hull;
    private System.Action<ModuleData> m_onClick;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnButtonClicked);
        if (m_selectedImage != null)
            m_selectedImage.gameObject.SetActive(false);
    }

    // 이 함체가 현재 선택 상태임을 표시 — 색 변경이 아니라 오브젝트 자체를 켜고 끔
    public void SetSelectedAvailableHullRow(bool selected)
    {
        if (m_selectedImage == null) return;
        m_selectedImage.gameObject.SetActive(selected);
    }

    // deltaCost: 이 함체로 교체했을 때 현재 슬롯 대비 지휘력 증감(유지되는 모듈 반영, 양수=추가 소모/음수=회수) —
    // 정적 statPoint가 아니라 호출부(UIHullPickerView)가 슬롯 유지 계산 결과로 넘겨줌
    public void Setup(ModuleData hull, int deltaCost, System.Action<ModuleData> onClick)
    {
        gameObject.SetActive(true);
        m_hull = hull;
        m_onClick = onClick;

        // 함선 이름은 moduleSubType 이름을 UI.csv 로컬라이즈 키로 그대로 사용(별도 displayNameKey 없음)
        if (m_nameRow != null)
            m_nameRow.SetRow("UIAvailableHullRow_Name", hull.moduleSubType, rawValue: false);
        if (m_costRow != null)
            m_costRow.SetRow("UIAvailableHullRow_Cost", $"{BuildDeltaText(deltaCost)} CP", rawValue: true);
    }

    // 0이면 부호 없이 "0", 양수면 "+N"(경고색), 음수면 "-N"(강조색) — 부호와 숫자를 리치텍스트 색으로 함께 표시
    private string BuildDeltaText(int deltaCost)
    {
        if (deltaCost == 0) return "0";
        string sign = deltaCost > 0 ? "+" : "";
        string colorHex = deltaCost > 0 ? k_increaseColorHex : k_decreaseColorHex;
        return $"<color={colorHex}>{sign}{deltaCost}</color>";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClick != null) m_onClick(m_hull);
    }
}
