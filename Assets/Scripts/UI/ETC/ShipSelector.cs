// 함대 내 함선 선택 UI 카드 컴포넌트
// 선택 여부를 selectButton Image 알파(선택=0.5, 미선택=0)로 시각화, HP를 게이지 바(RectTransform 너비) + 수치 텍스트로 시각화
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShipSelector : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;    // selected Button
    [SerializeField] private TMP_Text m_textName;      // 함선 이름 텍스트
    [SerializeField] private Transform m_shipStatsContainer;
    [SerializeField] private Image m_hpBarFill;        // HP 게이지 fill (Image.fillMethod = Horizontal)
    [SerializeField] private TMP_Text m_textHp;        // HP 수치 텍스트
    [SerializeField] private Button m_manageButton;

    [SerializeField] private float m_healthLerpDuration = 0.4f;

    private Image m_selectButtonImage;
    private Color m_selectButtonOriginalColor;
    private Color m_selectButtonSelectedColor;
    private Coroutine m_healthLerpCoroutine;
    private RowImageText[] m_statRows;

    public SpaceShip Ship { get; private set; }

    private void Awake()
    {
        if (m_selectButton != null)
        {
            m_selectButtonImage = m_selectButton.GetComponent<Image>();
            if (m_selectButtonImage != null)
                m_selectButtonOriginalColor = m_selectButtonImage.color;
        }

        m_selectButtonSelectedColor = CommonUtility.PaletteColor("Selected");

        if (m_shipStatsContainer != null)
            m_statRows = m_shipStatsContainer.GetComponentsInChildren<RowImageText>(true);
    }

    public void InitializeShipSelector(SpaceShip ship, UnityEngine.Events.UnityAction onSelect, UnityEngine.Events.UnityAction onManage)
    {
        Ship = ship;

        if (m_healthLerpCoroutine != null)
        {
            StopCoroutine(m_healthLerpCoroutine);
            m_healthLerpCoroutine = null;
        }

        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); onSelect?.Invoke(); });
        m_selectButton.interactable = true;

        if (m_manageButton != null)
        {
            m_manageButton.onClick.RemoveAllListeners();
            m_manageButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); onManage?.Invoke(); });
            m_manageButton.gameObject.SetActive(false);
        }

        RefreshStats();
        SetHealthImmediate();
    }

    // 모듈 레벨업 등 스탯 변경 시 외부에서 호출 (HP 제외)
    public void RefreshStats()
    {
        if (Ship == null) return;

        if (m_textName != null)
            m_textName.text = CommonUtility.GetShipDisplayName(Ship);

        if (m_statRows != null && m_statRows.Length > 0)
        {
            m_statRows[0].SetTextWithString($"{CommonUtility.FormatBigNumber((long)Ship.m_spaceShipStatsOrg.attack)}");
            for (int i = 1; i < m_statRows.Length; i++)
                m_statRows[i].Hide();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipStatsContainer as RectTransform);
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
        if (m_selectButtonImage != null)
            m_selectButtonImage.color = selected ? m_selectButtonSelectedColor : m_selectButtonOriginalColor;
        if (m_manageButton != null) m_manageButton.gameObject.SetActive(selected);
    }

    // 파괴된 함선 상태: GeneralDark2 색상, HP=0, 선택 불가
    public void SetDestroyedState()
    {
        if (m_selectButtonImage != null)
            m_selectButtonImage.color = CommonUtility.PaletteColor("GeneralDark2");
        m_selectButton.interactable = false;
        if (m_manageButton != null) m_manageButton.gameObject.SetActive(false);
        if (m_hpBarFill != null) ApplyHealthRatio(0f);
    }

    private float GetHealthRatio()
    {
        float maxHp = Ship.m_spaceShipStatsOrg.health;
        return maxHp > 0f ? Mathf.Clamp01(Ship.m_spaceShipStatsCur.health / maxHp) : 0f;
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

    private void UpdateHpText()
    {
        if (m_textHp == null || Ship == null) return;
        long cur = (long)Ship.m_spaceShipStatsCur.health;
        long max = (long)Ship.m_spaceShipStatsOrg.health;
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
