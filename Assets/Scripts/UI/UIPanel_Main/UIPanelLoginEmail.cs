using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIPanelLoginEmail : UIPanelBase
{
   [SerializeField] private TMP_InputField m_emailInput;
   [SerializeField] private TMP_InputField m_passwordInput;

   [SerializeField] private Button m_backButton;
   [SerializeField] private Button m_registerButton;
   [SerializeField] private Button m_loginButton;

   [SerializeField] private TMP_Text m_resultText;

   private UIMain m_uiMain;

   public override void InitializeUIPanel()
   {
      if (m_backButton != null)
         m_backButton.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelLoginType"));

      if (m_registerButton != null)
         //m_registerButton.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelRegisterEmail"));
         m_registerButton.onClick.AddListener(EmailRegister);

      if (m_loginButton != null)
         m_loginButton.onClick.AddListener(EmailLogin);


      if (SceneManager.GetActiveScene().name == "MainScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uiMain);

#if UNITY_EDITOR
      // 에디터에서만 테스트용 이메일/비밀번호 자동 입력
      if (m_emailInput != null)
         m_emailInput.text = "gsyan5@naver.com";
      if (m_passwordInput != null)
         m_passwordInput.text = "12345678";
#endif
   }


   private void EmailRegister()
   {
      string email = m_emailInput.text;
      string password = m_passwordInput.text;

      if (string.IsNullOrEmpty(email))
      {
         m_resultText.text = "Invalid Email";
         return;
      }
      if (string.IsNullOrEmpty(password))
      {
         m_resultText.text = "Invalid password";
         return;
      }

      if (m_resultText != null)
         m_resultText.text = "Processing...";

      NetworkManager.Instance.Register(email, password, (response) => {
         ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
         string message = "";
         if (errorCode == ServerErrorCode.SUCCESS)
         {
               message = ErrorCodeMapping.Messages[errorCode];
               Debug.Log($"Email Registration successful: {message}");

               //EmailLogin(email, password); // Attempt automatic login
         }
         else
         {
               message = ErrorCodeMapping.GetMessage(response.errorCode);
               Debug.LogError($"Email Registration failed - ErrorCode: {errorCode}, Message: {message}");
         }
         if (m_resultText != null)
               m_resultText.text = $"Result: {message}";
      });
   }

   private void EmailLogin()
   {
      string email = m_emailInput.text;
      string password = m_passwordInput.text;

      if (string.IsNullOrEmpty(email))
      {
         m_resultText.text = "Invalid Email";
         return;
      }
      if (string.IsNullOrEmpty(password))
      {
         m_resultText.text = "Invalid password";
         return;
      }

      if (m_resultText != null)
         m_resultText.text = "Processing...";

      NetworkManager.Instance.Login(email, password, (response) => {
         ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
         string message = "";
         if (errorCode == ServerErrorCode.SUCCESS)
         {
               message = ErrorCodeMapping.Messages[errorCode];
               Debug.Log($"Email Login successful: {message}");
               m_uiMain.GetCharacters();
         }
         else
         {
               message = ErrorCodeMapping.GetMessage(response.errorCode);
               Debug.LogError($"Email Login failed - ErrorCode: {errorCode}, Message: {message}");
         }
         if (m_resultText != null)
               m_resultText.text = $"Result: {message}";
      });
   }
}
