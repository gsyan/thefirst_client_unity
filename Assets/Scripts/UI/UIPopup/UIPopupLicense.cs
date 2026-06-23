// 외부 라이센스 고지 팝업 — 스크롤 가능한 텍스트로 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupLicense : UIPopupBase
{
    [SerializeField] private TMP_Text     m_titleText;
    [SerializeField] private TMP_Text     m_licenseText;
    [SerializeField] private ScrollRect   m_scrollView;
    [SerializeField] private Button       m_closeButton;
    [SerializeField] private Button       m_backgroundButton;

    private static readonly string LICENSE_CONTENT =
        "=== 외부 라이센스 고지 ===\n\n" +

        "[ game-icons.net ]\n" +
        "License: CC BY 3.0\n" +
        "Authors: Lorc, Delapouite, and contributors\n" +
        "https://game-icons.net\n\n" +

        "[ Noto Sans (KR / JP / SC / TC) ]\n" +
        "License: SIL Open Font License 1.1 (OFL)\n" +
        "Copyright: Google LLC\n" +
        "https://fonts.google.com/noto\n\n" +

        "[ Liberation Sans ]\n" +
        "License: SIL Open Font License 1.1 (OFL)\n" +
        "Copyright: Red Hat, Inc.\n\n" +

        "[ Quick Outline ]\n" +
        "Copyright: Chris Nolet, 2018\n\n" +

        "[ Space X-plorers (BGM) ]\n" +
        "License: CC BY 4.0\n" +
        "Author: Kevin MacLeod (incompetech.com)\n" +
        "https://incompetech.com/music/royalty-free/music.html\n\n" +

        "[ Sound Effects (FX) ]\n" +
        "License: Pixabay Content License (No attribution required)\n" +
        "https://pixabay.com/sound-effects/\n\n" +

        
        
        
        "------------------------\n\n";

        

    protected override void Awake()
    {
        base.Awake();
        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(OnCloseClicked);
        if (m_backgroundButton != null)
            m_backgroundButton.onClick.AddListener(OnCloseClicked);
    }

    public void ShowPopupLicense(System.Action onClose)
    {
        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UITabSettings_License");
        if (m_licenseText != null)
            m_licenseText.text = LICENSE_CONTENT;

        m_onClose = onClose;
        base.ShowPopup();

        if (m_licenseText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_licenseText.transform.parent as RectTransform);
            if (m_scrollView != null)
                m_scrollView.verticalNormalizedPosition = 1f;
        }
    }

    private System.Action m_onClose;

    private void OnCloseClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_onClose?.Invoke();
    }
}
