// 함대편성 UI — 배치된 함선 1행. 이름 표시 + 전방/후방 토글(ToggleButton) + 행 클릭(성능 컬럼에 이 함선 스탯 표시)
using UnityEngine;
using UnityEngine.UI;

public class UIPlacedShipRow : MonoBehaviour
{
    [SerializeField] private RowLabelValue m_rowLabelValue;
    [SerializeField] private ToggleButton m_frontToggleButton; // on(선택 색상) = 전방, off = 후방
    [SerializeField] private Button m_rowButton; // 행 클릭 — 토글 버튼과는 별개 영역

    private int m_index;
    private string m_shipPresetId;
    private bool m_isFront;
    private System.Action<int, bool> m_onFrontToggled;
    private System.Action<int, string> m_onRowClicked;

    private void Awake()
    {
        if (m_frontToggleButton != null)
            m_frontToggleButton.button.onClick.AddListener(OnToggleClicked);
        if (m_rowButton != null)
            m_rowButton.onClick.AddListener(OnRowClicked);
    }

    public void Setup(int index, string shipPresetId, bool isFront, System.Action<int, bool> onFrontToggled, System.Action<int, string> onRowClicked)
    {
        gameObject.SetActive(true);
        m_index = index;
        m_shipPresetId = shipPresetId;
        m_isFront = isFront;
        m_onFrontToggled = onFrontToggled;
        m_onRowClicked = onRowClicked;

        string positionKey = isFront ? "UITabFleetComposition_Front" : "UITabFleetComposition_Rear";
        // 함선 이름 로컬라이즈는 아직 미정 — 프리셋 코드(presetId)를 그대로 표시 (더미 프리셋이라 확정 이름 없음)
        if (m_rowLabelValue != null)
            m_rowLabelValue.SetRow(shipPresetId, positionKey, rawLabel: true);

        if (m_frontToggleButton != null)
            m_frontToggleButton.SetSelected(isFront);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnToggleClicked()
    {
        m_isFront = m_isFront == false;
        if (m_frontToggleButton != null)
            m_frontToggleButton.SetSelected(m_isFront);

        if (m_onFrontToggled != null) m_onFrontToggled(m_index, m_isFront);
    }

    private void OnRowClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onRowClicked != null) m_onRowClicked(m_index, m_shipPresetId);
    }
}
