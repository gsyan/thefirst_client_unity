// 자원 충전 팝업 — 슬라이더로 소모할 광물 수를 정하면 충전 후 자원/소모 광물/충전량을 실시간 표시, 확인 시 한 번 더 묻고 그 목표치까지만 충전
// 버서커(자원 비율이 낮을수록 공격력 증가)를 위해 가득 채우지 않고 원하는 만큼만 충전할 수 있게 함
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupChargeResource : UIPopupBase
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private Slider m_slider;              // 값 = 소모할 광물 수(정수)
    [SerializeField] private TMP_Text m_currentLabelText;
    [SerializeField] private TMP_Text m_currentValueText;  // 현재 자원 "1,200 / 10,000 (12%)"
    [SerializeField] private TMP_Text m_targetLabelText;
    [SerializeField] private TMP_Text m_targetValueText;   // 충전 후 자원 "4,200 / 10,000 (42%)"
    [SerializeField] private TMP_Text m_mineralLabelText;
    [SerializeField] private TMP_Text m_mineralValueText;  // 소모 광물 "300 / 1,500"
    [SerializeField] private TMP_Text m_gainLabelText;
    [SerializeField] private TMP_Text m_gainValueText;     // 충전량 "+3,000"
    [SerializeField] private Button m_cancelButton;
    [SerializeField] private TMP_Text m_cancelButtonText;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private TMP_Text m_confirmButtonText;

    private System.Action m_onClose;
    private System.Action<int, int> m_onCharged; // (자원 현재치, 광물 현재치) — 서버 권위값
    private int m_currentResource;
    private int m_resourceMax;
    private int m_mineralAmount;
    private int m_ratio;

    protected override void Awake()
    {
        base.Awake();
        if (m_cancelButton != null) m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_slider != null)
        {
            m_slider.wholeNumbers = true;
            m_slider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (m_titleText != null) CommonUtility.SetUILocText(m_titleText, "UIPopupChargeResource_Title");
        if (m_currentLabelText != null) CommonUtility.SetUILocText(m_currentLabelText, "UIPopupChargeResource_Current");
        if (m_targetLabelText != null) CommonUtility.SetUILocText(m_targetLabelText, "UIPopupChargeResource_Target");
        if (m_mineralLabelText != null) CommonUtility.SetUILocText(m_mineralLabelText, "UIPopupChargeResource_MineralCost");
        if (m_gainLabelText != null) CommonUtility.SetUILocText(m_gainLabelText, "UIPopupChargeResource_Gain");
        if (m_cancelButtonText != null) CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");
        if (m_confirmButtonText != null) CommonUtility.SetUILocText(m_confirmButtonText, "UI_Confirm");
    }

    public void ShowPopupChargeResource(System.Action onClose, System.Action<int, int> onCharged)
    {
        m_onClose = onClose;
        m_onCharged = onCharged;

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        m_currentResource = commanderInfo != null ? commanderInfo.resource : 0;
        m_resourceMax = commanderInfo != null ? commanderInfo.resourceMax : 0;
        m_mineralAmount = commanderInfo != null ? commanderInfo.mineralAmount : 0;
        m_ratio = Mathf.Max(1, DataManager.Instance.m_dataTableConfig.gameSettings.tactic.mineralToResourceRatio);

        int maxSpendMineral = ComputeMaxSpendMineral();
        if (m_slider != null)
        {
            m_slider.minValue = 0;
            m_slider.maxValue = maxSpendMineral;
            m_slider.interactable = maxSpendMineral > 0;
            m_slider.SetValueWithoutNotify(0);
        }

        ShowPopup();
        RefreshTexts(0);
    }

    // 최대치까지 채우는 데 드는 광물(올림)과 보유 광물 중 작은 값
    private int ComputeMaxSpendMineral()
    {
        int neededResource = m_resourceMax - m_currentResource;
        if (neededResource <= 0) return 0;

        int mineralToFull = (neededResource + m_ratio - 1) / m_ratio;
        return Mathf.Min(mineralToFull, m_mineralAmount);
    }

    private int ComputeGain(int spendMineral)
    {
        int neededResource = Mathf.Max(0, m_resourceMax - m_currentResource);
        int rawGain = spendMineral * m_ratio;
        return Mathf.Min(neededResource, rawGain);
    }

    private void OnSliderChanged(float value)
    {
        int spendMineral = Mathf.RoundToInt(value);
        RefreshTexts(spendMineral);
    }

    private void RefreshTexts(int spendMineral)
    {
        int gain = ComputeGain(spendMineral);
        int targetResource = m_currentResource + gain;

        if (m_currentValueText != null) m_currentValueText.text = BuildResourceText(m_currentResource);
        if (m_targetValueText != null) m_targetValueText.text = BuildResourceText(targetResource);
        if (m_mineralValueText != null) m_mineralValueText.text = $"{CommonUtility.FormatNumber(spendMineral)} / {CommonUtility.FormatNumber(m_mineralAmount)}";
        if (m_gainValueText != null) m_gainValueText.text = $"+{CommonUtility.FormatNumber(gain)}";
        if (m_confirmButton != null) m_confirmButton.interactable = gain > 0;
    }

    // "4,200 / 10,000 (42%)"
    private string BuildResourceText(int resource)
    {
        int percent = m_resourceMax > 0 ? Mathf.FloorToInt(resource * 100f / m_resourceMax) : 0;
        return $"{CommonUtility.FormatNumber(resource)} / {CommonUtility.FormatNumber(m_resourceMax)} ({percent}%)";
    }

    private void OnCancelClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClose != null) m_onClose();
    }

    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        int spendMineral = m_slider != null ? Mathf.RoundToInt(m_slider.value) : 0;
        int gain = ComputeGain(spendMineral);
        if (gain <= 0) return;

        int targetResource = m_currentResource + gain;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = string.Format(LocalizationManager.Instance.Get("UIPanelExplorationGrid_ChargeResourceConfirmMessage"), spendMineral, gain),
            onConfirm = () => RequestChargeResource(targetResource),
            onCancel = () => { },
        });
    }

    private void RequestChargeResource(int targetResource)
    {
        if (m_confirmButton != null) m_confirmButton.interactable = false; // 응답 전 중복 요청 방지

        NetworkManager.Instance.ChargeResource(new ChargeResourceRequest { targetResource = targetResource }, response =>
        {
            if (response.errorCode != 0 || response.data == null)
            {
                NetworkManager.Instance.ShowRequestFailedPopup(response.errorCode);
                RefreshTexts(m_slider != null ? Mathf.RoundToInt(m_slider.value) : 0);
                return;
            }

            if (m_onCharged != null) m_onCharged(response.data.resource, response.data.mineralAmount);
            if (m_onClose != null) m_onClose();
        });
    }
}
