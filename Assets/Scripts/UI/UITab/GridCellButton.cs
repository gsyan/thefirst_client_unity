// 탐사 그리드 셀 버튼 — 고정 그리드 좌표 기반 배치(월드좌표 변환 불필요), 상태별 색상 표시, 클릭 콜백
// 이음선(UIZoneConnector) 불필요 — 격자 배치라 인접 여부가 위치만으로 이미 명확함
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum EGridCellVisualState
{
    Locked,    // 인접하지 않음 — 클릭 불가
    Reachable, // 인접함 — 깜빡임 강조, 클릭 가능
    Current,   // 현재 위치
    Cleared,   // 클리어됨 — 재방문 가능
}

public class GridCellButton : MonoBehaviour
{
    [SerializeField] private RectTransform m_rectTransform;
    [SerializeField] private Image m_backgroundImage;
    [SerializeField] private Image m_startIcon;
    [SerializeField] private Image m_escapeIcon;
    [SerializeField] private Button m_button;

    private int m_x;
    private int m_y;
    private System.Action<int, int> m_onClick;
    private Coroutine m_blinkCoroutine;

    public void Initialize(GridCellData cellData, System.Action<int, int> onClick)
    {
        m_x = cellData.x;
        m_y = cellData.y;
        m_onClick = onClick;

        if (m_startIcon != null) m_startIcon.gameObject.SetActive(cellData.isStart);
        if (m_escapeIcon != null) m_escapeIcon.gameObject.SetActive(cellData.isEscape);

        if (m_button != null)
        {
            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        m_onClick?.Invoke(m_x, m_y);
    }

    // originOffset: 그리드 전체를 CellRoot 중심에 정렬하기 위한 보정값 (그리드 크기에 따라 가변, UITabExplorationGrid에서 계산)
    public void SetAnchoredPosition(float cellSize, Vector2 originOffset)
    {
        if (m_rectTransform == null) return;
        Vector2 anchoredPos = new Vector2(m_x * cellSize + originOffset.x, m_y * cellSize + originOffset.y);
        m_rectTransform.anchoredPosition = anchoredPos;
    }

    public void SetVisualState(EGridCellVisualState state)
    {
        StopBlink();

        Color color = CommonUtility.PaletteColor("General");
        if (state == EGridCellVisualState.Cleared) color = CommonUtility.PaletteColor("Unlocked");
        else if (state == EGridCellVisualState.Locked) color = CommonUtility.PaletteColor("Zone.Locked");
        else if (state == EGridCellVisualState.Current) color = CommonUtility.PaletteColor("Unlocked");

        if (m_backgroundImage != null) m_backgroundImage.color = color;

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
