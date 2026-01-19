using UnityEngine;

public abstract class UIPopupBase : MonoBehaviour
{
    protected virtual void Awake()
    {
        gameObject.SetActive(false);
    }

    public virtual void ShowPopup()
    {
        gameObject.SetActive(true);
    }

    public virtual void HidePopup()
    {
        gameObject.SetActive(false);
    }
}
