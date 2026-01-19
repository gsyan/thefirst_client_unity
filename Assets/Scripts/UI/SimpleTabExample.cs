using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 간단한 탭 시스템 사용 예제
/// Unity Inspector에서 쉽게 설정할 수 있습니다.
/// </summary>
public class SimpleTabExample : MonoBehaviour
{
    [Header("Quick Tab Setup")]
    public SimpleTab[] simpleTabs;
    
    private TabSystem tabSystem;
    private int currentTab = 0;

    private void Start()
    {
        SetupSimpleTabs();
    }

    private void SetupSimpleTabs()
    {
        // TabSystem 컴포넌트 추가
        tabSystem = gameObject.GetComponent<TabSystem>();
        if (tabSystem == null)
        {
            tabSystem = gameObject.AddComponent<TabSystem>();
        }

        // SimpleTab 배열을 TabData로 변환
        for (int i = 0; i < simpleTabs.Length; i++)
        {
            var simpleTab = simpleTabs[i];
            if (simpleTab.button != null && simpleTab.panel != null)
            {
                var tabData = new TabData
                {
                    tabButton = simpleTab.button,
                    tabPanel = simpleTab.panel,
                    tabName = simpleTab.name,
                    activeColor = Color.cyan,
                    inactiveColor = Color.gray
                };
                
                tabSystem.AddTab(tabData);
            }
        }

        // 첫 번째 탭 활성화
        if (simpleTabs.Length > 0)
        {
            tabSystem.SwitchToTab(0);
        }
    }

    // 외부에서 호출할 수 있는 메서드
    public void NextTab()
    {
        currentTab = (currentTab + 1) % simpleTabs.Length;
        tabSystem.SwitchToTab(currentTab);
    }

    public void PreviousTab()
    {
        currentTab = (currentTab - 1 + simpleTabs.Length) % simpleTabs.Length;
        tabSystem.SwitchToTab(currentTab);
    }
}