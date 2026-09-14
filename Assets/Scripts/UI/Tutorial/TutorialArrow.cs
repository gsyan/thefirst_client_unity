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

    // RectTransform이 없는 3D 월드 대상(탐사 그리드 셀 등)을 가리킬 때만 값이 채워짐 — 카메라 회전/이동에 맞춰 매 프레임 재투영해야 함
    private Vector3? m_worldTargetPos;
    private Camera m_worldCamera;
    private Vector3 m_worldDirOffset;

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
        m_worldTargetPos = null; // 일반 RectTransform 타겟으로 전환 — 3D 월드 추적 모드 해제

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

    // RectTransform이 없는 3D 월드 좌표(탐사 그리드 셀 등)를 직접 가리킴 — 딤 마스크/텍스트박스는 관여하지 않고 화살표만 표시.
    // 대상 크기를 모르므로 half-extent 계산 없이 고정 여백(margin)만 적용, 방향도 자동판정 없이 항상 강제 지정값을 씀
    public void ShowAtWorldPosition(Vector3 worldPos, Camera worldCamera, EArrowDirection direction)
    {
        gameObject.SetActive(true);

        m_currentDirection = direction;
        m_worldTargetPos = worldPos;
        m_worldCamera = worldCamera != null ? worldCamera : Camera.main;

        float rotation = 180f;
        if (direction == EArrowDirection.Up) rotation = 0f;
        else if (direction == EArrowDirection.Left) rotation = 90f;
        else if (direction == EArrowDirection.Right) rotation = -90f;
        m_arrowRect.localRotation = Quaternion.Euler(0, 0, rotation);

        float scale = m_canvas != null ? m_canvas.scaleFactor : 1f;
        float margin = m_offsetDistance / scale;

        Vector3 dirOffset = Vector3.down * margin;
        if (direction == EArrowDirection.Up) dirOffset = Vector3.up * margin;
        else if (direction == EArrowDirection.Left) dirOffset = Vector3.left * margin;
        else if (direction == EArrowDirection.Right) dirOffset = Vector3.right * margin;
        m_worldDirOffset = dirOffset;

        UpdateWorldTargetPosition();
        m_isAnimating = true;
    }

    // 3D 월드 좌표 → 스크린 좌표 재투영 — 카메라가 회전/이동해도 화살표가 대상 셀을 계속 가리키도록 매 프레임 호출됨
    private void UpdateWorldTargetPosition()
    {
        if (m_worldTargetPos == null || m_worldCamera == null) return;

        Vector3 screenPos = m_worldCamera.WorldToScreenPoint(m_worldTargetPos.Value);
        m_arrowRect.position = new Vector3(screenPos.x, screenPos.y, 0f) + m_worldDirOffset;
        m_basePosition = m_arrowRect.anchoredPosition;
    }

    // 화살표 숨기기
    public void Hide()
    {
        m_isAnimating = false;
        m_worldTargetPos = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!m_isAnimating) return;

        if (m_worldTargetPos != null)
            UpdateWorldTargetPosition();

        // 바운스 애니메이션 (배치 축에 따라 방향 결정)
        float bounce = Mathf.Sin(Time.time * m_bounceSpeed) * m_bounceAmount;
        bool isHorizontal = m_currentDirection == EArrowDirection.Left || m_currentDirection == EArrowDirection.Right;
        Vector2 bounceOffset = isHorizontal ? Vector2.right * bounce : Vector2.up * bounce;

        m_arrowRect.anchoredPosition = m_basePosition + bounceOffset;
    }
}
