using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 특정 패널을 여는 간단한 컴포넌트
/// Inspector에서 쉽게 설정 가능
/// </summary>
public class PanelButton : MonoBehaviour
{
    [Header("Panel Control")]
    public string targetPanelName;
    public PanelAction actionType = PanelAction.Show;
    
    [Header("Auto Setup")]
    public bool findUISpaceAutomatically = true;
    
    private UISpace uiSpace;
    private Button button;
    
    public enum PanelAction
    {
        Show,           // 패널 표시
        Hide,           // 현재 패널 숨기고 메인으로
        Toggle,         // 토글 (열려있으면 닫고, 닫혀있으면 열기)
        ShowMain        // 메인 패널로 돌아가기
    }
    
    private void Start()
    {
        SetupButton();
        FindUISpace();
    }
    
    private void SetupButton()
    {
        button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"PanelButton on {gameObject.name} requires a Button component!");
            return;
        }
        
        button.onClick.AddListener(OnButtonClick);
    }
    
    private void FindUISpace()
    {
        if (findUISpaceAutomatically)
        {
            uiSpace = FindFirstObjectByType<UISpace>();
            if (uiSpace == null)
            {
                Debug.LogWarning("UISpace not found! PanelButton will not work.");
            }
        }
    }
    
    private void OnButtonClick()
    {
        if (uiSpace == null)
        {
            Debug.LogWarning("UISpace is not assigned to PanelButton!");
            return;
        }
        
        switch (actionType)
        {
            case PanelAction.Show:
                if (!string.IsNullOrEmpty(targetPanelName))
                {
                    uiSpace.ShowPanel(targetPanelName);
                }
                break;
                
            case PanelAction.Hide:
                uiSpace.HideCurrentPanel();
                break;
                
            case PanelAction.Toggle:
                if (!string.IsNullOrEmpty(targetPanelName))
                {
                    uiSpace.TogglePanel(targetPanelName);
                }
                break;
                
            case PanelAction.ShowMain:
                uiSpace.ShowMainPanel();
                break;
        }
    }
    
    // Inspector에서 쉽게 설정할 수 있도록 도우미 메서드들
    public void SetTargetPanel(string panelName)
    {
        targetPanelName = panelName;
    }
    
    public void SetUISpace(UISpace space)
    {
        uiSpace = space;
    }
}