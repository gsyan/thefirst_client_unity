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
    [SerializeField] private TMP_Text m_zoneNameText;
    [SerializeField] private TMP_Text m_resourceText;       // 자원 수치 (클리어 시 중앙 표시)
    [SerializeField] private CanvasGroup m_fogCanvasGroup;  // 안개 overlay (alpha로 제어)
    [SerializeField] private Image m_progressFill;          // 클리어 진행률 (Filled type)

    [Header("선택 외곽선")]
    [SerializeField] private Color m_colorSelected = new(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth = 4f;

    private UnityEngine.UI.Outline m_outline;
    private Coroutine m_revealCoroutine;

    public ZoneConfig m_zoneConfig { get; private set; }

    public void Initialize(ZoneConfig zoneConfig, UnityEngine.Events.UnityAction onClick, EZoneState state)
    {
        m_zoneConfig = zoneConfig;

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);

        if (m_zoneNameText != null)
            m_zoneNameText.text = zoneConfig.zoneName;

        var outlineTarget = m_bgImage != null ? m_bgImage.gameObject : m_button.gameObject;
        m_outline = outlineTarget.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = outlineTarget.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled = false;

        // 안개가 클릭을 막지 않도록
        if (m_fogCanvasGroup != null) m_fogCanvasGroup.blocksRaycasts = false;

        // ProgressFill 초기 비활성화 (프리팹 의존 없이 코드로 보장)
        if (m_progressFill != null) m_progressFill.gameObject.SetActive(false);

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

        if (m_progressFill != null)
        {
            m_progressFill.gameObject.SetActive(state == EZoneState.Current);
            if (state == EZoneState.Current) ApplyProgressRatio(0f);
        }
    }

    // 웨이브 진행에 따라 안개 서서히 걷힘
    public void SetClearProgress(int clearedWaves, int zoneClearCount)
    {
        float ratio = zoneClearCount > 0 ? Mathf.Clamp01((float)clearedWaves / zoneClearCount) : 0f;
        ApplyProgressRatio(ratio);
        SetFogAlpha(Mathf.Lerp(1f, 0.1f, ratio));
    }

    private void ApplyProgressRatio(float ratio)
    {
        if (m_progressFill == null) return;
        RectTransform rt = m_progressFill.rectTransform;
        // Y 앵커는 프리팹 설정 유지, X만 변경
        rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        if (m_resourceText == null || m_zoneConfig == null) return;
        var sb = new StringBuilder();
        if (m_zoneConfig.mineralPerHour > 0)
            sb.Append($"M:{CommonUtility.FormatBigNumber(m_zoneConfig.mineralPerHour)}/h");
        if (m_zoneConfig.mineralRarePerHour > 0)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"R:{CommonUtility.FormatBigNumber(m_zoneConfig.mineralRarePerHour)}/h");
        }
        m_resourceText.text = sb.ToString();
    }
}
