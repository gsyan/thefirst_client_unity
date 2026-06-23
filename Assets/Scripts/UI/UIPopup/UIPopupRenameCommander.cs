// 커맨더 이름 변경 팝업: 실시간 포맷 검사(클라) + 디바운스 서버 유효성 검사 + 남은 횟수 표시
using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupRenameCommander : UIPopupBase
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField m_nameInput;
    [SerializeField] private TMP_Text m_validationText;
    [SerializeField] private UIButtonHasChildren m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [Header("색상")]
    [SerializeField] private Color m_colorAvailable = Color.green;
    [SerializeField] private Color m_colorError     = Color.red;
    [SerializeField] private Color m_colorChecking  = Color.yellow;

    // 한글·영문·숫자 2~16자, 공백·특수문자 불가
    private static readonly Regex s_nameRegex = new Regex(@"^[a-zA-Z0-9가-힣]{2,16}$", RegexOptions.Compiled);
    private const float DebounceDelay = 0.5f;

    private DataTableForbiddenWords m_forbiddenWords;

    private enum EValidationState { Idle, Checking, Available, Error }

    private Action m_onRenameSuccess;
    private Action m_onClose;
    private Coroutine m_debounceCoroutine;
    private bool m_isNameValid;

    protected override void Awake()
    {
        base.Awake();
        m_forbiddenWords = ResourceManager.Instance.Load<DataTableForbiddenWords>("DataTable/DataTableForbiddenWords");
        if (m_confirmButton != null) m_confirmButton.GetButton().onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)  m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_nameInput != null)
        {
            m_nameInput.onValueChanged.AddListener(OnNameInputChanged);
            m_nameInput.characterLimit = 16;
        }
    }

    public void ShowPopupRenameCommander(Action onClose, Action onRenameSuccess = null)
    {
        m_onClose          = onClose;
        m_onRenameSuccess  = onRenameSuccess;
        m_isNameValid      = false;

        if (m_nameInput != null)
        {
            m_nameInput.text = "";
            m_nameInput.ActivateInputField();
        }

        SetValidationState(EValidationState.Idle, "");
        UpdateConfirmButton();
        base.ShowPopup();
    }

    // ── 입력 변경 ─────────────────────────────────────────────────
    private void OnNameInputChanged(string value)
    {
        m_isNameValid = false;
        UpdateConfirmButton();

        if (m_debounceCoroutine != null)
            StopCoroutine(m_debounceCoroutine);

        if (string.IsNullOrEmpty(value))
        {
            SetValidationState(EValidationState.Idle, "");
            return;
        }

        if (s_nameRegex.IsMatch(value) == false)
        {
            SetValidationState(EValidationState.Error,
                LocalizationManager.Instance.Get("ui_rename_char_format_err"));
            return;
        }

        if (m_forbiddenWords != null && m_forbiddenWords.ContainsForbiddenWord(value) == true)
        {
            SetValidationState(EValidationState.Error,
                LocalizationManager.Instance.Get("ui_rename_char_profanity"));
            return;
        }

        // 포맷 OK → 디바운스 후 서버 요청
        SetValidationState(EValidationState.Checking,
            LocalizationManager.Instance.Get("ui_rename_char_checking"));
        m_debounceCoroutine = StartCoroutine(ValidateDebounce(value));
    }

    private IEnumerator ValidateDebounce(string name)
    {
        yield return new WaitForSeconds(DebounceDelay);
        NetworkManager.Instance.ValidateCommanderName(name, OnValidateResponse);
    }

    private void OnValidateResponse(ApiResponse<bool> response)
    {
        if (response == null || response.errorCode != 0)
        {
            string msg;
            if (response != null && response.errorCode == (int)ServerErrorCode.CHARACTER_VALIDATE_NAME_DUPLICATE)
                msg = LocalizationManager.Instance.Get("ui_rename_char_duplicate");
            else if (response != null && response.errorCode == (int)ServerErrorCode.CHARACTER_VALIDATE_NAME_PROFANITY)
                msg = LocalizationManager.Instance.Get("ui_rename_char_profanity");
            else
                msg = LocalizationManager.Instance.Get("ui_rename_char_format_err");

            SetValidationState(EValidationState.Error, msg);
            m_isNameValid = false;
        }
        else
        {
            SetValidationState(EValidationState.Available,
                LocalizationManager.Instance.Get("ui_rename_char_available"));
            m_isNameValid = true;
        }

        UpdateConfirmButton();
    }

    // ── 확인 클릭 ─────────────────────────────────────────────────
    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_isNameValid == false) return;

        m_confirmButton.SetInteractable(false);  // 중복 클릭 방지

        var request = new CommanderRenameRequest { newName = m_nameInput.text };
        NetworkManager.Instance.RenameCommander(request, OnRenameResponse);
    }

    private void OnRenameResponse(ApiResponse<CommanderRenameResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            // 실패 케이스 — 남은 횟수 부족 등 서버 측 최종 거부
            SetValidationState(EValidationState.Error,
                LocalizationManager.Instance.Get("ui_rename_char_format_err"));
            m_isNameValid = false;
            UpdateConfirmButton();
            return;
        }

        Commander currentCommander = DataManager.Instance.m_currentCommander;
        if (currentCommander != null)
            currentCommander.UpdateCommanderName(response.data.commanderName, response.data.nameChangeCount);

        if (m_onRenameSuccess != null) m_onRenameSuccess.Invoke();
        if (m_onClose != null) m_onClose.Invoke();
    }

    // ── 취소 ──────────────────────────────────────────────────────
    private void OnCancelClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_debounceCoroutine != null)
            StopCoroutine(m_debounceCoroutine);

        if (m_onClose != null) m_onClose.Invoke();
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────
    private void SetValidationState(EValidationState state, string message)
    {
        if (m_validationText == null) return;
        m_validationText.text = message;

        Color color;
        if (state == EValidationState.Checking)       color = m_colorChecking;
        else if (state == EValidationState.Available) color = m_colorAvailable;
        else if (state == EValidationState.Error)     color = m_colorError;
        else                                           color = Color.white;

        m_validationText.color = color;
    }

    private void UpdateConfirmButton()
    {
        if (m_confirmButton == null) return;
        Commander currentCommander = DataManager.Instance.m_currentCommander;
        CommanderInfo info = (currentCommander != null) ? currentCommander.m_commanderInfo : null;
        int remaining = (info != null) ? info.nameChangeCount : 0;
        m_confirmButton.SetInteractable(m_isNameValid && remaining > 0);
    }
}
