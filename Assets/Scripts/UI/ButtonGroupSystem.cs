using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 라디오 버튼 그룹: 하나만 선택 가능, 선택 상태 색상 관리
[System.Serializable]
public class ButtonGroupItem
{
    public Button button;
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;
    [System.NonSerialized] public System.Action onSelected;
    [System.NonSerialized] public System.Action onDeselected;
}

public class ButtonGroupSystem : MonoBehaviour
{
    [Header("Button Configuration")]
    public List<ButtonGroupItem> items = new List<ButtonGroupItem>();
    public int defaultIndex = 0;

    // 인스펙터에서 + 버튼으로 추가 시 색상 기본값 자동 적용
    private void OnValidate()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.activeColor.a == 0f && item.activeColor.r == 0f && item.activeColor.g == 0f && item.activeColor.b == 0f)
                item.activeColor = new Color(1f, 0.8f, 0.2f, 1f);
            if (item.inactiveColor.a == 0f && item.inactiveColor.r == 0f && item.inactiveColor.g == 0f && item.inactiveColor.b == 0f)
                item.inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        }
    }

    private int currentIndex = -1;
    private bool initialized = false;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].button != null)
            {
                int idx = i;
                items[i].button.onClick.AddListener(() => Select(idx));
            }
        }

        initialized = true;
        Select(defaultIndex);
    }

    public void Select(int index)
    {
        if (!initialized) return;
        if (index < 0 || index >= items.Count) return;
        if (index == currentIndex) return;

        // 이전 버튼 비활성화
        if (currentIndex >= 0)
        {
            var prev = items[currentIndex];
            ApplyColor(prev.button, prev.inactiveColor);
            prev.onDeselected?.Invoke();
        }

        // 새 버튼 활성화
        currentIndex = index;
        var cur = items[currentIndex];
        ApplyColor(cur.button, cur.activeColor);
        cur.onSelected?.Invoke();
    }

    public int GetCurrentIndex() => currentIndex;

    private void ApplyColor(Button button, Color color)
    {
        if (button == null) return;
        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.1f;
        colors.pressedColor = color * 0.8f;
        colors.selectedColor = color;
        button.colors = colors;
    }
}
