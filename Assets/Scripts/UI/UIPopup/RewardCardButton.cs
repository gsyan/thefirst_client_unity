// 보상카드 구매 팝업의 카드 버튼 1개 — 이름/설명/비용 텍스트와 클릭(즉시 구매) 콜백을 스스로 관리
// 등장(가운데 점에서 피어남)/퇴장(구매 카드는 튀었다가 빨려들 듯 축소, 나머지는 페이드아웃) 연출도 담당 — localScale + CanvasGroup 알파만 사용
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardCardButton : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_iconImage;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_descText;
    [SerializeField] private TMP_Text m_mineralCostText;
    [SerializeField] private GameObject m_selectedIndicator; // 구매 처리 중 등 시각 표시용 — 없으면(null) 시각 표시 없이 클릭만 동작

    private static readonly Color k_insufficientMineralColor = Color.red;

    private const float k_enterDuration = 0.28f;
    private const float k_enterFadeRatio = 0.6f; // 등장 시간 중 앞부분 이 비율 동안 알파가 0→1
    private const float k_purchasedPunchDuration = 0.08f;
    private const float k_purchasedPunchScale = 1.15f;
    private const float k_purchasedShrinkDuration = 0.18f;
    private const float k_otherExitDuration = 0.15f;
    private const float k_otherExitScale = 0.85f;

    private System.Action m_onClicked;
    private int m_mineralCost;
    private Color m_defaultMineralCostColor;
    private CanvasGroup m_canvasGroup;
    private Coroutine m_animCoroutine;

    private void Awake()
    {
        if (m_button == null)
            m_button = GetComponent<Button>();
        m_button.onClick.AddListener(OnClicked);

        if (m_mineralCostText != null)
            m_defaultMineralCostColor = m_mineralCostText.color;

        m_canvasGroup = GetComponent<CanvasGroup>();
        if (m_canvasGroup == null)
            m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetCard(RewardCardData card, int mineralCost, System.Action onClicked)
    {
        gameObject.SetActive(true);
        m_onClicked = onClicked;
        m_mineralCost = mineralCost;

        if (card == null)
        {
            m_nameText.text = "";
            m_descText.text = "";
            if (m_mineralCostText != null)
                m_mineralCostText.text = "";
            return;
        }

        CommonUtility.SetUILocText(m_nameText, card.nameKey);
        // Buff_MineralFlatBonus는 실제 가산량이 card.value1이 아니라 "이번에 지불할 비용 그대로"라, 설명에도 value1 대신 이번 비용을 보여줌
        float descValue1 = card.effectType == ECardEffectType.Buff_MineralFlatBonus ? mineralCost : card.value1;
        m_descText.text = LocalizationManager.Instance.Get(card.descKey, descValue1, card.value2);
        if (m_mineralCostText != null)
            m_mineralCostText.text = LocalizationManager.Instance.Get("RewardCardButton_MineralCost", mineralCost);
        if (m_iconImage != null)
            m_iconImage.sprite = UISpriteCache.Get(card.iconName);
        SetSelected(false);
    }

    public void Hide()
    {
        StopAnim();
        SetVisual(1f, 1f);
        gameObject.SetActive(false);
        m_onClicked = null;
    }

    // 가운데 점에서 피어나듯 등장 — delay는 카드별 순차 등장(왼쪽부터)용
    public void PlayEnter(float delay)
    {
        StopAnim();
        if (gameObject.activeInHierarchy == false)
        {
            SetVisual(1f, 1f);
            return;
        }
        m_animCoroutine = StartCoroutine(Co_Enter(delay));
    }

    // 퇴장 연출 시작 후 총 소요시간(초)을 반환 — 호출부가 이 시간이 지난 뒤 새 카드를 바인딩하도록
    public float PlayExit(bool isPurchased)
    {
        StopAnim();
        if (gameObject.activeInHierarchy == false) return 0f;

        m_animCoroutine = StartCoroutine(isPurchased == true ? Co_ExitPurchased() : Co_ExitOther());
        float duration = isPurchased == true ? k_purchasedPunchDuration + k_purchasedShrinkDuration : k_otherExitDuration;
        return duration;
    }

    private void StopAnim()
    {
        if (m_animCoroutine == null) return;
        StopCoroutine(m_animCoroutine);
        m_animCoroutine = null;
    }

    private void SetVisual(float scale, float alpha)
    {
        transform.localScale = new Vector3(scale, scale, 1f);
        if (m_canvasGroup != null)
            m_canvasGroup.alpha = alpha;
    }

    private IEnumerator Co_Enter(float delay)
    {
        SetVisual(0f, 0f);

        float waited = 0f;
        while (waited < delay)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < k_enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / k_enterDuration);
            float scale = EaseOutBack(t);
            float alpha = Mathf.Clamp01(t / k_enterFadeRatio);
            SetVisual(scale, alpha);
            yield return null;
        }

        SetVisual(1f, 1f);
        m_animCoroutine = null;
    }

    // 구매한 카드 — 살짝 튀어 커졌다가 중심으로 빨려들 듯 축소+페이드아웃
    private IEnumerator Co_ExitPurchased()
    {
        float elapsed = 0f;
        while (elapsed < k_purchasedPunchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / k_purchasedPunchDuration);
            float scale = Mathf.Lerp(1f, k_purchasedPunchScale, t);
            SetVisual(scale, 1f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < k_purchasedShrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / k_purchasedShrinkDuration);
            float easedT = t * t; // 처음엔 천천히, 끝으로 갈수록 빨리 빨려듦
            float scale = Mathf.Lerp(k_purchasedPunchScale, 0f, easedT);
            float alpha = 1f - easedT;
            SetVisual(scale, alpha);
            yield return null;
        }

        SetVisual(0f, 0f);
        m_animCoroutine = null;
    }

    // 구매하지 않은 나머지 카드 — 살짝 작아지며 페이드아웃
    private IEnumerator Co_ExitOther()
    {
        float elapsed = 0f;
        while (elapsed < k_otherExitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / k_otherExitDuration);
            float scale = Mathf.Lerp(1f, k_otherExitScale, t);
            float alpha = 1f - t;
            SetVisual(scale, alpha);
            yield return null;
        }

        SetVisual(k_otherExitScale, 0f);
        m_animCoroutine = null;
    }

    // 끝에서 목표(1)를 살짝 넘겼다가 돌아오는 이징 — "툭 피어나는" 느낌
    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float shifted = t - 1f;
        float cubic = (overshoot + 1f) * shifted * shifted * shifted;
        float quadratic = overshoot * shifted * shifted;
        return 1f + cubic + quadratic;
    }

    // 보유 광물이 비용보다 적어도 버튼은 계속 눌리게 둠(클릭하면 팝업으로 부족하다고 안내) — 비용 텍스트 색상으로만 부족함을 표시
    public void SetAffordable(bool isAffordable)
    {
        if (m_mineralCostText != null)
            m_mineralCostText.color = isAffordable ? m_defaultMineralCostColor : k_insufficientMineralColor;
    }

    // 서버 응답을 기다리는 동안 등 구매 가능 여부와 무관한 이유로 버튼 자체를 잠글 때 사용
    public void SetInteractable(bool interactable)
    {
        m_button.interactable = interactable;
    }

    public void SetSelected(bool isSelected)
    {
        if (m_selectedIndicator != null)
            m_selectedIndicator.SetActive(isSelected);
    }

    private void OnClicked()
    {
        m_onClicked?.Invoke();
    }
}
