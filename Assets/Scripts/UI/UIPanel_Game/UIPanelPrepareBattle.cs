// 탐험 그리드 인접 셀 진입 시 적 함대와 대치한 상태에서 뜨는 패널 — 진입 즉시 좌우 분할뷰(UIFleetStandoffView) + 전투시작/퇴각 2버튼
// UITabExplorationGrid는 셀 진입 확정 시 탭을 닫아버리므로(카메라 갤럭시뷰→로컬뷰 복귀 트리거), 이 패널은 그 탭의 자식이 아니라
// 로컬뷰 복귀 완료 + 적 함대 스폰 완료 시점에 별도로 열림(Open 호출부: UITabExplorationGrid.SpawnEnemyFleetAndWarpIn 이후)
// 대치 건너뛰기 설정(셀 이동 팝업의 옵션 체크박스)이 켜져 있으면 이 화면을 보여주지 않고 곧바로 전투를 시작함(튜토리얼 중 제외)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelPrepareBattle : UIPanelBase
{
    [SerializeField] private Button m_startButton;
    [SerializeField] private TMP_Text m_startButtonText;
    [SerializeField] private Button m_retreatButton;
    [SerializeField] private TMP_Text m_retreatButtonText;
    [SerializeField] private UIFleetStandoffView m_standoffView; // 좌/우 듀얼 카메라 분할뷰 — 대치 건너뛰기가 아니면 진입 즉시 이 화면부터 시작
    [SerializeField] private TMP_Text m_zoneCellText; // "Zone: N\nCell: N-N" 표기
    [SerializeField] private GameObject m_bottomRoot; // ZoneCellText+버튼들의 공통 부모 — SetupContent 전엔 통째로 숨김

    // 키 이름은 이미 저장된 유저 설정과 호환되도록 유지
    private const string k_skipStandoffPrefKey = "AutoStartBattleOnCellEntry";

    private System.Action m_onStartBattle;
    private System.Action m_onRetreat;
    private SpaceFleet m_myFleet;
    private SpaceFleet m_enemyFleet;

    public static bool IsSkipStandoffEnabled()
    {
        return PlayerPrefs.GetInt(k_skipStandoffPrefKey, 0) == 1;
    }

    public static void SetSkipStandoffEnabled(bool isEnabled)
    {
        PlayerPrefs.SetInt(k_skipStandoffPrefKey, isEnabled == true ? 1 : 0);
        PlayerPrefs.Save();
    }

    public override void InitializeUIPanel()
    {
        m_startButton.onClick.AddListener(OnClickStart);
        m_retreatButton.onClick.AddListener(OnClickRetreat);

        if (m_startButtonText != null)
            CommonUtility.SetUILocText(m_startButtonText, "UI_StartBattle");
        if (m_retreatButtonText != null)
            CommonUtility.SetUILocText(m_retreatButtonText, "UI_Retreat");
    }

    // 셀 진입이 확정된 즉시(워프인 애니메이션이 끝나기 전) 콘텐츠 없이 패널만 먼저 push — 탐험그리드 패널 위에 이 패널이
    // 곧바로 덮이도록 해서(탐험그리드는 스택에서 제거되지 않고 그대로 남음) 워프인 완료까지 메인 UI가 노출되지 않게 함
    // 실제 내용(좌우 분할뷰/버튼)은 SetupContent가 채움 — 그 전까지는 버튼도 눌리지 않도록 비활성 상태로 둠
    public void OpenEmpty()
    {
        SetBottomVisible(false);
        // 대치 뷰(적 함대 카메라/구분선 포함)는 SetupContent의 Open()이 켤 때까지 숨김 — 프리팹 기본 상태가 활성이라 ShowPanel 시점에 함께 켜지기 때문
        if (m_standoffView != null)
            m_standoffView.gameObject.SetActive(false);
        UIManager.Instance.ShowPanel(panelName);
    }

    // 호출부는 함대 대치 상태를 만든 쪽(UITabExplorationGrid)에서 각 버튼의 실제 처리(콜백)를 넘겨줌 —
    // 워프인 연출 완료 + 적 함대 스폰 완료 시점(OpenEmpty 이후)에 호출되어 좌우 분할뷰와 버튼을 실제로 채움
    public void SetupContent(SpaceFleet myFleet, SpaceFleet enemyFleet, int zoneNumber, string cellDisplay, System.Action onStartBattle, System.Action onRetreat)
    {
        m_myFleet = myFleet;
        m_enemyFleet = enemyFleet;
        m_onStartBattle = onStartBattle;
        m_onRetreat = onRetreat;

        if (m_zoneCellText != null)
        {
            m_zoneCellText.text = LocalizationManager.Instance.Get("Zone_Cell_Text", zoneNumber, cellDisplay);
        }

        // 대치 건너뛰기 — 분할뷰/버튼을 띄우지 않고(OpenEmpty 상태 그대로) 곧바로 전투시작. 튜토리얼 중엔 대치 화면 안내가 있을 수 있어 적용하지 않음
        bool isTutorialPlaying = TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying == true;
        bool isSkipStandoff = IsSkipStandoffEnabled() == true && isTutorialPlaying == false;
        if (isSkipStandoff == true)
        {
            StartBattle();
            return;
        }

        if (m_standoffView != null)
            m_standoffView.Open(m_myFleet, m_enemyFleet);

        SetBottomVisible(true);
    }

    // 대치 건너뛰기로 바로 시작했는데 서버 전투시작이 실패한 경우 — 재시도/퇴각할 수 있게 대치 화면을 그제서야 띄움(이미 떠 있으면 그대로)
    public void ShowStandoffIfSkipped()
    {
        if (m_bottomRoot != null && m_bottomRoot.activeSelf == true) return;

        if (m_standoffView != null)
            m_standoffView.Open(m_myFleet, m_enemyFleet);
        SetBottomVisible(true);
    }

    private void SetBottomVisible(bool isVisible)
    {
        if (m_bottomRoot != null) m_bottomRoot.SetActive(isVisible);
    }

    public void Close()
    {
        if (m_standoffView != null) m_standoffView.Close();
        UIManager.Instance.HidePanel(panelName);
    }

    // 전투시작 — m_onStartBattle은 서버 EnterExplorationCell 응답을 기다린 뒤에야 UIPanelBattle을 push하는
    // 비동기 흐름이라, 이 패널을 스택에서 정리하는 건 여기서 직접 하지 않고 UIPanelBattle.OnShowUIPanel이
    // 실제로 뜨는 시점에 맡긴다(UIPanelBattle.cs 참고) — 응답 실패 시엔 UIPanelBattle이 안 뜨므로 이 패널이
    // 그대로 남아 재시도/퇴각이 가능해야 하는데, 여기서 미리 지워버리면 그 경로가 깨짐
    private void OnClickStart()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        StartBattle();
    }

    private void StartBattle()
    {
        if (m_standoffView != null) m_standoffView.Close();

        if (m_onStartBattle != null) m_onStartBattle();
    }

    private void OnClickRetreat()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        Close();

        if (m_onRetreat != null) m_onRetreat();
    }
}
