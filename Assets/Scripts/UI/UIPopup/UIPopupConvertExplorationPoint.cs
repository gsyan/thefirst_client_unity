// 탐사포인트 -> 지휘력/전술력 최대치 변환 팝업 — +10/+100/+1000, -10/-100/-1000, 전체, 초기화 버튼으로 소모량을 직접 조정 후 확인
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이 팝업이 어떤 자원의 최대치를 늘려주는 중인지 — 서버 API/응답 필드/DataManager 갱신 대상이 이 값에 따라 갈라짐
public enum EExplorationPointConvertTarget
{
    CommandPower,
    TacticPower,
}

public class UIPopupConvertExplorationPoint : UIPopupBase
{
    [SerializeField] private TMP_Text m_ownedExplorationPointText;
    [SerializeField] private TMP_Text m_currentCommandPowerText;
    [SerializeField] private TMP_Text m_targetLabelText; // "지휘력" / "전술력" — 대상에 따라 라벨 텍스트 교체

    // 증감 표시 색상 - 괄호와 그 안의 델타 수치("(-10)", "(+10)")를 강조
    private const string k_deltaColorHex = "#FF5555";

    [SerializeField] private Button m_plus10Button;
    [SerializeField] private Button m_minus10Button;
    [SerializeField] private Button m_plus100Button;
    [SerializeField] private Button m_minus100Button;
    [SerializeField] private Button m_plus1000Button;
    [SerializeField] private Button m_minus1000Button;
    [SerializeField] private Button m_allButton;
    [SerializeField] private Button m_resetButton;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    // 서버 ExplorationService.increaseCommandPowerMax()/increaseTacticPowerMax()와 반드시 함께 수정 — 탐사포인트 1당 증가량(둘 다 1:1)
    private const float k_explorationPointToPowerRatio = 1f;

    private System.Action m_onClose;
    private System.Action m_onConfirmed;
    private EExplorationPointConvertTarget m_target;

    private int m_ownedExplorationPoint;
    private int m_currentPowerMax;
    private int m_pendingAmount;

