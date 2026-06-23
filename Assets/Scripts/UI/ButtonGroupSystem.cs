using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 라디오 버튼 그룹: 하나만 선택 가능, 선택 상태 색상 관리 / allowDeselect 시 재클릭으로 해제 가능
[System.Serializable]
public class ButtonGroupItem
{
    public Button button;
    public Graphic[] childGraphics; // 자식 Image, TMP_Text 등 색상 연동 대상
    [System.NonSerialized] public Color activeColor = Color.white;
    [System.NonSerialized] public Color inactiveColor = Color.gray;
    [System.NonSerialized] public System.Action onSelected;
    [System.NonSerialized] public System.Action onDeselected;
}

public class ButtonGroupSystem : MonoBehaviour
{
    [Header("Button Configuration")]
    public List<ButtonGroupItem> items = new List<ButtonGroupItem>();
    public int defaultIndex = 0;
    // true면 현재 선택된 버튼 재클릭 시 해제 (-1 상태)
    public bool allowDeselect = false;

    private int currentIndex = -1;
    private bool initialized = false;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized) return;

        Color colorActive   = CommonUtility.PaletteColor("GeneralBright1");
        Color colorInactive = CommonUtility.PaletteColor("GeneralDark1");

        for (int i = 0; i < items.Count; i++)
        {
            items[i].activeColor   = colorActive;
            items[i].inactiveColor = colorInactive;

            if (items[i].button != null)
            {
                int idx = i;
                items[i].button.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); Select(idx); });
            }
        }

        initialized = true;

        // 전체 버튼에 inactiveColor 초기 적용 후 defaultIndex만 activeColor로 덮어씀
        for (int i = 0; i < items.Count; i++)
            ApplyColor(items[i], items[i].inactiveColor);

        if (defaultIndex >= 0)
            Select(defaultIndex);
    }

    public void Select(int index)
    {
        if (!initialized) return;
        if (index < 0 || index >= items.Count) return;

        // 같은 탭 재클릭 → 해제
        if (index == currentIndex && allowDeselect == true)
        {
            Deselect();
            return;
        }
        if (index == currentIndex) return;

        // 이전 버튼 비활성화
        if (currentIndex >= 0)
        {
            var prev = items[currentIndex];
            ApplyColor(prev, prev.inactiveColor);
            prev.onDeselected?.Invoke();
        }

        // 새 버튼 활성화
        currentIndex = index;
        var cur = items[currentIndex];
        ApplyColor(cur, cur.activeColor);
        cur.onSelected?.Invoke();
    }

    // 선택 해제 (-1 상태)
    public void Deselect()
    {
        if (currentIndex < 0) return;
        var prev = items[currentIndex];
        ApplyColor(prev, prev.inactiveColor);
        currentIndex = -1; // 콜백 전에 -1 설정 (TabSystem이 GetCurrentIndex로 확인하기 때문)
        prev.onDeselected?.Invoke();
    }

    public int GetCurrentIndex() => currentIndex;

    private void ApplyColor(ButtonGroupItem item, Color color)
    {
        if (item.button != null)
        {
            var colors = item.button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.1f;
            colors.pressedColor = color * 0.8f;
            colors.selectedColor = color;
            item.button.colors = colors;
        }

        if (item.childGraphics != null)
        {
            for (int i = 0; i < item.childGraphics.Length; i++)
            {
                if (item.childGraphics[i] != null)
                    item.childGraphics[i].color = color;
            }
        }
    }
}
