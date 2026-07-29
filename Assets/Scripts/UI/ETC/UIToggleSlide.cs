// 좌우로 슬라이드되는 On/Off 스위치(iOS 스타일) — Unity 기본 Toggle엔 슬라이드 애니메이션이 없어 직접 구현
// 배경(Image) + 핸들(RectTransform, 원형 버튼) 구조. 핸들의 좌/우 끝 위치는 배경/핸들 크기로 자동 계산
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIToggleSlide : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_backgroundImage;
    [SerializeField] private RectTransform m_handleRect;
    [SerializeField] private TMP_Text m_labelText; // 배경 위에 겹쳐 표시되는 상태 텍스트(예: 전방/후방)

    [SerializeField] private Color m_onBackgroundColor = new Color(0.3f, 0.85f, 0.5f, 1f);
    [SerializeField] private Color m_offBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float m_slideDuration = 0.15f;

    private bool m_isOn;
    private float m_handleOnX;
    private float m_handleOffX;
    private bool m_initialized;
    private Coroutine m_slideCoroutine;
    private System.Action<bool> m_onValueChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    // 비활성 계층에서 Instantiate 직후 바로 Setup이 호출될 수 있어(Awake가 아직 안 돈 시점), 각 public 진입점에서 보장
    private void EnsureInitialized()
    {
        if (m_initialized == true) return;
        m_initialized = true;

        if (m_button != null)
            m_button.onClick.AddListener(OnClicked);

        // 핸들 크기는 건드리지 않음(직접 원하는 크기로 설정) — 배경/핸들의 "현재" 크기만 읽어서 좌우 이동 범위(마진)만 계산
        if (m_backgroundImage != null && m_handleRect != null)
        {
            float halfBgWidth = m_backgroundImage.rectTransform.rect.width * 0.5f;
            float halfHandleWidth = m_handleRect.rect.width * 0.5f;
            float margin = halfBgWidth - halfHandleWidth;
            m_handleOffX = -margin;
            m_handleOnX = margin;
        }
    }

    // 애니메이션 없이 즉시 상태 반영 — 리스트 행 재사용 시 등 초기값 세팅용
    public void SetOn(bool isOn, System.Action<bool> onValueChanged)
    {
        EnsureInitialized();
        m_isOn = isOn;
        m_onValueChanged = onValueChanged;
        ApplyVisualImmediate(isOn);
    }

    public bool IsOn()
    {
        return m_isOn;
    }

    public void SetLabelText(string text, bool rawText = false)
    {
        if (m_labelText == null) return;

        m_labelText.text = rawText == true ? text : LocalizationManager.Instance.Get(text);
    }

    private void OnClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        m_isOn = m_isOn == false;
        AnimateToState(m_isOn);

        if (m_onValueChanged != null) m_onValueChanged(m_isOn);
    }

    private void ApplyVisualImmediate(bool isOn)
    {
        if (m_backgroundImage != null)
            m_backgroundImage.color = isOn ? m_onBackgroundColor : m_offBackgroundColor;

        if (m_handleRect != null)
        {
            Vector2 pos = m_handleRect.anchoredPosition;
            pos.x = isOn ? m_handleOnX : m_handleOffX;
            m_handleRect.anchoredPosition = pos;
        }
    }

    private void AnimateToState(bool isOn)
    {
        if (m_backgroundImage != null)
            m_backgroundImage.color = isOn ? m_onBackgroundColor : m_offBackgroundColor;

        if (m_handleRect == null) return;

        if (m_slideCoroutine != null) StopCoroutine(m_slideCoroutine);
        m_slideCoroutine = StartCoroutine(Co_SlideHandle(isOn ? m_handleOnX : m_handleOffX));
    }

    private IEnumerator Co_SlideHandle(float targetX)
    {
        float startX = m_handleRect.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < m_slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_slideDuration));

            Vector2 pos = m_handleRect.anchoredPosition;
            pos.x = Mathf.Lerp(startX, targetX, t);
            m_handleRect.anchoredPosition = pos;

            yield return null;
        }

        Vector2 finalPos = m_handleRect.anchoredPosition;
        finalPos.x = targetX;
        m_handleRect.anchoredPosition = finalPos;
    }
}
