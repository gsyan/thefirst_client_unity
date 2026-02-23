// 대용량 아이템을 효율적으로 표시하는 가상 스크롤뷰
// Content 전체 높이를 유지하면서 실제 아이템은 viewport 채울 만큼만 생성/재활용
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InfiniteScrollView : MonoBehaviour
{
    [SerializeField] private ScrollRect m_scrollRect;
    [SerializeField] private float m_itemHeight = 80f;
    [SerializeField] private int m_bufferCount = 2; // viewport 위아래 추가 유지 개수

    // (데이터 인덱스, 아이템 GameObject) → 데이터 적용
    public Action<int, GameObject> onItemBind;
    // (시작 인덱스, 개수) → 해당 범위 서버 데이터 요청 필요 시 발동
    public Action<int, int> onNeedData;

    private int m_totalCount;
    private readonly List<RectTransform> m_itemPool = new List<RectTransform>();
    private int m_topDataIndex = int.MinValue;
    private int m_poolSize;
    private bool m_initialized;

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

        content.sizeDelta = new Vector2(content.sizeDelta.x, totalCount * m_itemHeight);
        content.anchoredPosition = Vector2.zero;

        float viewportHeight = m_scrollRect.viewport.rect.height;
        if (viewportHeight <= 0f)
        {
            Canvas.ForceUpdateCanvases();
            viewportHeight = m_scrollRect.viewport.rect.height;
        }

        int visibleCount = Mathf.CeilToInt(viewportHeight / m_itemHeight) + 1;
        m_poolSize = visibleCount + m_bufferCount * 2;

        // 기존 풀 정리 후 재생성
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
            // 상단 고정 앵커로 Y 좌표 = -(dataIndex * itemHeight)
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, m_itemHeight);
            obj.SetActive(false);
            m_itemPool.Add(rt);
        }

        m_scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        m_scrollRect.onValueChanged.AddListener(OnScrollChanged);

        m_initialized = true;
        m_topDataIndex = int.MinValue;
        RefreshView();
    }

    // 전체 개수 변경 시 (서버에서 totalCount 갱신됐을 때)
    public void UpdateTotalCount(int totalCount)
    {
        if (m_initialized == false) return;
        m_totalCount = totalCount;
        m_scrollRect.content.sizeDelta = new Vector2(
            m_scrollRect.content.sizeDelta.x,
            totalCount * m_itemHeight
        );
        m_topDataIndex = int.MinValue;
        RefreshView();
    }

    // dataIndex번 아이템이 화면 상단에 오도록 이동
    public void JumpToIndex(int dataIndex)
    {
        if (m_initialized == false || m_totalCount <= 0) return;
        dataIndex = Mathf.Clamp(dataIndex, 0, m_totalCount - 1);

        float contentHeight = m_scrollRect.content.sizeDelta.y;
        float viewportHeight = m_scrollRect.viewport.rect.height;
        float maxScrollY = Mathf.Max(0f, contentHeight - viewportHeight);
        float targetY = Mathf.Clamp(dataIndex * m_itemHeight, 0f, maxScrollY);

        m_scrollRect.content.anchoredPosition = new Vector2(0f, targetY);
        m_topDataIndex = int.MinValue;
        RefreshView();
    }

    // 데이터가 새로 들어왔을 때 현재 화면 강제 갱신
    public void RefreshVisible()
    {
        if (m_initialized == false) return;
        m_topDataIndex = int.MinValue;
        RefreshView();
    }

    private void OnScrollChanged(Vector2 _)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        if (m_initialized == false || m_totalCount == 0) return;

        float scrollY = m_scrollRect.content.anchoredPosition.y;
        int newTop = Mathf.Max(0, Mathf.FloorToInt(scrollY / m_itemHeight) - m_bufferCount);

        if (newTop == m_topDataIndex) return;
        m_topDataIndex = newTop;

        for (int i = 0; i < m_itemPool.Count; i++)
        {
            int dataIndex = m_topDataIndex + i;
            RectTransform rt = m_itemPool[i];

            if (dataIndex >= m_totalCount)
            {
                rt.gameObject.SetActive(false);
                continue;
            }

            rt.gameObject.SetActive(true);
            rt.anchoredPosition = new Vector2(0f, -dataIndex * m_itemHeight);
            onItemBind?.Invoke(dataIndex, rt.gameObject);
        }

        // 현재 보이는 범위 중 데이터가 없는 페이지 요청 유도
        int visibleEnd = Mathf.Min(m_totalCount - 1, m_topDataIndex + m_poolSize - 1);
        if (visibleEnd >= m_topDataIndex)
            onNeedData?.Invoke(m_topDataIndex, visibleEnd - m_topDataIndex + 1);
    }

    private void OnDestroy()
    {
        if (m_scrollRect != null)
            m_scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
    }
}
