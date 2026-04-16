// 존 맵 셀 — 미클리어(안개)/도전가능/클리어 상태 시각화, 클리어 시 안개 reveal 애니메이션
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ZoneMapCell : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_bgImage;               // 배경 이미지 (outline 부착 대상)
    [SerializeField] private TMP_Text m_zoneStageNameText;
    [SerializeField] private TMP_Text m_resourceText;       // 자원 수치 (클리어 시 중앙 표시)
    [SerializeField] private CanvasGroup m_fogCanvasGroup;  // 안개 overlay (alpha로 제어)
    
    [Header("선택 외곽선")]
    [SerializeField] private Color m_colorSelected = new(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth = 4f;

    private UnityEngine.UI.Outline m_outline;
    private Coroutine m_revealCoroutine;

    public ZoneStageConfig m_zoneStageConfig { get; private set; }

    public void Initialize(ZoneStageConfig zoneStageConfig, UnityEngine.Events.UnityAction onClick, EZoneState state)
    {
        m_zoneStageConfig = zoneStageConfig;

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);

        if (m_zoneStageNameText != null)
            m_zoneStageNameText.text = zoneStageConfig.zoneName;

        var outlineTarget = m_bgImage != null ? m_bgImage.gameObject : m_button.gameObject;
        m_outline = outlineTarget.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = outlineTarget.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled = false;

        // 안개가 클릭을 막지 않도록
        if (m_fogCanvasGroup != null) m_fogCanvasGroup.blocksRaycasts = false;

        SetState(state, false);
    }

    public void SetState(EZoneState state, bool animate = true)
    {
        bool cleared = state == EZoneState.Cleared;

        if (cleared && animate)
            RevealWithAnimation();
        else
            SetFogAlpha(cleared ? 0f : 1f);

        if (m_resourceText != null)
        {
            m_resourceText.gameObject.SetActive(cleared);
            if (cleared) RefreshResourceText();
        }
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null) m_outline.enabled = selected;
    }

    private void RevealWithAnimation()
    {
        if (m_revealCoroutine != null) StopCoroutine(m_revealCoroutine);

        // 비활성 상태(다른 그룹 탭 등)에서는 코루틴 불가 — 즉시 최종 상태 적용
        if (gameObject.activeInHierarchy == false)
        {
            SetFogAlpha(0f);
            if (m_resourceText != null)
            {
                m_resourceText.gameObject.SetActive(true);
                RefreshResourceText();
            }
            return;
        }

        m_revealCoroutine = StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        float duration = 1.5f;
        float t = 0f;
        float startAlpha = m_fogCanvasGroup != null ? m_fogCanvasGroup.alpha : 1f;

        while (t < duration)
        {
            t += Time.deltaTime;
            SetFogAlpha(Mathf.Lerp(startAlpha, 0f, t / duration));
            yield return null;
        }
        SetFogAlpha(0f);

        if (m_resourceText != null)
        {
            m_resourceText.gameObject.SetActive(true);
            RefreshResourceText();
        }
    }

    private void SetFogAlpha(float alpha)
    {
        if (m_fogCanvasGroup != null) m_fogCanvasGroup.alpha = alpha;
    }

    private void RefreshResourceText()
    {
        if (m_resourceText == null || m_zoneStageConfig == null) return;
        var sb = new StringBuilder();
        void AppendIfPositive(string icon, float value)
        {
            if (value <= 0f) return;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{CommonUtility.Sprite(icon)} {CommonUtility.FormatBigNumber(value)}/h");
        }
        AppendIfPositive("crystal-growth",  m_zoneStageConfig.mineralPerHour);
        AppendIfPositive("minerals", m_zoneStageConfig.mineralRarePerHour);
        AppendIfPositive("emerald", m_zoneStageConfig.mineralExoticPerHour);
        AppendIfPositive("fire-gem", m_zoneStageConfig.mineralDarkPerHour);
        m_resourceText.text = sb.ToString();
    }
}
