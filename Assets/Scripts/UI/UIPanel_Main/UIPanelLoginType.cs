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
         m_emailLoginButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); UIManager.Instance.ShowPanel("UIPanelEmailLogin"); });
      if (m_googleLoginButton != null)
         m_googleLoginButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); GoogleLogin(); });
      if (m_guestLoginButton != null)
         m_guestLoginButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); GuestLogin(); });

      if (SceneManager.GetActiveScene().name == "MainScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uiMain);
   }

   private void GoogleLogin()
   {
      SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
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
      SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
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
               if (m_resultText != null)
                  m_resultText.text = ErrorCodeMapping.GetMessage(response.errorCode);
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
