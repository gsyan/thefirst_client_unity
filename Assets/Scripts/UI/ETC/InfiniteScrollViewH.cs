// 수평 가상 스크롤뷰 — 뷰포트 중앙 아이템이 선택된 상태로 표시 (존 탭 전용)
// Content 전체 너비를 유지하면서 실제 아이템은 viewport 채울 만큼만 생성/재활용
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InfiniteScrollViewH : MonoBehaviour
{
    [SerializeField] private ScrollRect m_scrollRect;
    [SerializeField] private float m_itemWidth = 80f;
    [SerializeField] private int m_bufferCount = 2;
    [SerializeField] private float m_spacing = 0f;

    // (데이터 인덱스, 아이템 GameObject) → 데이터 적용
    public Action<int, GameObject> onItemBind;
    // 뷰포트 중앙에 있는 데이터 인덱스가 변경될 때 발동
    public Action<int> onCenterIndexChanged;

    private int m_totalCount;
    private readonly List<RectTransform> m_itemPool = new List<RectTransform>();
    private int m_leftDataIndex = int.MinValue;
    private int m_poolSize;
    private bool m_initialized;
    // 첫/마지막 아이템을 뷰포트 중앙에 놓을 수 있도록 좌우에 추가하는 패딩
    private float m_sidePadding;
    private int m_lastCenterIndex = -1;

    private void Awake()
    {
        if (m_scrollRect == null)
            m_scrollRect = GetComponent<ScrollRect>();
    }

    // totalCount: 전체 데이터 개수, itemPrefab: 재활용할 아이템 프리팹
    public void Initialize(int totalCount, GameObject itemPrefab)
    {
        m_totalCount = totalCount;
        RectTransform content = m_scrollRect.content;

        Canvas.ForceUpdateCanvases();
        float viewportWidth = m_scrollRect.viewport.rect.width;
        m_sidePadding = Mathf.Max(0f, (viewportWidth - m_itemWidth) * 0.5f);

        float itemStep    = m_itemWidth + m_spacing;
        float totalWidth  = m_sidePadding * 2f + totalCount * itemStep - m_spacing;
        content.sizeDelta = new Vector2(totalWidth, content.sizeDelta.y);
        content.anchoredPosition = Vector2.zero;

        int visibleCount = Mathf.CeilToInt(viewportWidth / itemStep) + 1;
        m_poolSize = visibleCount + m_bufferCount * 2;

        for (int i = 0; i < m_itemPool.Count; i++)
        {
            if (m_itemPool[i] != null)
                Destroy(m_itemPool[i].gameObject);
        }
        m_itemPool.Clear();

        for (int i = 0; i < m_poolSize; i++)
        {
            GameObject obj = Instantiate(itemPrefab, content);
            RectTransform rt = obj.GetComponent<RectTransform>();
            // 좌측 고정 앵커, 수직 스트레치
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(m_itemWidth, 0f);
            obj.SetActive(false);
            m_itemPool.Add(rt);
        }

        m_scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        m_scrollRect.onValueChanged.AddListener(OnScrollChanged);

        m_initialized     = true;
        m_leftDataIndex   = int.MinValue;
        m_lastCenterIndex = -1;
        RefreshView();
    }

    // dataIndex 아이템이 뷰포트 중앙에 오도록 즉시 이동
    public void ScrollToCenter(int dataIndex)
    {
        if (m_initialized == false || m_totalCount <= 0) return;
        dataIndex = Mathf.Clamp(dataIndex, 0, m_totalCount - 1);

        float itemStep      = m_itemWidth + m_spacing;
        float viewportWidth = m_scrollRect.viewport.rect.width;
        float contentWidth  = m_scrollRect.content.sizeDelta.x;
        float maxScrollX    = Mathf.Max(0f, contentWidth - viewportWidth);
        float itemCenterX   = m_sidePadding + dataIndex * itemStep + m_itemWidth * 0.5f;
        float targetScrollX = Mathf.Clamp(itemCenterX - viewportWidth * 0.5f, 0f, maxScrollX);

        m_scrollRect.content.anchoredPosition = new Vector2(-targetScrollX, 0f);
        m_leftDataIndex = int.MinValue;
        RefreshView();
    }

    // 현재 뷰포트 중앙에 가장 가까운 데이터 인덱스 반환
    public int GetCenterDataIndex()
    {
        if (m_initialized == false || m_totalCount == 0) return 0;

        float itemStep      = m_itemWidth + m_spacing;
        float scrollX       = -m_scrollRect.content.anchoredPosition.x;
        float viewportWidth = m_scrollRect.viewport.rect.width;
        float centerX       = scrollX + viewportWidth * 0.5f;
        int idx = Mathf.RoundToInt((centerX - m_sidePadding - m_itemWidth * 0.5f) / itemStep);
        return Mathf.Clamp(idx, 0, m_totalCount - 1);
    }

    // 선택 상태 변경 등 외부에서 강제 갱신할 때 호출
    public void RefreshVisible()
    {
        if (m_initialized == false) return;
        m_leftDataIndex = int.MinValue;
        RefreshView();
    }

    private void OnScrollChanged(Vector2 _)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        if (m_initialized == false || m_totalCount == 0) return;

        float itemStep = m_itemWidth + m_spacing;
        float scrollX  = -m_scrollRect.content.anchoredPosition.x;
        int newLeft    = Mathf.Max(0, Mathf.FloorToInt((scrollX - m_sidePadding) / itemStep) - m_bufferCount);

        if (newLeft != m_leftDataIndex)
        {
            m_leftDataIndex = newLeft;
            for (int i = 0; i < m_itemPool.Count; i++)
            {
                int dataIndex = m_leftDataIndex + i;
                RectTransform rt = m_itemPool[i];
                if (dataIndex >= m_totalCount)
                {
                    rt.gameObject.SetActive(false);
                    continue;
                }
                rt.gameObject.SetActive(true);
                rt.anchoredPosition = new Vector2(m_sidePadding + dataIndex * itemStep, 0f);
                onItemBind?.Invoke(dataIndex, rt.gameObject);
            }
        }

        int centerIndex = GetCenterDataIndex();
        if (centerIndex != m_lastCenterIndex)
        {
            m_lastCenterIndex = centerIndex;
            onCenterIndexChanged?.Invoke(centerIndex);
        }
    }

    private void OnDestroy()
    {
        if (m_scrollRect != null)
            m_scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
    }
}
