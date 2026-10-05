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
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [Header("색상")]
    [SerializeField] private Color m_colorSuccess = Color.green;
    [SerializeField] private Color m_colorError   = Color.red;

    private Action m_onClose;

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)  m_cancelButton.onClick.AddListener(OnCancelClicked);

        ApplyStaticLocalization();
    }

    // 확인/취소 버튼 라벨 로컬라이즈 — CommonUtility.SetUILocText가 각 버튼에 붙어있는 LocalizeStringEvent를 이 키로 재설정함
    private void ApplyStaticLocalization()
    {
        TMP_Text confirmButtonText = m_confirmButton != null ? m_confirmButton.GetComponentInChildren<TMP_Text>() : null;
        if (confirmButtonText != null)
            CommonUtility.SetUILocText(confirmButtonText, "UI_Confirm");

        TMP_Text cancelButtonText = m_cancelButton != null ? m_cancelButton.GetComponentInChildren<TMP_Text>() : null;
        if (cancelButtonText != null)
            CommonUtility.SetUILocText(cancelButtonText, "UI_Cancel");
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
        if (m_confirmButton != null) m_confirmButton.interactable = true;
        base.ShowPopup();
    }

    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_codeInput == null || string.IsNullOrEmpty(m_codeInput.text) == true) return;

        m_confirmButton.interactable = false;  // 중복 클릭 방지

        var request = new RedeemCodeRequest { code = m_codeInput.text };
        NetworkManager.Instance.RedeemCode(request, OnRedeemResponse);
    }

    private void OnRedeemResponse(ApiResponse<RedeemCodeResponse> response)
    {
        if (m_confirmButton != null) m_confirmButton.interactable = true;

        if (response == null || response.errorCode != 0)
        {
            SetResultText(ErrorCodeMapping.GetMessage(response != null ? response.errorCode : 0), m_colorError);
            return;
        }

        Commander currentCommander = DataManager.Instance.m_currentCommander;
        if (currentCommander != null)
        {
            currentCommander.UpdateExp(response.data.exp);
            currentCommander.UpdateCommanderLevel(response.data.commanderLevel);
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
