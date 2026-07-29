// 탐사 그리드 셀 버튼 — 고정 그리드 좌표 기반 배치, 상태별 색상 표시, 클릭 콜백
// 3D 월드 좌표가 필요하면(셀 진입 시 등) 이 버튼의 화면 좌표(GetScreenPosition)에서 카메라로 광선을 쏴 역산 — UITabExplorationGrid에서 처리
// 이음선(UIZoneConnector) 불필요 — 격자 배치라 인접 여부가 위치만으로 이미 명확함
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EGridCellVisualState
{
    Unvisited, // 안 가본 곳 — 인접하지 않고 클리어도 안 됨, 클릭 불가
    Reachable, // 인접함 — 깜빡임 강조, 클릭 가능
    Current,   // 현재 위치
    Cleared,   // 클리어됨 — 재방문 가능
    Blocked,   // 완전 통행불가(절차적 생성 시 막힌 셀) — 버튼 자체를 숨김, 자리만 차지
}

public class GridCellButton : MonoBehaviour
{
    [SerializeField] private RectTransform m_rectTransform;
    [SerializeField] private Image m_backgroundImage;
    [SerializeField] private Image m_borderImage;
    [SerializeField] private TextMeshProUGUI m_stateLabel;
    [SerializeField] private Button m_button;

    private int m_row;
    private int m_col;
    private bool m_isStart;
    private bool m_isEscape;
    private System.Action<int, int> m_onClick;
    private Coroutine m_blinkCoroutine;

    public void Initialize(GridCellData cellData, System.Action<int, int> onClick)
    {
        m_row = cellData.row;
        m_col = cellData.col;
        m_isStart = cellData.isStart;
        m_isEscape = cellData.isEscape;
        m_onClick = onClick;

        UpdateStateLabel();

        if (m_button != null)
        {
            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        m_onClick?.Invoke(m_row, m_col);
    }

    // start/escape가 clear보다 우선 표시 — 셀 하나에 여러 상태가 겹쳐도 위치(시작/탈출)가 더 중요한 정보이기 때문
    private void UpdateStateLabel(bool isCleared = false)
    {
        if (m_stateLabel == null) return;

        if (m_isStart == true)
            m_stateLabel.text = "Start";
        else if (m_isEscape == true)
            m_stateLabel.text = "Escape";
        else if (isCleared == true)
            m_stateLabel.text = "Cleared";
        else
            m_stateLabel.text = string.Empty;
    }

    // originOffset: 그리드 전체를 CellRoot 중심에 정렬하기 위한 보정값 (그리드 크기에 따라 가변, UITabExplorationGrid에서 계산)
    public void SetAnchoredPosition(float cellSize, Vector2 originOffset)
    {
        if (m_rectTransform == null) return;
        Vector2 anchoredPos = new Vector2(m_row * cellSize + originOffset.x, m_col * cellSize + originOffset.y);
        m_rectTransform.anchoredPosition = anchoredPos;
    }

    // Screen Space Overlay 캔버스에서는 rectTransform.position이 곧 화면 좌표 — 3D 월드 좌표 역산에 사용
    public Vector3 GetScreenPosition()
    {
        return m_rectTransform != null ? m_rectTransform.position : Vector3.zero;
    }

    public void SetVisualState(EGridCellVisualState state)
    {
        StopBlink();

        // 완전 통행불가 셀은 자리만 차지 — 보이지도, 눌리지도 않음
        if (state == EGridCellVisualState.Blocked)
        {
            gameObject.SetActive(false);
            return;
        }
        if (gameObject.activeSelf == false) gameObject.SetActive(true);

        Color fillColor;
        Color borderColor;
        switch (state)
        {
            case EGridCellVisualState.Current:
                fillColor = CommonUtility.PaletteColor("Selected");
                borderColor = CommonUtility.PaletteColor("GeneralBright1");
                break;
            case EGridCellVisualState.Reachable:
                fillColor = CommonUtility.PaletteColor("Unlocked");
                borderColor = CommonUtility.PaletteColor("UnlockedSelected");
                break;
            case EGridCellVisualState.Cleared:
                fillColor = CommonUtility.PaletteColor("GeneralDark1");
                borderColor = CommonUtility.PaletteColor("Unlocked");
                break;
            default: // Unvisited
                fillColor = CommonUtility.PaletteColor("Zone.Locked");
                borderColor = CommonUtility.PaletteColor("GeneralDark1");
                break;
        }

        if (m_backgroundImage != null) m_backgroundImage.color = fillColor;
        if (m_borderImage != null) m_borderImage.color = borderColor;

        UpdateStateLabel(state == EGridCellVisualState.Cleared);

        bool interactable = state == EGridCellVisualState.Reachable || state == EGridCellVisualState.Cleared;
        if (m_button != null) m_button.interactable = interactable;

        if (state == EGridCellVisualState.Reachable)
            m_blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        Color baseColor = CommonUtility.PaletteColor("Unlocked");
        while (true)
        {
            float blinkPhase = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            if (m_backgroundImage != null)
                m_backgroundImage.color = Color.Lerp(baseColor, Color.white, blinkPhase);
            yield return null;
        }
    }

    private void StopBlink()
    {
        if (m_blinkCoroutine != null)
        {
            StopCoroutine(m_blinkCoroutine);
            m_blinkCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopBlink();
    }
}
