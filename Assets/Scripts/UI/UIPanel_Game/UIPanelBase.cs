using UnityEngine;

public class UIPanelBase : MonoBehaviour
{
    public string panelName;
    public bool bMainPanel = false;
    public bool bHideCurWhenActive = false; // 이 패널이 활성화될 때 현재 패널을 숨길지 여부
    public bool bCameraMove = false;
    // false면 UIManager의 오버레이 패널 카운트(탭 진입 버튼 숨김 판단 기준)에서 제외 — VIP 팝오버처럼 화면 전체를 가리지 않는 보조 패널용
    public bool bAffectsOverlayCount = true;

    virtual public void InitializeUIPanel()
    {
        
    }

    virtual public void OnShowUIPanel()
    {
        
    }

    virtual public void OnHideUIPanel()
    {
        
    }

}

