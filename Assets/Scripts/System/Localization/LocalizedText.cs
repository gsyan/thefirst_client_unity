using UnityEngine;
using TMPro;

// TMP_Text에 붙여서 자동으로 로컬라이제이션 적용하는 컴포넌트
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string m_key;

    private TMP_Text m_text;
    private bool m_isSubscribed;

    public string Key
    {
        get => m_key;
        set
        {
            m_key = value;
            UpdateText();
        }
    }

    private void Awake()
    {
        m_text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Subscribe();
        UpdateText();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (m_isSubscribed) return;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += UpdateText;
            m_isSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!m_isSubscribed) return;
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= UpdateText;
        }
        m_isSubscribed = false;
    }

    private void UpdateText()
    {
        if (m_text == null) return;
        if (string.IsNullOrEmpty(m_key)) return;
        if (LocalizationManager.Instance == null) return;

        m_text.text = LocalizationManager.Instance.Get(m_key);
    }

    // 포맷 인자와 함께 텍스트 설정
    public void SetTextWithArgs(params object[] args)
    {
        if (m_text == null || LocalizationManager.Instance == null) return;
        m_text.text = LocalizationManager.Instance.Get(m_key, args);
    }

    // 키 변경 후 포맷 인자 적용
    public void SetKeyWithArgs(string key, params object[] args)
    {
        m_key = key;
        if (m_text == null || LocalizationManager.Instance == null) return;
        m_text.text = LocalizationManager.Instance.Get(m_key, args);
    }

#if UNITY_EDITOR
    // 에디터에서 키 입력 시 미리보기 (Play 모드 아닐 때)
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (m_text == null) m_text = GetComponent<TMP_Text>();
        if (m_text != null && !string.IsNullOrEmpty(m_key))
        {
            m_text.text = $"[{m_key}]";
        }
    }
#endif
}
