// 함대편성 UI — 배치 가능한 함선 프리셋 1행. 클릭하면 스탯 팝업, 드래그하면 배치된 함선 리스트로 이동
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIAvailablePresetRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RowLabelValue m_nameRow;
    [SerializeField] private RowLabelValue m_typeRow; // "타입"은 별도 필드가 없어 prefabName(선체 종류)을 그대로 표시
    [SerializeField] private RowLabelValue m_costRow; // 라벨은 "비용"만, 단위(지휘력)는 값 쪽에 숫자와 함께 표시(레이아웃 균형용)
    [SerializeField] private Button m_button; // 클릭(스탯 팝업) — 눌림 시각 피드백까지 기본 제공

    private ShipPresetData m_preset;
    private System.Action<ShipPresetData> m_onClick;
    private System.Action<ShipPresetData, PointerEventData> m_onDrop;
    private System.Action<PointerEventData> m_onDragging;

    private Canvas m_rootCanvas;
    private GameObject m_dragGhost;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnButtonClicked);
    }

    public void Setup(ShipPresetData preset, System.Action<ShipPresetData> onClick, System.Action<ShipPresetData, PointerEventData> onDrop, System.Action<PointerEventData> onDragging)
    {
        gameObject.SetActive(true);
        m_preset = preset;
        m_onClick = onClick;
        m_onDrop = onDrop;
        m_onDragging = onDragging;

        // 함선 이름 로컬라이즈(displayNameKey)는 아직 미정 — 프리셋 코드(presetId)를 값으로 그대로 표시(더미 프리셋이라 확정 이름 없음)
        if (m_nameRow != null)
            m_nameRow.SetRow("UIAvailablePresetRow_Name", preset.presetId, rawValue: true);
        if (m_typeRow != null)
            m_typeRow.SetRow("UIAvailablePresetRow_Type", preset.prefabName, rawValue: true);
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
        if (m_dragGhost != null) return; // 드래그가 시작된 제스처면 클릭으로 처리하지 않음(드래그+클릭 중복 방지)

        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClick != null) m_onClick(m_preset);
    }

    // 원본 행은 리스트 안 제자리에 그대로 두고, 커서를 따라가는 비주얼만 고스트로 복제해서 사용 —
    // 원본을 reparent하면 리스트(Vertical Layout Group)에서 그 칸이 빠져 나머지 행이 당겨 붙어버림
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (m_rootCanvas == null)
            m_rootCanvas = GetComponentInParent<Canvas>();
        if (m_rootCanvas == null) return;

        RectTransform sourceRect = transform as RectTransform;
        Vector2 originalSize = sourceRect.rect.size;

        m_dragGhost = Instantiate(gameObject, m_rootCanvas.transform);
        UIAvailablePresetRow ghostRow = m_dragGhost.GetComponent<UIAvailablePresetRow>();
        if (ghostRow != null)
            Destroy(ghostRow); // 고스트는 순수 비주얼 — 클릭/드래그 핸들러 중복 방지

        CanvasGroup ghostCanvasGroup = m_dragGhost.GetComponent<CanvasGroup>();
        if (ghostCanvasGroup == null)
            ghostCanvasGroup = m_dragGhost.AddComponent<CanvasGroup>();
        ghostCanvasGroup.blocksRaycasts = false; // 고스트가 드롭 위치 레이캐스트를 가리지 않도록

        // 원본은 스크롤뷰 Content 폭에 맞춰 가로 스트레치 anchor를 쓰므로, 루트 캔버스에 그대로 붙이면
        // 캔버스 전체 폭 기준으로 다시 늘어남 — 고정 anchor + 원본 크기로 강제 지정
        RectTransform ghostRect = m_dragGhost.transform as RectTransform;
        ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRect.pivot = new Vector2(0.5f, 0.5f);
        ghostRect.sizeDelta = originalSize;

        m_dragGhost.transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (m_dragGhost != null)
            m_dragGhost.transform.position = eventData.position;
        if (m_onDragging != null) m_onDragging(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (m_onDrop != null) m_onDrop(m_preset, eventData);

        if (m_dragGhost != null)
            Destroy(m_dragGhost);
        m_dragGhost = null;
    }
}
