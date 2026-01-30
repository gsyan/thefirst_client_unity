using UnityEngine;

// 튜토리얼 클릭 유도 화살표
public class TutorialArrow : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private RectTransform m_arrowRect;
    [SerializeField] private float m_bounceSpeed = 4f;
    [SerializeField] private float m_bounceAmount = 15f;
    [SerializeField] private float m_offsetDistance = 60f;

    private Vector2 m_basePosition;
    private bool m_isAnimating;
    private EArrowDirection m_currentDirection;

    // 화살표 표시
    public void Show(RectTransform target, EArrowDirection direction)
    {
        if (m_arrowRect == null)
            m_arrowRect = GetComponent<RectTransform>();

        gameObject.SetActive(true);
        m_currentDirection = direction;

        // 방향에 따른 회전
        float rotation = direction switch
        {
            EArrowDirection.Up => 180f,
            EArrowDirection.Down => 0f,
            EArrowDirection.Left => -90f,
            EArrowDirection.Right => 90f,
            _ => 0f
        };
        m_arrowRect.localRotation = Quaternion.Euler(0, 0, rotation);

        // Canvas 스케일 기준 오프셋 계산
        Canvas canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float scaledOffset = m_offsetDistance / scale;
        //scaledOffset = 1;

        Vector3 dirOffset = direction switch
        {
            EArrowDirection.Up => Vector3.up * scaledOffset,
            EArrowDirection.Down => Vector3.down * scaledOffset,
            EArrowDirection.Left => Vector3.left * scaledOffset,
            EArrowDirection.Right => Vector3.right * scaledOffset,
            _ => Vector3.zero
        };

        // 타겟의 중앙 위치 (pivot에 관계없이)
        Vector3 targetCenter = target.TransformPoint(target.rect.center);
        m_arrowRect.position = targetCenter + dirOffset;
        m_basePosition = m_arrowRect.anchoredPosition;
        m_isAnimating = true;
    }

    // 화살표 숨기기
    public void Hide()
    {
        m_isAnimating = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!m_isAnimating) return;

        //바운스 애니메이션 (방향에 따라)
        float bounce = Mathf.Sin(Time.time * m_bounceSpeed) * m_bounceAmount;

        Vector2 bounceOffset = m_currentDirection switch
        {
            EArrowDirection.Up => Vector2.down * bounce,
            EArrowDirection.Down => Vector2.up * bounce,
            EArrowDirection.Left => Vector2.right * bounce,
            EArrowDirection.Right => Vector2.left * bounce,
            _ => Vector2.zero
        };

        m_arrowRect.anchoredPosition = m_basePosition + bounceOffset;
    }
}
