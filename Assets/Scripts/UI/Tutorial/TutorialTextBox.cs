using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;

// 튜토리얼 텍스트 박스
public class TutorialTextBox : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private TMP_Text m_messageText;
    [SerializeField] private RectTransform m_boxRect;

    [Header("타이핑 효과")]
    [SerializeField] private bool m_useTypewriter = true;
    [SerializeField] private float m_typewriterSpeed = 0.03f;

    [Header("스토리 모드 (타겟 없을 때)")]
    [SerializeField] private float m_storyModeWidth = 800f;
    [SerializeField] private Vector2 m_storyModePosition = new Vector2(0, 0); // 화면 중앙

    private Coroutine m_typewriterCoroutine;
    private float m_originalWidth;
    private StringBuilder m_stringBuilder = new StringBuilder(256);

    private void Awake()
    {
        if (m_boxRect != null)
            m_originalWidth = m_boxRect.sizeDelta.x;
    }

    // 메시지 표시
    public void ShowMessage(string message, Vector2 offset, RectTransform targetUI, Vector2 customSize = default)
    {
        if (m_typewriterCoroutine != null)
        {
            StopCoroutine(m_typewriterCoroutine);
            m_typewriterCoroutine = null;
        }

        // 위치 설정
        if (m_boxRect != null)
        {
            if (targetUI != null)
            {
                // GetWorldCorners로 실제 렌더링된 크기/위치 계산 (LayoutGroup/ContentSizeFitter 대응)
                Vector3[] corners = new Vector3[4];
                targetUI.GetWorldCorners(corners);
                Vector3 targetCenter = (corners[0] + corners[2]) * 0.5f;

                // Canvas 스케일 보정 (월드 좌표 → 로컬 좌표)
                float scale = m_boxRect.lossyScale.x;
                float actualWidth = Mathf.Abs(corners[3].x - corners[0].x) / scale;
                Vector3 scaledOffset = (Vector3)offset * scale;

                m_boxRect.position = targetCenter + scaledOffset;

                // 사이즈 적용 (customSize가 0이면 타겟 너비 사용)
                float width = customSize.x > 0 ? customSize.x : actualWidth;
                float height = customSize.y > 0 ? customSize.y : m_boxRect.sizeDelta.y;
                m_boxRect.sizeDelta = new Vector2(width, height);
            }
            else
            {
                // 스토리 모드: 화면 중앙, 넓은 너비
                m_boxRect.anchoredPosition = m_storyModePosition + offset;
                float width = customSize.x > 0 ? customSize.x : m_storyModeWidth;
                float height = customSize.y > 0 ? customSize.y : m_boxRect.sizeDelta.y;
                m_boxRect.sizeDelta = new Vector2(width, height);
            }
        }

        // 텍스트 표시
        if (m_messageText != null)
        {
            // 부모가 비활성화 상태면 코루틴 사용 불가
            if (m_useTypewriter && gameObject.activeInHierarchy)
                m_typewriterCoroutine = StartCoroutine(TypewriterEffect(message));
            else
                m_messageText.text = message;
        }
    }

    // 타이핑 효과
    private IEnumerator TypewriterEffect(string message)
    {
        m_stringBuilder.Clear();
        m_messageText.text = "";

        WaitForSeconds wait = new WaitForSeconds(m_typewriterSpeed);

        for (int i = 0; i < message.Length; i++)
        {
            m_stringBuilder.Append(message[i]);
            m_messageText.text = m_stringBuilder.ToString();
            yield return wait;
        }

        m_typewriterCoroutine = null;
    }

    // 즉시 전체 텍스트 표시
    public void ShowFullText()
    {
        if (m_typewriterCoroutine != null)
        {
            StopCoroutine(m_typewriterCoroutine);
            m_typewriterCoroutine = null;
        }
    }

    // 숨기기
    public void Hide()
    {
        if (m_typewriterCoroutine != null)
        {
            StopCoroutine(m_typewriterCoroutine);
            m_typewriterCoroutine = null;
        }
        gameObject.SetActive(false);
    }
}
