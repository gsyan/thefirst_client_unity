// 탐험 그리드 인접 셀 진입 시 적 함대와 대치한 상태에서 뜨는 패널 — 진입 즉시 좌우 분할뷰(UIFleetStandoffView) + 전투시작/퇴각 2버튼
// UITabExplorationGrid는 셀 진입 확정 시 탭을 닫아버리므로(카메라 갤럭시뷰→로컬뷰 복귀 트리거), 이 패널은 그 탭의 자식이 아니라
// 로컬뷰 복귀 완료 + 적 함대 스폰 완료 시점에 별도로 열림(Open 호출부: UITabExplorationGrid.SpawnEnemyFleetAndWarpIn 이후)
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelPrepareBattle : UIPanelBase
{
    [SerializeField] private Button m_startButton;
    [SerializeField] private TMP_Text m_startButtonText;
    [SerializeField] private Button m_retreatButton;
    [SerializeField] private TMP_Text m_retreatButtonText;
    [SerializeField] private UIFleetStandoffView m_standoffView; // 좌/우 듀얼 카메라 분할뷰 — 진입 즉시 항상 이 화면부터 시작
    [SerializeField] private TMP_Text m_zoneCellText; // "Zone: N\nCell: N-N" 표기
    [SerializeField] private GameObject m_bottomRoot; // ZoneCellText+버튼들의 공통 부모 — SetupContent 전엔 통째로 숨김
    [SerializeField] private ToggleButton m_autoStartToggle; // 켜두면 대치 화면 진입 k_autoStartDelaySec초 후 전투시작이 자동으로 눌림(단순 체크박스라 SetSelected로만 시각 제어, 선택 상태는 직접 관리)

    private const string k_autoStartPrefKey = "AutoStartBattleOnCellEntry";
    private const float k_autoStartDelaySec = 3f; // 이 시간 동안은 화면이 정상 조작 가능 상태로 유지되어, 그 사이 토글을 끄면 자동 진입이 취소됨

    private System.Action m_onStartBattle;
    private System.Action m_onRetreat;
    private SpaceFleet m_myFleet;
    private SpaceFleet m_enemyFleet;
    private Coroutine m_autoStartCoroutine;
    private bool m_autoStartEnabled;

    public override void InitializeUIPanel()
    {
        m_startButton.onClick.AddListener(OnClickStart);
        m_retreatButton.onClick.AddListener(OnClickRetreat);

        if (m_startButtonText != null)
            CommonUtility.SetUILocText(m_startButtonText, "UI_StartBattle");
        if (m_retreatButtonText != null)
            CommonUtility.SetUILocText(m_retreatButtonText, "UI_Retreat");

        if (m_autoStartToggle != null)
        {
            m_autoStartToggle.SetTexts("UI_AutoEnterBattle", "");
            m_autoStartEnabled = PlayerPrefs.GetInt(k_autoStartPrefKey, 0) == 1;
            m_autoStartToggle.SetSelected(m_autoStartEnabled);
            m_autoStartToggle.button.onClick.AddListener(OnClickAutoStartToggle);
        }
    }

    private void OnClickAutoStartToggle()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        bool newValue = m_autoStartEnabled == false;
        m_autoStartEnabled = newValue;
        m_autoStartToggle.SetSelected(newValue);

        PlayerPrefs.SetInt(k_autoStartPrefKey, newValue ? 1 : 0);
        PlayerPrefs.Save();

        if (newValue == false && m_autoStartCoroutine != null)
        {
            StopCoroutine(m_autoStartCoroutine);
            m_autoStartCoroutine = null;
        }
    }

    // 셀 진입이 확정된 즉시(워프인 애니메이션이 끝나기 전) 콘텐츠 없이 패널만 먼저 push — 탐험그리드 패널 위에 이 패널이
    // 곧바로 덮이도록 해서(탐험그리드는 스택에서 제거되지 않고 그대로 남음) 워프인 완료까지 메인 UI가 노출되지 않게 함
    // 실제 내용(좌우 분할뷰/버튼)은 SetupContent가 채움 — 그 전까지는 버튼도 눌리지 않도록 비활성 상태로 둠
    public void OpenEmpty()
    {
        SetBottomVisible(false);
        Debug.Log($"[StandoffDiag] OpenEmpty ShowPanel begin t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}"); // [진단] 원인 확정 후 제거
        UIManager.Instance.ShowPanel(panelName);
        Debug.Log($"[StandoffDiag] OpenEmpty ShowPanel end t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}"); // [진단] 원인 확정 후 제거
    }

    // 호출부는 함대 대치 상태를 만든 쪽(UITabExplorationGrid)에서 각 버튼의 실제 처리(콜백)를 넘겨줌 —
    // 워프인 연출 완료 + 적 함대 스폰 완료 시점(OpenEmpty 이후)에 호출되어 좌우 분할뷰와 버튼을 실제로 채움
    public void SetupContent(SpaceFleet myFleet, SpaceFleet enemyFleet, int zoneNumber, string cellDisplay, System.Action onStartBattle, System.Action onRetreat)
    {
        m_myFleet = myFleet;
        m_enemyFleet = enemyFleet;
        m_onStartBattle = onStartBattle;
        m_onRetreat = onRetreat;

        Debug.Log($"[StandoffDiag] SetupContent begin t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}"); // [진단] 원인 확정 후 제거
        if (m_standoffView != null)
            m_standoffView.Open(m_myFleet, m_enemyFleet);
        Debug.Log($"[StandoffDiag] SetupContent standoffView.Open done t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}"); // [진단] 원인 확정 후 제거

        if (m_zoneCellText != null)
        {
            m_zoneCellText.text = LocalizationManager.Instance.Get("Zone_Cell_Text", zoneNumber, cellDisplay);
        }

        SetBottomVisible(true);
        Debug.Log($"[StandoffDiag] SetupContent SetBottomVisible done t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}"); // [진단] 원인 확정 후 제거
        StartCoroutine(Co_DiagFrameTimes()); // [진단] 원인 확정 후 제거

        if (m_autoStartEnabled == true)
            m_autoStartCoroutine = StartCoroutine(Co_AutoStartAfterDelay());
    }

    // [진단] 원인 확정 후 제거 — 대치 화면 완성 직후 프레임 시간 기록(처음 5프레임은 전부, 이후 15초간은 0.1초 이상 걸린 프레임만)
    private IEnumerator Co_DiagFrameTimes()
    {
        const int k_logAllFrameCount = 5;
        const float k_hitchThresholdSec = 0.1f;
        const float k_watchDurationSec = 15f;

        float watchStartTime = Time.realtimeSinceStartup;
        int frameIndex = 0;
        while (Time.realtimeSinceStartup - watchStartTime < k_watchDurationSec)
        {
            yield return null;
            frameIndex++;
            float frameDeltaSec = Time.unscaledDeltaTime;
            bool isHitch = frameDeltaSec >= k_hitchThresholdSec;
            if (frameIndex <= k_logAllFrameCount || isHitch == true)
                Debug.Log($"[StandoffDiag] after SetupContent +{frameIndex}f dt={frameDeltaSec:F3} t={Time.realtimeSinceStartup:F3} frame={Time.frameCount} hitch={isHitch}");
        }
    }

    private IEnumerator Co_AutoStartAfterDelay()
    {
        yield return new WaitForSeconds(k_autoStartDelaySec);
        m_autoStartCoroutine = null;
        OnClickStart();
    }

    private void SetBottomVisible(bool isVisible)
    {
        if (m_bottomRoot != null) m_bottomRoot.SetActive(isVisible);
    }

    private void StopAutoStartCoroutine()
    {
        if (m_autoStartCoroutine == null) return;
        StopCoroutine(m_autoStartCoroutine);
        m_autoStartCoroutine = null;
    }

    public void Close()
    {
        StopAutoStartCoroutine();
        if (m_standoffView != null) m_standoffView.Close();
        UIManager.Instance.HidePanel(panelName);
    }

    // 전투시작 — m_onStartBattle은 서버 EnterExplorationCell 응답을 기다린 뒤에야 UIPanelBattle을 push하는
    // 비동기 흐름이라, 이 패널을 스택에서 정리하는 건 여기서 직접 하지 않고 UIPanelBattle.OnShowUIPanel이
    // 실제로 뜨는 시점에 맡긴다(UIPanelBattle.cs 참고) — 응답 실패 시엔 UIPanelBattle이 안 뜨므로 이 패널이
    // 그대로 남아 재시도/퇴각이 가능해야 하는데, 여기서 미리 지워버리면 그 경로가 깨짐
    private void OnClickStart()
    {
        StopAutoStartCoroutine();
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

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
