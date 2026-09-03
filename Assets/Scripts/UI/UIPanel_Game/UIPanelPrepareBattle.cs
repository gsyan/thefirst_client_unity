// 탐험 그리드 인접 셀 진입 시 적 함대와 대치한 상태에서 뜨는 패널 — 진입 즉시 좌우 분할뷰(UIFleetStandoffView) + 전투시작/퇴각 2버튼
// UITabExplorationGrid는 셀 진입 확정 시 탭을 닫아버리므로(카메라 갤럭시뷰→로컬뷰 복귀 트리거), 이 패널은 그 탭의 자식이 아니라
// 로컬뷰 복귀 완료 + 적 함대 스폰 완료 시점에 별도로 열림(Open 호출부: UITabExplorationGrid.SpawnEnemyFleetAndWarpIn 이후)
using UnityEngine;
using UnityEngine.UI;

public class UIPanelPrepareBattle : UIPanelBase
{
    [SerializeField] private Button m_startButton;
    [SerializeField] private Button m_retreatButton;
    [SerializeField] private UIFleetStandoffView m_standoffView; // 좌/우 듀얼 카메라 분할뷰 — 진입 즉시 항상 이 화면부터 시작

    private System.Action m_onStartBattle;
    private System.Action m_onRetreat;
    private SpaceFleet m_myFleet;
    private SpaceFleet m_enemyFleet;

    public override void InitializeUIPanel()
    {
        m_startButton.onClick.AddListener(OnClickStart);
        m_retreatButton.onClick.AddListener(OnClickRetreat);
    }

    // 셀 진입이 확정된 즉시(워프인 애니메이션이 끝나기 전) 콘텐츠 없이 패널만 먼저 push — 탐험그리드 패널 위에 이 패널이
    // 곧바로 덮이도록 해서(탐험그리드는 스택에서 제거되지 않고 그대로 남음) 워프인 완료까지 메인 UI가 노출되지 않게 함
    // 실제 내용(좌우 분할뷰/버튼)은 SetupContent가 채움 — 그 전까지는 버튼도 눌리지 않도록 비활성 상태로 둠
    public void OpenEmpty()
    {
        SetButtonsVisible(false);
        UIManager.Instance.ShowPanel(panelName);
    }

    // 호출부는 함대 대치 상태를 만든 쪽(UITabExplorationGrid)에서 각 버튼의 실제 처리(콜백)를 넘겨줌 —
    // 워프인 연출 완료 + 적 함대 스폰 완료 시점(OpenEmpty 이후)에 호출되어 좌우 분할뷰와 버튼을 실제로 채움
    public void SetupContent(SpaceFleet myFleet, SpaceFleet enemyFleet, System.Action onStartBattle, System.Action onRetreat)
    {
        m_myFleet = myFleet;
        m_enemyFleet = enemyFleet;
        m_onStartBattle = onStartBattle;
        m_onRetreat = onRetreat;

        if (m_standoffView != null)
            m_standoffView.Open(m_myFleet, m_enemyFleet);

        SetButtonsVisible(true);
    }

    private void SetButtonsVisible(bool isVisible)
    {
        if (m_startButton != null) m_startButton.gameObject.SetActive(isVisible);
        if (m_retreatButton != null) m_retreatButton.gameObject.SetActive(isVisible);
    }

    public void Close()
    {
        if (m_standoffView != null) m_standoffView.Close();
        UIManager.Instance.HidePanel(panelName);
    }

    // 전투시작 — Close()(정상 pop)를 쓰면 그 사이 탐사그리드가 잠깐 top으로 드러났다가(OnShowUIPanel 발동) 곧바로
    // UIPanelBattle push로 다시 덮이는 스퓨리어스한 재진입이 발생함. 그 대신 콜백을 먼저 실행해 UIPanelBattle이
    // 이 패널 위로 자연스럽게 push되게 한 뒤(탐사그리드는 한 번도 top이 되지 않음), 파묻힌 이 패널만 조용히 제거
    private void OnClickStart()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        if (m_standoffView != null) m_standoffView.Close();

        if (m_onStartBattle != null) m_onStartBattle();

        UIManager.Instance.RemoveHiddenPanelFromStack(panelName);
    }

    private void OnClickRetreat()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        Close();

        if (m_onRetreat != null) m_onRetreat();
    }
}
