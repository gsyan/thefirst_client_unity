using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 라디오 버튼 그룹: 하나만 선택 가능, 선택 상태 색상 관리 / allowDeselect 시 재클릭으로 해제 가능
[System.Serializable]
public class ButtonGroupItem
{
    public Button button;
    [System.NonSerialized] public Color originalNormalColor; // Initialize 시점에 캐시한 버튼 본연의 Normal Color(비선택 상태로 되돌릴 기준값)
    [System.NonSerialized] public System.Action onSelected;
    [System.NonSerialized] public System.Action onDeselected;

    // button 자식 중 "UsingImage" 이름을 가진 오브젝트 — 있으면 선택 상태를 색상 대신 이 오브젝트의 활성화로 표시
    [System.NonSerialized] public GameObject usingImage;
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

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].button != null)
            {
                int idx = i;
                items[i].button.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); Select(idx); });

                Transform usingImage = items[i].button.transform.Find("UsingImage");
                items[i].usingImage = usingImage != null ? usingImage.gameObject : null;
                items[i].originalNormalColor = items[i].button.colors.normalColor;
            }
        }

        initialized = true;

        // 전체 버튼을 비선택 상태로 초기화한 뒤 defaultIndex만 선택 상태로 덮어씀
        for (int i = 0; i < items.Count; i++)
            ApplyState(items[i], false);

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
            ApplyState(prev, false);
            prev.onDeselected?.Invoke();
        }

        // 새 버튼 활성화
        currentIndex = index;
        var cur = items[currentIndex];
        ApplyState(cur, true);
        cur.onSelected?.Invoke();
    }

    // 선택 해제 (-1 상태)
    public void Deselect()
    {
        if (currentIndex < 0) return;
        var prev = items[currentIndex];
        ApplyState(prev, false);
        currentIndex = -1; // 콜백 전에 -1 설정 (TabSystem이 GetCurrentIndex로 확인하기 때문)
        prev.onDeselected?.Invoke();
    }

    public int GetCurrentIndex() => currentIndex;

    // 현재 선택된 버튼의 색을 다시 한번 덮어씀 — ApplyState가 더 이상 EventSystem 포커스에 기대지 않아 꼭 필요하진 않지만,
    // 패널 재오픈 시(TabSystem.OnEnable) 안전하게 재확인하는 용도로 유지
    public void ReapplySelectedVisual()
    {
        if (currentIndex < 0 || currentIndex >= items.Count) return;
        ApplyState(items[currentIndex], true);
    }

    // usingImage가 있으면 색상 대신 그 오브젝트의 활성화로 선택 상태를 표시.
    // 없으면 Inspector에 직접 칠해둔 Button.colors(Normal/Selected)를 그대로 쓰되, Unity의 EventSystem.currentSelectedGameObject
    // 기반 Selected 상태에는 기대지 않음 — 다른 UI를 클릭하면 전역 포커스가 넘어가며 즉시 꺼져버리기 때문.
    // 대신 선택된 버튼의 Normal Color 자체를 Selected Color로 바꿔치기(평상시 색을 선택색으로 고정) — 포커스와 무관하게 유지됨
    private void ApplyState(ButtonGroupItem item, bool isSelected)
    {
        if (item.usingImage != null)
        {
            item.usingImage.SetActive(isSelected);
            return;
        }

        if (item.button == null) return;

        ColorBlock colors = item.button.colors;
        colors.normalColor = isSelected == true ? colors.selectedColor : item.originalNormalColor;
        item.button.colors = colors;
    }
}
