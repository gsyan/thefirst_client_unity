// 진입 버튼 하나가 UIPanelBase 패널 하나를 열고 닫음 — TabSystem/ButtonGroupSystem을 대신해 배타적 화면 전환용 진입 버튼에 부착
// 이미 그 패널이 열려있는 상태에서 재클릭하면 UIManager.HideCurrentPanel()로 닫힘(기존 ButtonGroupSystem allowDeselect와 동일 동작)
using UnityEngine;
using UnityEngine.UI;

public class UIPanelEntryButton : MonoBehaviour
{
    [SerializeField] private string m_panelName;
    [SerializeField] private Graphic[] m_childGraphics; // 하이라이트 색상 연동 대상(자식 Image, TMP_Text)

    private Button m_button;
    private Color m_colorActive;
    private Color m_colorInactive;

    private void Awake()
    {
        m_button = GetComponent<Button>();
        if (m_button != null)
            m_button.onClick.AddListener(OnClick);

        m_colorActive   = CommonUtility.PaletteColor("General.Bright1");
        m_colorInactive = CommonUtility.PaletteColor("General.Dark1");

        EventManager.Subscribe_CurrentPanelChanged(OnCurrentPanelChanged);
        RefreshHighlight(UIManager.Instance.GetCurrentActivePanelName());
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_CurrentPanelChanged(OnCurrentPanelChanged);
    }

    // 튜토리얼 dim 없는 스텝에서 진입 버튼 클릭을 일괄 차단할 때 사용 (UIPanelSpace.OnTutorialGeneralUIBlockedChanged)
    public void SetInteractable(bool interactable)
    {
        if (m_button != null)
            m_button.interactable = interactable;
    }

    private void OnClick()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        if (UIManager.Instance.GetCurrentActivePanelName() == m_panelName)
            UIManager.Instance.HideCurrentPanel();
        else
            UIManager.Instance.ShowPanel(m_panelName);
    }

    private void OnCurrentPanelChanged(string panelName)
    {
        RefreshHighlight(panelName);
    }

    private void RefreshHighlight(string activePanelName)
    {
        Color color = (activePanelName == m_panelName) ? m_colorActive : m_colorInactive;

        if (m_button != null)
        {
            var colors = m_button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.1f;
            colors.pressedColor = color * 0.8f;
            colors.selectedColor = color;
            m_button.colors = colors;
        }

        if (m_childGraphics != null)
        {
            for (int i = 0; i < m_childGraphics.Length; i++)
            {
                if (m_childGraphics[i] != null)
                    m_childGraphics[i].color = color;
            }
        }
    }
}
