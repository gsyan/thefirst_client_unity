using UnityEngine;
using UnityEngine.UI;

// 버튼 + 자식 Graphic(Image, TMP_Text 등) 색상을 일괄 변경하는 컴포넌트
// m_exceptGraphics에 등록된 것들은 별도의 except 색상 키 적용
[RequireComponent(typeof(Button))]
public class UIButtonHasChildren : MonoBehaviour
{
    [SerializeField] private string m_activeColorKey   = "GeneralBright1";
    [SerializeField] private string m_inactiveColorKey = "GeneralDark2";

    private Button    m_button;
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

    public bool IsInteractable()
    {
        return GetButton().interactable;
    }

    public void SetInteractable(bool interactable)
    {
        GetButton().interactable = interactable;

        if (m_graphics == null)
            m_graphics = GetComponentsInChildren<Graphic>(true);

        Color mainColor   = CommonUtility.PaletteColor(interactable == true ? m_activeColorKey   : m_inactiveColorKey);

        for (int i = 0; i < m_graphics.Length; i++)
        {
            if (m_graphics[i] == null) continue;
            m_graphics[i].color = mainColor;
        }
    }

    public void SetActiveColorKey(string colorKey)
    {
        m_activeColorKey = colorKey;
        if (GetButton().interactable == true)
            SetInteractable(true);
    }
}
