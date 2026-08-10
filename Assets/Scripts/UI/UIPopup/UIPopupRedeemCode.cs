// 보상코드 입력 팝업: 텍스트 입력 + 확인/취소, 서버 응답으로 결과 표시
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupRedeemCode : UIPopupBase
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField m_codeInput;
    [SerializeField] private TMP_Text m_resultText;
    [SerializeField] private UIButtonHasChildren m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [Header("색상")]
    [SerializeField] private Color m_colorSuccess = Color.green;
    [SerializeField] private Color m_colorError   = Color.red;

    private Action m_onClose;

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null)
        {
            m_confirmButton.GetButton().onClick.AddListener(OnConfirmClicked);
            m_confirmButton.SetActiveColorKey("Action.Primary");
        }
        if (m_cancelButton != null)  m_cancelButton.onClick.AddListener(OnCancelClicked);
    }

    public void ShowPopupRedeemCode(Action onClose)
    {
        m_onClose = onClose;

        if (m_codeInput != null)
        {
            m_codeInput.text = "";
            m_codeInput.ActivateInputField();
        }

        SetResultText("", m_colorSuccess);
        if (m_confirmButton != null) m_confirmButton.SetInteractable(true);
        base.ShowPopup();
    }

    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_codeInput == null || string.IsNullOrEmpty(m_codeInput.text) == true) return;

        m_confirmButton.SetInteractable(false);  // 중복 클릭 방지

        var request = new RedeemCodeRequest { code = m_codeInput.text };
        NetworkManager.Instance.RedeemCode(request, OnRedeemResponse);
    }

    private void OnRedeemResponse(ApiResponse<RedeemCodeResponse> response)
    {
        if (m_confirmButton != null) m_confirmButton.SetInteractable(true);

        if (response == null || response.errorCode != 0)
        {
            SetResultText(ErrorCodeMapping.GetMessage(response != null ? response.errorCode : 0), m_colorError);
            return;
        }

        Commander currentCommander = DataManager.Instance.m_currentCommander;
        if (currentCommander != null)
        {
            currentCommander.UpdateCommanderLevel(response.data.commanderLevel);
            currentCommander.UpdateExp(response.data.exp);
        }

        SetResultText(LocalizationManager.Instance.Get("UIPopupRedeemCode_Success"), m_colorSuccess);
    }

    private void OnCancelClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClose != null) m_onClose.Invoke();
    }

    private void SetResultText(string message, Color color)
    {
        if (m_resultText == null) return;
        m_resultText.text = message;
        m_resultText.color = color;
    }
}
