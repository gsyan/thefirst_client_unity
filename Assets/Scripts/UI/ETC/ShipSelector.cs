// 함대 내 함선 선택 UI 버튼 컴포넌트
// 선택 여부를 Outline, 함선 체력 비율을 분할 색상(위 빨강 / 아래 초록)으로 시각화
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ShipSelector : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_healthGreenImage;  // 배경 (초록, 고정 크기)

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorSelected = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth  = 4f;
    [SerializeField] private float m_healthLerpDuration = 0.4f;

    private UnityEngine.UI.Outline m_outline;
    private Coroutine m_healthLerpCoroutine;

    public SpaceShip Ship { get; private set; }

    public void Initialize(SpaceShip ship, UnityEngine.Events.UnityAction onClick)
    {
        Ship = ship;

        // 풀 재사용 시 실행 중인 코루틴 정리
        if (m_healthLerpCoroutine != null)
        {
            StopCoroutine(m_healthLerpCoroutine);
            m_healthLerpCoroutine = null;
        }

        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);

        m_outline = m_button.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = m_button.gameObject.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor    = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled        = false;

        // 초기화 시 즉시 반영
        SetHealthImmediate();
    }

    // HP 변경 시 외부에서 호출 — 비활성 계층이면 즉시 반영, 활성이면 코루틴 Lerp
    public void RefreshHealth()
    {
        if (Ship == null || m_healthGreenImage == null) return;

        float targetRatio = GetHealthRatio();
        if (m_healthLerpCoroutine != null)
            StopCoroutine(m_healthLerpCoroutine);

        if (gameObject.activeInHierarchy == false)
        {
            ApplyHealthRatio(targetRatio);
            return;
        }

        m_healthLerpCoroutine = StartCoroutine(LerpHealth(targetRatio));
    }

    public void SetSelected(bool selected)
    {
        if (m_outline != null)
            m_outline.enabled = selected;
    }

    private float GetHealthRatio()
    {
        float maxHp = Ship.m_spaceShipStatsOrg.health_power;
        return maxHp > 0f ? Mathf.Clamp01(Ship.m_spaceShipStatsCur.health_power / maxHp) : 0f;
    }

    private void SetHealthImmediate()
    {
        if (Ship == null || m_healthGreenImage == null) return;
        float ratio = GetHealthRatio();
        ApplyHealthRatio(ratio);
    }

    private void ApplyHealthRatio(float ratio)
    {
        // ratio=1.0 → anchorMax.y=1 → 전체 초록 / ratio=0.8 → 아래 80% 초록, 위 20% 빨강 노출
        RectTransform rt = m_healthGreenImage.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, ratio);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator LerpHealth(float targetRatio)
    {
        RectTransform rt = m_healthGreenImage.rectTransform;
        float startRatio = rt.anchorMax.y;
        float elapsed = 0f;

        while (elapsed < m_healthLerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_healthLerpDuration);
            ApplyHealthRatio(Mathf.Lerp(startRatio, targetRatio, t));
            yield return null;
        }

        ApplyHealthRatio(targetRatio);
        m_healthLerpCoroutine = null;
    }
}
