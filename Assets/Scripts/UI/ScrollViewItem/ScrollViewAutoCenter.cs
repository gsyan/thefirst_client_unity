using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 지정 아이템을 스크롤뷰 중앙에 배치, 유저 스크롤 후 5초 뒤 자동 복귀
public class ScrollViewAutoCenter : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    private ScrollRect m_scrollRect;
    private RectTransform m_targetChild;
    private float m_targetNormalizedPos;
    private bool m_hasTarget;
    private bool m_isDragging;
    private float m_lastUserInputTime;
    private bool m_isReturning;

    private const float RETURN_DELAY = 5f;
    private const float LERP_SPEED = 5f;

    private void Awake()
    {
        m_scrollRect = GetComponent<ScrollRect>();
    }

    // 대상 아이템을 중앙에 즉시 배치
    public void CenterOnChild(RectTransform child)
    {
        if (m_scrollRect == null || child == null)
        {
            m_hasTarget = false;
            return;
        }

        m_targetChild = child;
        m_isReturning = false;
        m_lastUserInputTime = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_scrollRect.content);
        Canvas.ForceUpdateCanvases();

        if (!IsContentLargerThanViewport())
        {
            m_hasTarget = false;
            return;
        }

        m_targetNormalizedPos = CalculateNormalizedPos(child);
        m_hasTarget = true;
        SetNormalizedPos(m_targetNormalizedPos);
    }

    // 타겟 해제
    public void ClearTarget()
    {
        m_hasTarget = false;
        m_isReturning = false;
        m_targetChild = null;
    }

    private bool IsContentLargerThanViewport()
    {
        RectTransform content = m_scrollRect.content;
        RectTransform viewport = GetViewport();

        return m_scrollRect.horizontal
            ? content.rect.width > viewport.rect.width
            : content.rect.height > viewport.rect.height;
    }

    private RectTransform GetViewport()
    {
        return m_scrollRect.viewport != null ? m_scrollRect.viewport : (RectTransform)transform;
    }

    private float CalculateNormalizedPos(RectTransform child)
    {
        RectTransform content = m_scrollRect.content;
        RectTransform viewport = GetViewport();
        Vector2 childLocal = (Vector2)content.InverseTransformPoint(child.position);

        if (m_scrollRect.horizontal)
        {
            float scrollable = content.rect.width - viewport.rect.width;
            if (scrollable <= 0f) return 0f;

            float childFromLeft = childLocal.x - content.rect.xMin;
            float offset = childFromLeft - viewport.rect.width * 0.5f;
            return Mathf.Clamp01(offset / scrollable);
        }
        else
        {
            float scrollable = content.rect.height - viewport.rect.height;
            if (scrollable <= 0f) return 1f;

            float childFromTop = content.rect.yMax - childLocal.y;
            float offset = childFromTop - viewport.rect.height * 0.5f;
            return 1f - Mathf.Clamp01(offset / scrollable);
        }
    }

    private void SetNormalizedPos(float pos)
    {
        if (m_scrollRect.horizontal)
            m_scrollRect.horizontalNormalizedPosition = pos;
        else
            m_scrollRect.verticalNormalizedPosition = pos;
    }

    private float GetNormalizedPos()
    {
        return m_scrollRect.horizontal
            ? m_scrollRect.horizontalNormalizedPosition
            : m_scrollRect.verticalNormalizedPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        m_isDragging = true;
        m_isReturning = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        m_isDragging = false;
        if (m_hasTarget)
            m_lastUserInputTime = Time.unscaledTime;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (m_hasTarget)
        {
            m_lastUserInputTime = Time.unscaledTime;
            m_isReturning = false;
        }
    }

    private void Update()
    {
        if (!m_hasTarget || m_isDragging || m_scrollRect == null) return;

        // 유저 입력 후 RETURN_DELAY 경과 시 복귀 시작
        if (m_lastUserInputTime > 0f && !m_isReturning)
        {
            if (Time.unscaledTime - m_lastUserInputTime >= RETURN_DELAY)
            {
                m_isReturning = true;
                m_targetNormalizedPos = CalculateNormalizedPos(m_targetChild);
            }
        }

        if (!m_isReturning) return;

        float current = GetNormalizedPos();
        float diff = m_targetNormalizedPos - current;

        if (Mathf.Abs(diff) < 0.001f)
        {
            SetNormalizedPos(m_targetNormalizedPos);
            m_isReturning = false;
            m_lastUserInputTime = 0f;
            return;
        }

        SetNormalizedPos(Mathf.Lerp(current, m_targetNormalizedPos, Time.unscaledDeltaTime * LERP_SPEED));
    }
}
