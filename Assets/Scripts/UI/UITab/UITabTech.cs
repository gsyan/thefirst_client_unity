// 기술레벨 탭 UI — 현재 기술레벨/함선 수 표시, 기술 포인트 게이지 표시
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabTech : UITabBase
{
    [SerializeField] private TMP_Text m_techLevelText;
    [SerializeField] private TMP_Text m_shipCountText;
    [SerializeField] private Transform m_shipImages;
    [SerializeField] private TMP_Text m_moduleGradeLimitText;    
    [SerializeField] private Image m_techPointGaugeImage;   // 기술 포인트 게이지 Fill Image
    [SerializeField] private TMP_Text m_techPointGaugeText; // 기술 포인트 게이지 위 텍스트
    [SerializeField] private TMP_Text m_nextLevelText;
    [SerializeField] private TMP_Text m_nextLevelShipCountText;
    [SerializeField] private TMP_Text m_nextModuleGradeText;    
    
    private static readonly Vector2 k_sizeActive   = new Vector2(10f, 50f);
    private static readonly Vector2 k_sizeInactive = new Vector2(10f, 25f);

    private Color m_colorActive;
    private Color m_colorInactive;

    private Image[] m_shipSlots;


    public override void InitializeUITab()
    {
        InitializeUITabTech();
    }

    private void InitializeUITabTech()
    {
        m_colorActive   = CommonUtility.PaletteColor("GeneralBright1");
        m_colorInactive = CommonUtility.PaletteColor("GeneralDark1");

        if (DataManager.Instance.m_currentCommander == null || ObjectManager.Instance.m_myFleet == null) return;

        if (m_shipImages != null)
        {
            m_shipSlots = new Image[m_shipImages.childCount];
            for (int i = 0; i < m_shipImages.childCount; i++)
                m_shipSlots[i] = m_shipImages.GetChild(i).GetComponent<Image>();
        }

        EventManager.Subscribe_TechLevelChanged(OnTechLevelChanged);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        HideTabButtons();
        UpdateTechLevelDisplay();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        RefreshTabButtons();
    }

    // ── Ship Slots ────────────────────────────────────────────────────

    private void RefreshShipSlots(int activeCount)
    {
        if (m_shipSlots == null) return;
        for (int i = 0; i < m_shipSlots.Length; i++)
        {
            bool active = i < activeCount;
            m_shipSlots[i].color = active ? m_colorActive : m_colorInactive;
            m_shipSlots[i].rectTransform.sizeDelta = active ? k_sizeActive : k_sizeInactive;
        }
    }

    // ── Tech Level ────────────────────────────────────────────────────

    private void UpdateTechLevelDisplay()
    {
        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int currentLevel = commander.GetTechLevel();
        int maxShips = DataManager.Instance.m_dataTableTechLevel.GetShipCount(currentLevel);
        TechLevelData nextNode = GetNextTechLevelNode(commander);

        // 기술레벨 요약: 레벨 / 자원 보관 캡 / 최대 함선 수
        if (m_techLevelText != null)
            m_techLevelText.text = $"{currentLevel}";

        if (m_shipCountText != null)
            m_shipCountText.text = $"{maxShips}";

        if (m_moduleGradeLimitText != null)
            m_moduleGradeLimitText.text = $"T.{currentLevel}";

        RefreshShipSlots(maxShips);

        if (m_nextLevelText != null)
        {
            if (nextNode != null)
            {
                m_nextLevelText.text = string.Format(LocalizationManager.Instance.Get("UITabTech_NextUnlockTitle"), nextNode.targetTechLevel);
                m_nextModuleGradeText.text = string.Format(LocalizationManager.Instance.Get("UITabTech_NextModuleGrade"), nextNode.targetTechLevel);
                m_nextLevelShipCountText.text = string.Format(LocalizationManager.Instance.Get("UITabTech_NextUnlockShipCount"), nextNode.shipCount);
            }
            else
            {
                m_nextLevelText.text = LocalizationManager.Instance.Get("LevelupButtonTextMax");
                m_nextModuleGradeText.gameObject.SetActive(false);
                m_nextLevelShipCountText.gameObject.SetActive(false);
            }
                
        }

        int currentTechPoint = commander.GetTechPoint();
        int currentLevelRequired = DataManager.Instance.m_dataTableTechLevel.GetRequiredTechPoint(currentLevel);
        int progressCurrent = currentTechPoint - currentLevelRequired;

        if (m_techPointGaugeText != null)
        {
            if (nextNode != null)
            {
                int progressRequired = nextNode.requiredTechPoint - currentLevelRequired;
                int remaining = progressRequired - progressCurrent;
                m_techPointGaugeText.text = LocalizationManager.Instance.Get("UITabTech_PointsToNextLevel", remaining);
            }
            else
                m_techPointGaugeText.text = string.Empty;
        }

        if (m_techPointGaugeImage != null)
        {
            if (nextNode != null)
            {
                int progressRequired = nextNode.requiredTechPoint - currentLevelRequired;
                float ratio = progressRequired > 0 ? (float)progressCurrent / progressRequired : 0f;
                m_techPointGaugeImage.fillAmount = Mathf.Clamp01(ratio);
            }
            else
            {
                m_techPointGaugeImage.fillAmount = 1f;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_techLevelText.transform as RectTransform);
    }

    private TechLevelData GetNextTechLevelNode(Commander commander)
    {
        int currentLevel = commander.GetTechLevel();
        var techList = DataManager.Instance.m_dataTableTechLevel.GetTechLevelDataList();
        for (int i = 0; i < techList.Count; i++)
        {
            if (techList[i].targetTechLevel > currentLevel)
                return techList[i];
        }
        return null;
    }

    private void OnTechLevelChanged(int techLevel)
    {
        UpdateTechLevelDisplay();
    }

}

