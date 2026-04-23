// 존 스테이지 버튼 — 3D 월드 좌표(fleetPosition)를 매 LateUpdate마다 Screen Space로 변환해 배치
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIZoneStageButton : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Image m_bgImage;

    private static readonly Color k_colorLocked  = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Color k_colorCurrent = new Color(0.2f, 0.6f, 1.0f, 0.9f);
    private static readonly Color k_colorCleared = new Color(0.3f, 0.7f, 0.3f, 0.9f);

    private RectTransform m_rectTransform;
    private Camera m_worldCamera;
    private Vector3 m_worldPos;
    private UnityEngine.UI.Outline m_outline;

    public ZoneStageConfig ZoneStageConfig { get; private set; }

    private void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(ZoneStageConfig config, System.Action onClick, EZoneState state, Camera worldCamera)
    {
        ZoneStageConfig = config;
        m_worldCamera = worldCamera;
        m_worldPos = config.fleetPosition;

        if (m_outline == null && m_bgImage != null)
        {
            m_outline = m_bgImage.GetComponent<UnityEngine.UI.Outline>();
            if (m_outline == null)
                m_outline = m_bgImage.gameObject.AddComponent<UnityEngine.UI.Outline>();
            m_outline.effectColor = new Color(1f, 0.8f, 0.2f, 1f);
            m_outline.effectDistance = new Vector2(4f, -4f);
            m_outline.enabled = false;
        }

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(() => onClick?.Invoke());

        if (m_nameText != null)
            m_nameText.text = config.zoneName;

        SetState(state);
    }

    public void SetState(EZoneState state)
    {
        if (m_bgImage != null)
        {
            if (state == EZoneState.Cleared)
                m_bgImage.color = k_colorCleared;
            else if (state == EZoneState.Current)
                m_bgImage.color = k_colorCurrent;
            else
                m_bgImage.color = k_colorLocked;
        }
        m_button.interactable = state != EZoneState.Locked;
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
    }

    private void LateUpdate()
    {
        if (m_worldCamera == null) return;
        Vector3 screenPos = m_worldCamera.WorldToScreenPoint(m_worldPos);
        // 카메라 뒤쪽이면 화면 밖으로 밀어냄
        if (screenPos.z < 0f)
            screenPos = new Vector3(-9999f, -9999f, 0f);
        m_rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }
}
