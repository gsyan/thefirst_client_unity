using UnityEngine;

// 튜토리얼 클릭 유도 화살표 — 대상 UI 주변 여유 화면 공간을 검사해 배치 위치(위→우→좌→아래 우선순위)를 자동 결정
public class TutorialArrow : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private RectTransform m_arrowRect;
    private float m_bounceSpeed = 4f;
    private float m_bounceAmount = 15f;
    private float m_offsetDistance = 30f;

    private Vector2 m_basePosition;
    private bool m_isAnimating;
    private EArrowDirection m_currentDirection; // 화살표가 대상 UI 기준 어느 쪽에 배치됐는지
    private Canvas m_canvas;

    private void Awake()
    {
        if (m_arrowRect == null)
            m_arrowRect = GetComponent<RectTransform>();
        m_canvas = GetComponentInParent<Canvas>();
    }

    // 화살표 표시 — forcedDirection이 Auto면 배치 방향 자동 계산, 그 외는 강제 지정된 방향 사용
    public void Show(RectTransform target, EArrowDirection forcedDirection = EArrowDirection.Auto)
    {
        gameObject.SetActive(true);

        EArrowDirection direction = forcedDirection == EArrowDirection.Auto ? DetermineDirection(target) : forcedDirection;
        m_currentDirection = direction;

        // 배치 위치에 따른 회전 (화살표가 항상 대상 UI를 가리키도록)
        float rotation = direction switch
        {
            EArrowDirection.Up => 0f,     // 대상 UI 위에 배치 → 아래(대상 UI)를 가리킴
            EArrowDirection.Down => 180f, // 대상 UI 아래에 배치 → 위(대상 UI)를 가리킴
            EArrowDirection.Left => 90f,  // 대상 UI 좌측에 배치 → 우측(대상 UI)을 가리킴
            EArrowDirection.Right => -90f,// 대상 UI 우측에 배치 → 좌측(대상 UI)을 가리킴
            _ => 0f
        };
        m_arrowRect.localRotation = Quaternion.Euler(0, 0, rotation);

        // Canvas 스케일 기준 여백 계산 (대상 UI 바깥쪽에 이 여백만큼 추가로 띄움)
        float scale = m_canvas != null ? m_canvas.scaleFactor : 1f;
        float margin = m_offsetDistance / scale;

        // 대상 UI의 실제 상하/좌우 크기(half extent) — 버튼 크기가 제각각이므로 고정 거리만으로는 부족/파묻힘 발생
        Vector3[] targetCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);
        float targetHalfWidth = (targetCorners[2].x - targetCorners[0].x) * 0.5f;
        float targetHalfHeight = (targetCorners[2].y - targetCorners[0].y) * 0.5f;

        Vector3 dirOffset = direction switch
        {
            EArrowDirection.Up => Vector3.up * (targetHalfHeight + margin),
            EArrowDirection.Down => Vector3.down * (targetHalfHeight + margin),
            EArrowDirection.Left => Vector3.left * (targetHalfWidth + margin),
            EArrowDirection.Right => Vector3.right * (targetHalfWidth + margin),
            _ => Vector3.zero
        };

        // 대상 UI의 중앙 위치 (pivot에 관계없이)
        Vector3 targetCenter = target.TransformPoint(target.rect.center);
        m_arrowRect.position = targetCenter + dirOffset;
        m_basePosition = m_arrowRect.anchoredPosition;
        m_isAnimating = true;
    }

    // 대상 UI 주변 여유 공간 검사 — 위 → 우 → 좌 → 아래 순으로 우선 배치, 전부 부족하면 아래로 최종 배치
    // 대상 UI 자체의 상하좌우 크기(half extent)까지 감안해야 큰 버튼에서도 화면 밖으로 안 나감
    private EArrowDirection DetermineDirection(RectTransform target)
    {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Camera canvasCamera = m_canvas != null ? m_canvas.worldCamera : null;
        Vector2 screenA = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
        Vector2 screenB = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);

        float left = Mathf.Min(screenA.x, screenB.x);
        float right = Mathf.Max(screenA.x, screenB.x);
        float top = Mathf.Max(screenA.y, screenB.y);
        float halfWidth = (right - left) * 0.5f;
        float halfHeight = (top - Mathf.Min(screenA.y, screenB.y)) * 0.5f;

        float arrowSize = Mathf.Max(m_arrowRect.rect.width, m_arrowRect.rect.height);
        float requiredSpaceVertical = m_offsetDistance + halfHeight + arrowSize * 0.5f;
        float requiredSpaceHorizontal = m_offsetDistance + halfWidth + arrowSize * 0.5f;

        float spaceAbove = Screen.height - top;
        float spaceRight = Screen.width - right;
        float spaceLeft = left;

        if (spaceAbove >= requiredSpaceVertical) return EArrowDirection.Up;
        if (spaceRight >= requiredSpaceHorizontal) return EArrowDirection.Right;
        if (spaceLeft >= requiredSpaceHorizontal) return EArrowDirection.Left;
        return EArrowDirection.Down;
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

        // 바운스 애니메이션 (배치 축에 따라 방향 결정)
        float bounce = Mathf.Sin(Time.time * m_bounceSpeed) * m_bounceAmount;
        bool isHorizontal = m_currentDirection == EArrowDirection.Left || m_currentDirection == EArrowDirection.Right;
        Vector2 bounceOffset = isHorizontal ? Vector2.right * bounce : Vector2.up * bounce;

        m_arrowRect.anchoredPosition = m_basePosition + bounceOffset;
    }
}
