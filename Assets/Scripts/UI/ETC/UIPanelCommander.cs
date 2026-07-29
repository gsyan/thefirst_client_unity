// 기술레벨 패널 — 현재 기술레벨/함선 수 표시, 기술 포인트 게이지 표시. UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPanelCommander : UIPanelBase
{
    [SerializeField] private TMP_Text m_commanderLevelText;
    [SerializeField] private TMP_Text m_shipCountText;
    [SerializeField] private Transform m_shipImages;
    [SerializeField] private TMP_Text m_commandPowerText;
    [SerializeField] private Image m_expGaugeImage;       // 기술 포인트 게이지 Fill Image
    [SerializeField] private TMP_Text m_expGaugeText;           // 경험치 게이지 위 텍스트
    [SerializeField] private TMP_Text m_nextLevelShipCountText;

    private static readonly Vector2 k_sizeActive   = new Vector2(10f, 50f);
    private static readonly Vector2 k_sizeInactive = new Vector2(10f, 25f);

    private Color m_colorActive;
    private Color m_colorInactive;

    private Image[] m_shipSlots;

    public override void InitializeUIPanel()
    {
        m_colorActive   = CommonUtility.PaletteColor("GeneralBright1");
        m_colorInactive = CommonUtility.PaletteColor("GeneralDark1");

        if (m_shipImages != null)
        {
            m_shipSlots = new Image[m_shipImages.childCount];
            for (int i = 0; i < m_shipImages.childCount; i++)
                m_shipSlots[i] = m_shipImages.GetChild(i).GetComponent<Image>();
        }

        // 패널 초기화 시점에 함대가 아직 스폰되지 않았을 수 있음 — 스폰 시점에 뒤늦게 갱신
        EventManager.Subscribe_MyFleetSet(OnMyFleetSet);
        EventManager.Subscribe_CommanderLevelChanged(OnCommanderLevelChanged);

        // 이미 함대가 존재하면 즉시 갱신
        if (DataManager.Instance.m_currentCommander != null && ObjectManager.Instance.GetMyFleet() != null)
            UpdateCommanderLevelDisplay();
    }

    // 함대 스폰/교체 시 호출 — 매번 패널 열 때 체크하지 않아도 되도록 이벤트로 처리
    private void OnMyFleetSet()
    {
        UpdateCommanderLevelDisplay();
    }

    public override void OnShowUIPanel()
    {
        UpdateCommanderLevelDisplay();
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

    // ── Commander Level ────────────────────────────────────────────────────

    private void UpdateCommanderLevelDisplay()
    {
        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int currentLevel = commander.GetCommanderLevel();
        int maxShips = DataManager.Instance.m_dataTableCommander.GetShipCount(currentLevel);
        CommanderData nextNode = GetNextCommanderLevelNode(commander);

        // 기술레벨 요약: 레벨 / 자원 보관 캡 / 최대 함선 수
        if (m_commanderLevelText != null)
            m_commanderLevelText.text = $"{currentLevel}";

        if (m_shipCountText != null)
            m_shipCountText.text = $"{maxShips}";

        // 이 패널은 총량만 표시 — 사용량(배치 현황)은 함대편성 UI에서 별도로 보여줄 예정
        FleetComposition fleetComposition = DataManager.Instance.m_currentFleetComposition;
        if (m_commandPowerText != null)
        {
            if (fleetComposition != null)
            {
                m_commandPowerText.gameObject.SetActive(true);
                m_commandPowerText.text = $"{fleetComposition.GetMaxCommandPower()}";
            }
            else
            {
                m_commandPowerText.gameObject.SetActive(false);
            }
        }

        RefreshShipSlots(maxShips);

        if (nextNode != null)
        {
            m_nextLevelShipCountText.text = string.Format(LocalizationManager.Instance.Get("UITabTech_NextUnlockShipCount"), nextNode.shipCount);
        }
        else
        {
            m_nextLevelShipCountText.gameObject.SetActive(false);
        }

        int currentExp = commander.GetExp();
        int currentLevelRequired = DataManager.Instance.m_dataTableCommander.GetRequireExp(currentLevel);
        int progressCurrent = currentExp - currentLevelRequired;

        if (m_expGaugeText != null)
        {
            if (nextNode != null)
            {
                int progressRequired = nextNode.requireExp - currentLevelRequired;
                int remaining = progressRequired - progressCurrent;
                m_expGaugeText.text = LocalizationManager.Instance.Get("UITabCommander_PointsToNextLevel", remaining);
            }
            else
                m_expGaugeText.text = string.Empty;
        }

        if (m_expGaugeImage != null)
        {
            if (nextNode != null)
            {
                int progressRequired = nextNode.requireExp - currentLevelRequired;
                float ratio = progressRequired > 0 ? (float)progressCurrent / progressRequired : 0f;
                m_expGaugeImage.fillAmount = Mathf.Clamp01(ratio);
            }
            else
            {
                m_expGaugeImage.fillAmount = 1f;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commanderLevelText.transform as RectTransform);
    }

    private CommanderData GetNextCommanderLevelNode(Commander commander)
    {
        int currentLevel = commander.GetCommanderLevel();
        var levelList = DataManager.Instance.m_dataTableCommander.GetCommanderDataList();
        for (int i = 0; i < levelList.Count; i++)
        {
            if (levelList[i].commanderLevel > currentLevel)
                return levelList[i];
        }
        return null;
    }

    private void OnCommanderLevelChanged(int commanderLevel)
    {
        UpdateCommanderLevelDisplay();
    }

}
