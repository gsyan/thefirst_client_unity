// 탐험 그리드 인접 셀 진입 시 적 함대와 대치한 상태에서 뜨는 패널 — 전투시작/퇴각/함대설정 3버튼
// UITabExplorationGrid는 셀 진입 확정 시 탭을 닫아버리므로(카메라 갤럭시뷰→로컬뷰 복귀 트리거), 이 패널은 그 탭의 자식이 아니라
// 로컬뷰 복귀 완료 + 적 함대 스폰 완료 시점에 별도로 열림(Open 호출부: UITabExplorationGrid.SpawnEnemyFleetAndWarpIn 이후)
using UnityEngine;
using UnityEngine.UI;

public class UIPanelPrepareBattle : UIPanelBase
{
    [SerializeField] private Button m_startButton;
    [SerializeField] private Button m_retreatButton;
    [SerializeField] private Button m_fleetSettingButton;
    [SerializeField] private GameObject m_buttonContainer;       // 3버튼을 담은 "horizontal" 컨테이너 — 함대설정 진입 시 숨김
    [SerializeField] private UIFleetStandoffView m_standoffView; // 함대설정(정찰) 모드 — 좌/우 듀얼 카메라 분할뷰

    private System.Action m_onStartBattle;
    private System.Action m_onRetreat;
    private SpaceFleet m_myFleet;
    private SpaceFleet m_enemyFleet;

    public override void InitializeUIPanel()
    {
        m_startButton.onClick.AddListener(OnClickStart);
        m_retreatButton.onClick.AddListener(OnClickRetreat);
        m_fleetSettingButton.onClick.AddListener(OnClickFleetSetting);
    }

    // 호출부는 함대 대치 상태를 만든 쪽(UITabExplorationGrid)에서 각 버튼의 실제 처리(콜백)를 넘겨줌 —
    // 이 패널은 UI 표시/입력만 담당하고 함대/그리드 상태는 모름. 함대설정은 콜백이 아니라 이 패널 안에서 듀얼뷰를 직접 열고 닫음(1단계 범위)
    public void Open(SpaceFleet myFleet, SpaceFleet enemyFleet, System.Action onStartBattle, System.Action onRetreat)
    {
        m_myFleet = myFleet;
        m_enemyFleet = enemyFleet;
        m_onStartBattle = onStartBattle;
        m_onRetreat = onRetreat;

        if (m_buttonContainer != null) m_buttonContainer.SetActive(true);
        if (m_standoffView != null) m_standoffView.Close();

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

    // 함대설정 버튼은 ButtonContainer 안에 있어 듀얼뷰가 열리면 같이 숨겨짐 — 복귀는 UIFleetStandoffView 안의 별도 "뒤로" 버튼이 이 콜백을 통해 처리
    // 2단계에서 "허공 터치 시 복귀"가 추가되면 이 콜백에 이어붙이면 됨
    private void OnClickFleetSetting()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_standoffView == null) return;

        if (m_buttonContainer != null) m_buttonContainer.SetActive(false);
        m_standoffView.Open(m_myFleet, m_enemyFleet, OnStandoffViewClosed);
    }

    private void OnStandoffViewClosed()
    {
        if (m_buttonContainer != null) m_buttonContainer.SetActive(true);
    }
}
