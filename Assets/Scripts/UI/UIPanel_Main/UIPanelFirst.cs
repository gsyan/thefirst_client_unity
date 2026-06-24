using UnityEngine;
using UnityEngine.UI;

public class UIPanelFirst : UIPanelBase
{
    public override void InitializeUIPanel()
    {
        SoundManager.Instance.PlayFX(EFx.Main);
    }


    public override void OnShowUIPanel()
    {
        
    }

    public override void OnHideUIPanel()
    {
        
    }
}
