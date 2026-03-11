// 함대 내 함선 선택 UI 카드 컴포넌트
// 선택 여부를 Outline, HP를 게이지 바(RectTransform 너비) + 수치 텍스트로 시각화, 함선 이름/ATK 표시
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShipSelector : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private TMP_Text m_textName;   // 함선 이름 텍스트
    [SerializeField] private TMP_Text m_textAtk;    // ATK 수치 텍스트
    [SerializeField] private Image m_hpBarFill;     // HP 게이지 fill (Image.fillMethod = Horizontal)
    [SerializeField] private TMP_Text m_textHp;     // HP 수치 텍스트

    [Header("선택 외곽선")]
    [SerializeField] private Color m_colorSelected = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth  = 4f;
    [SerializeField] private float m_healthLerpDuration = 0.4f;

    private UnityEngine.UI.Outline m_outline;
    private Coroutine m_healthLerpCoroutine;

    public SpaceShip Ship { get; private set; }

    public void Initialize(SpaceShip ship, UnityEngine.Events.UnityAction onClick)
    {
        Ship = ship;

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

        UpdateNameText();
        SetHealthImmediate();
        UpdateAtkText();
    }

    // HP 변경 시 외부에서 호출
    public void RefreshHealth()
    {
        if (Ship == null || m_hpBarFill == null) return;

        UpdateHpText();
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
        if (Ship == null || m_hpBarFill == null) return;
        ApplyHealthRatio(GetHealthRatio());
        UpdateHpText();
    }

    private void ApplyHealthRatio(float ratio)
    {
        // anchorMax.x로 우측을 줄여 체력 비율 표현
        RectTransform rt = m_hpBarFill.rectTransform;
        rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void UpdateNameText()
    {
        if (m_textName == null || Ship == null) return;
        m_textName.text = Ship.m_shipInfo.shipName;
    }

    private void UpdateAtkText()
    {
        if (m_textAtk == null || Ship == null) return;
        m_textAtk.text = $"ATK {CommonUtility.FormatBigNumber((long)Ship.m_spaceShipStatsOrg.attack_power)}";
    }

    private void UpdateHpText()
    {
        if (m_textHp == null || Ship == null) return;
        long cur = (long)Ship.m_spaceShipStatsCur.health_power;
        long max = (long)Ship.m_spaceShipStatsOrg.health_power;
        m_textHp.text = $"{CommonUtility.FormatBigNumber(cur)} / {CommonUtility.FormatBigNumber(max)}";
    }

    private IEnumerator LerpHealth(float targetRatio)
    {
        float startRatio = m_hpBarFill.rectTransform.anchorMax.x;
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