    protected override void Awake()
    {
        base.Awake();

        if (m_plus10Button != null)    m_plus10Button.onClick.AddListener(() => OnStepClicked(10));
        if (m_minus10Button != null)   m_minus10Button.onClick.AddListener(() => OnStepClicked(-10));
        if (m_plus100Button != null)   m_plus100Button.onClick.AddListener(() => OnStepClicked(100));
        if (m_minus100Button != null)  m_minus100Button.onClick.AddListener(() => OnStepClicked(-100));
        if (m_plus1000Button != null)  m_plus1000Button.onClick.AddListener(() => OnStepClicked(1000));
        if (m_minus1000Button != null) m_minus1000Button.onClick.AddListener(() => OnStepClicked(-1000));
        if (m_allButton != null)       m_allButton.onClick.AddListener(OnAllClicked);
        if (m_resetButton != null)     m_resetButton.onClick.AddListener(OnResetClicked);
        if (m_confirmButton != null)   m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)    m_cancelButton.onClick.AddListener(OnCancelClicked);
    }

    public void ShowPopupConvertExplorationPoint(EExplorationPointConvertTarget target, System.Action onClose, System.Action onConfirmed = null)
    {
        m_target      = target;
        m_onClose     = onClose;
        m_onConfirmed = onConfirmed;

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        m_ownedExplorationPoint = commanderInfo != null ? commanderInfo.explorationPoint : 0;
        m_currentPowerMax = commanderInfo == null ? 0
            : m_target == EExplorationPointConvertTarget.CommandPower ? commanderInfo.commandPowerMax
            : commanderInfo.tacticPowerMax;

        if (m_targetLabelText != null)
        {
            string labelKey = m_target == EExplorationPointConvertTarget.CommandPower ? "UITabCommander_CommandPower" : "TacticPower";
            m_targetLabelText.text = LocalizationManager.Instance.Get(labelKey);
        }

        m_pendingAmount = 0;

        RefreshUI();
        base.ShowPopup();
    }

    private int CalculatePowerGain(int explorationPointAmount)
    {
        return Mathf.FloorToInt(explorationPointAmount * k_explorationPointToPowerRatio);
    }

    // 기준값 옆에 델타를 빨간 괄호로 붙임 — 예: "60 <color=#FF5555>(-10)</color>", 델타가 0이면 괄호 생략
    private string BuildValueWithDelta(int baseValue, int delta)
    {
        if (delta == 0) return baseValue.ToString();

        string sign = delta > 0 ? "+" : "";
        return $"{baseValue} <color={k_deltaColorHex}>({sign}{delta})</color>";
    }

    private void OnStepClicked(int step)
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_pendingAmount = Mathf.Clamp(m_pendingAmount + step, 0, m_ownedExplorationPoint);
        RefreshUI();
    }

    private void OnAllClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_pendingAmount = m_ownedExplorationPoint;
        RefreshUI();
    }

    private void OnResetClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_pendingAmount = 0;
        RefreshUI();
    }

    private void RefreshUI()
    {
        int remaining = m_ownedExplorationPoint - m_pendingAmount;
        int powerGain = CalculatePowerGain(m_pendingAmount);

        if (m_ownedExplorationPointText != null)
            m_ownedExplorationPointText.text = BuildValueWithDelta(m_ownedExplorationPoint, -m_pendingAmount);
        if (m_currentCommandPowerText != null)
            m_currentCommandPowerText.text = BuildValueWithDelta(m_currentPowerMax, powerGain);

        if (m_plus10Button != null)    m_plus10Button.interactable    = remaining >= 10;
        if (m_plus100Button != null)   m_plus100Button.interactable   = remaining >= 100;
        if (m_plus1000Button != null)  m_plus1000Button.interactable  = remaining >= 1000;
        if (m_minus10Button != null)   m_minus10Button.interactable   = m_pendingAmount >= 10;
        if (m_minus100Button != null)  m_minus100Button.interactable  = m_pendingAmount >= 100;
        if (m_minus1000Button != null) m_minus1000Button.interactable = m_pendingAmount >= 1000;
        if (m_allButton != null)       m_allButton.interactable       = m_pendingAmount < m_ownedExplorationPoint;
        if (m_resetButton != null)     m_resetButton.interactable     = m_pendingAmount > 0;
        if (m_confirmButton != null)   m_confirmButton.interactable   = m_pendingAmount > 0;
    }

    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_pendingAmount <= 0) return;

        m_confirmButton.interactable = false;

        if (m_target == EExplorationPointConvertTarget.CommandPower)
        {
            IncreaseCommandPowerMaxRequest request = new IncreaseCommandPowerMaxRequest();
            request.amount = m_pendingAmount;
            NetworkManager.Instance.IncreaseCommandPowerMax(request, OnIncreaseCommandPowerResponse);
        }
        else
        {
            IncreaseTacticPowerMaxRequest request = new IncreaseTacticPowerMaxRequest();
            request.amount = m_pendingAmount;
            NetworkManager.Instance.IncreaseTacticPowerMax(request, OnIncreaseTacticPowerResponse);
        }
    }

    private void OnIncreaseCommandPowerResponse(ApiResponse<IncreaseCommandPowerMaxResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            Debug.LogError($"[UIPopupConvertExplorationPoint] IncreaseCommandPowerMax 실패: {(response != null ? response.errorCode : -1)}");
            if (m_confirmButton != null) m_confirmButton.interactable = true;
            return;
        }

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition != null)
            composition.SetMaxCommandPower(response.data.commandPowerMax);

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo != null)
            commanderInfo.commandPowerMax = response.data.commandPowerMax;

        ApplyExplorationPointRemainAndClose(response.data.explorationPointRemain);
    }

    private void OnIncreaseTacticPowerResponse(ApiResponse<IncreaseTacticPowerMaxResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            Debug.LogError($"[UIPopupConvertExplorationPoint] IncreaseTacticPowerMax 실패: {(response != null ? response.errorCode : -1)}");
            if (m_confirmButton != null) m_confirmButton.interactable = true;
            return;
        }

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo != null)
        {
            commanderInfo.tacticPowerMax = response.data.tacticPowerMax;
            commanderInfo.tacticPower = response.data.tacticPower;
            EventManager.Trigger_TacticPowerChanged(commanderInfo.tacticPower, commanderInfo.tacticPowerMax);
        }

        ApplyExplorationPointRemainAndClose(response.data.explorationPointRemain);
    }

    private void ApplyExplorationPointRemainAndClose(int explorationPointRemain)
    {
        // Commander.UpdateExplorationPoint()를 거쳐야 EventManager.OnExplorationPointChanged가 발행되어 다른 열린 패널도 즉시 갱신됨
        if (DataManager.Instance.m_currentCommander != null)
            DataManager.Instance.m_currentCommander.UpdateExplorationPoint(explorationPointRemain);

        if (m_onConfirmed != null) m_onConfirmed.Invoke();
        if (m_onClose != null) m_onClose.Invoke();
    }

    private void OnCancelClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClose != null) m_onClose.Invoke();
    }
}
