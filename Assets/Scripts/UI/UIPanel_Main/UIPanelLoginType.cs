using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIPanelLoginType : UIPanelBase
{
   [SerializeField] private Button m_emailLoginButton;
   [SerializeField] private Button m_googleLoginButton;
   [SerializeField] private Button m_guestLoginButton;

   [SerializeField] private TMP_Text m_resultText;

   private UIMain m_uiMain;


   public override void InitializeUIPanel()
   {
      if (m_emailLoginButton != null)
         m_emailLoginButton.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelEmailLogin"));
      if (m_googleLoginButton != null)
         m_googleLoginButton.onClick.AddListener(() => GoogleLogin());
      if (m_guestLoginButton != null)
         m_guestLoginButton.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelGuestLogin"));

      if (SceneManager.GetActiveScene().name == "MainScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uiMain);
   }

   private void GoogleLogin()
   {
      UIManager.Instance.ShowMainPanel();

      if (m_resultText != null)
         m_resultText.text = "Processing Google Login...";

      NetworkManager.Instance.GoogleLogin((response) => {
         ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
         string message = "";
         if (errorCode == ServerErrorCode.SUCCESS)
         {
               message = ErrorCodeMapping.Messages[errorCode];
               Debug.Log($"Google Login successful: {message}");
               Debug.Log($"Access Token received: {response.data?.accessToken}");

               m_uiMain.GetCharacters();
         }
         else
         {
               message = ErrorCodeMapping.GetMessage(response.errorCode);
               Debug.LogError($"Google Login failed - ErrorCode: {errorCode}, Message: {message}");

               UIManager.Instance.ShowPanel("UIpanelLoginType");
         }
         if (m_resultText != null)
               m_resultText.text = $"Result: {message}";
      });
   }

   public override void OnShowUIPanel()
   {
      
   }

   public override void OnHideUIPanel()
   {
      
   }

}
