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

    // 호출부는 함대 대치 상태를 만든 쪽(UITabExplorationGrid)에서 각 버튼의 실제 처리(콜백)를 넘겨줌 —
    // 이 패널은 UI 표시/입력만 담당하고 함대/그리드 상태는 모름. 진입하자마자 좌우 분할뷰(UIFleetStandoffView)부터 보여줌
    public void Open(SpaceFleet myFleet, SpaceFleet enemyFleet, System.Action onStartBattle, System.Action onRetreat)
    {
        m_myFleet = myFleet;
        m_enemyFleet = enemyFleet;
        m_onStartBattle = onStartBattle;
        m_onRetreat = onRetreat;

        if (m_standoffView != null)
            m_standoffView.Open(m_myFleet, m_enemyFleet);

        UIManager.Instance.ShowPanel(panelName);
    }

    public void Close()
    {
        if (m_standoffView != null) m_standoffView.Close();
        UIManager.Instance.HidePanel(panelName);
    }

    private void OnClickStart()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        Close();

        if (m_onStartBattle != null) m_onStartBattle();
    }

    private void OnClickRetreat()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        Close();

        if (m_onRetreat != null) m_onRetreat();
    }
}
