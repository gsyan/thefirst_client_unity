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
    [SerializeField] private float m_storyModeWidth = 600f;
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
    public void ShowMessage(string message, Vector2 offset, RectTransform targetUI)
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
                // Canvas 스케일 기준 오프셋 계산
                Canvas canvas = GetComponentInParent<Canvas>();
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                Vector3 scaledOffset = (Vector3)offset / scale;

                // 타겟의 중앙 위치 (pivot에 관계없이)
                Vector3 targetCenter = targetUI.TransformPoint(targetUI.rect.center);
                m_boxRect.position = targetCenter + scaledOffset;

                // 원래 너비로 복원
                m_boxRect.sizeDelta = new Vector2(m_originalWidth, m_boxRect.sizeDelta.y);
            }
            else
            {
                // 스토리 모드: 화면 중앙, 넓은 너비
                m_boxRect.anchoredPosition = m_storyModePosition + offset;
                m_boxRect.sizeDelta = new Vector2(m_storyModeWidth, m_boxRect.sizeDelta.y);
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
