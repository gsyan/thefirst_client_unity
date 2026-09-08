// 진입 버튼 하나가 UIPanelBase 패널 하나를 열고 닫음 — TabSystem/ButtonGroupSystem을 대신해 배타적 화면 전환용 진입 버튼에 부착
// 이미 그 패널이 열려있는 상태에서 재클릭하면 UIManager.HideCurrentPanel()로 닫힘(기존 ButtonGroupSystem allowDeselect와 동일 동작)
using UnityEngine;
using UnityEngine.UI;

public class UIPanelEntryButton : MonoBehaviour
{
    [SerializeField] private string m_panelName;

    private Button m_button;

    private void Awake()
    {
        m_button = GetComponent<Button>();
        if (m_button != null)
            m_button.onClick.AddListener(OnClick);
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
}
