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
         m_guestLoginButton.onClick.AddListener(() => GuestLogin());

      if (SceneManager.GetActiveScene().name == "MainScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uiMain);
   }

   private void GoogleLogin()
   {
      gameObject.SetActive(false);

      NetworkManager.Instance.GoogleLogin((response) => {
         ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
         if (errorCode == ServerErrorCode.SUCCESS)
         {
               m_uiMain.GetCommanders();
         }
         else
         {
               Debug.LogError($"Google Login failed - ErrorCode: {errorCode}");
               gameObject.SetActive(true);
         }
      });
   }

   private void GuestLogin()
   {
      gameObject.SetActive(false);

      NetworkManager.Instance.GuestLogin((response) => {
         ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
         if (errorCode == ServerErrorCode.SUCCESS)
         {
               m_uiMain.GetCommanders();
         }
         else
         {
               Debug.LogError($"Guest Login failed - ErrorCode: {errorCode}");
               gameObject.SetActive(true);
         }
      });
   }

   public override void OnShowUIPanel()
   {
      
   }

   public override void OnHideUIPanel()
   {
      
   }

}
