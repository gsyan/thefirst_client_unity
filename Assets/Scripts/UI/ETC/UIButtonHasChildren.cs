using UnityEngine;
using UnityEngine.UI;

// 버튼 + 자식 Graphic(Image, TMP_Text 등) 색상을 일괄 변경하는 컴포넌트
[RequireComponent(typeof(Button))]
public class UIButtonHasChildren : MonoBehaviour
{
    private Button   m_button;
    private Graphic[] m_graphics;

    public Button GetButton()
    {
        if (m_button == null)
            m_button = GetComponent<Button>();
        return m_button;
    }

    void Awake()
    {
        GetButton();
        m_graphics = GetComponentsInChildren<Graphic>(true);
    }

    public void SetColor(Color color)
    {
        if (m_graphics == null)
            m_graphics = GetComponentsInChildren<Graphic>(true);
            
        for (int i = 0; i < m_graphics.Length; i++)
            if (m_graphics[i] != null)
                m_graphics[i].color = color;
    }

    public void SetInteractable(bool interactable)
    {
        GetButton().interactable = interactable;
        SetColor(interactable ? CommonUtility.PaletteColor("GeneralBright1") : CommonUtility.PaletteColor("GeneralDark2"));
    }
}
