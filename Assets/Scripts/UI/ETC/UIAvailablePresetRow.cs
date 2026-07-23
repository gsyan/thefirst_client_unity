// 함대편성 UI — 배치 가능한 함선 프리셋 1행. 클릭하면 스탯 팝업, 드래그하면 배치된 함선 리스트로 이동
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIAvailablePresetRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RowLabelValue m_rowLabelValue;
    [SerializeField] private Button m_button; // 클릭(스탯 팝업) — 눌림 시각 피드백까지 기본 제공

    private ShipPresetData m_preset;
    private System.Action<ShipPresetData> m_onClick;
    private System.Action<ShipPresetData, PointerEventData> m_onDrop;

    private Transform m_originalParent;
    private int m_originalSiblingIndex;
    private Canvas m_rootCanvas;
    private CanvasGroup m_canvasGroup;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnButtonClicked);
    }

    public void Setup(ShipPresetData preset, System.Action<ShipPresetData> onClick, System.Action<ShipPresetData, PointerEventData> onDrop)
    {
        gameObject.SetActive(true);
        m_preset = preset;
        m_onClick = onClick;
        m_onDrop = onDrop;

        // 함선 이름 로컬라이즈는 아직 미정 — 프리셋 코드(presetId)를 그대로 표시 (더미 프리셋이라 확정 이름 없음)
        if (m_rowLabelValue != null)
            m_rowLabelValue.SetRow(preset.presetId, $"{preset.commandCost}", rawValue: true, rawLabel: true);
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (m_rootCanvas == null)
            m_rootCanvas = GetComponentInParent<Canvas>();
        if (m_canvasGroup == null)
            m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

        m_canvasGroup.blocksRaycasts = false; // 드래그 중엔 자기 자신이 드롭 위치 레이캐스트를 가리지 않도록

        m_originalParent = transform.parent;
        m_originalSiblingIndex = transform.GetSiblingIndex();
        if (m_rootCanvas != null)
            transform.SetParent(m_rootCanvas.transform, worldPositionStays: true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (m_canvasGroup != null)
            m_canvasGroup.blocksRaycasts = true;

        if (m_onDrop != null) m_onDrop(m_preset, eventData);

        // 드롭 처리 후엔 UITabFleetComposition.RefreshFleetComposition()이 전체 목록을 다시 채우므로,
        // 이 인스턴스는 원래 위치로 되돌려놔서 다음 갱신 때 재사용 가능한 상태로 복귀
        transform.SetParent(m_originalParent, worldPositionStays: false);
        transform.SetSiblingIndex(m_originalSiblingIndex);
    }
}
