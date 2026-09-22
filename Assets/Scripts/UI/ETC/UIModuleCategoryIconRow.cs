// 성능 컬럼 모듈 현황 아이콘 1줄(빔/미사일/격납고/etc(실드+요격체) 컨테이너에 부착) — 칸 수는 프리팹에 미리 배치된 자식 Image 개수로 결정(런타임 Instantiate 없음)
// 아이콘 순서 = 모듈 슬롯 인덱스와 1:1. 지원하지 않는 슬롯은 숨기고, 지원하지만 미장착이면 어둡게, 장착돼 있으면 밝게 표시
// 각 아이콘엔 Button이 달려있어 클릭 시 로컬 인덱스를 콜백으로 알림(장착 여부 판단/화면 전환은 호출부(UIPanelFleet) 책임)
// 각 아이콘의 자식 TMP_Text에는 장착된 모듈의 티어를 "T{N}"으로 표시(미장착이면 숨김)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIModuleCategoryIconRow : MonoBehaviour
{
    private const int k_tierTextCacheSize = 32;
    private static readonly string[] s_tierTextCache = new string[k_tierTextCacheSize];

    private Image[] m_icons;
    private Button[] m_buttons;
    private TMP_Text[] m_tierTexts;
    private Color m_colorInstalled;
    private Color m_colorNotInstalled;

    private System.Action<int> m_onIconClicked;

    private void Awake()
    {
        m_icons = GetComponentsInChildren<Image>(true);
        m_colorInstalled = Color.white;
        m_colorNotInstalled = new Color32(128, 128, 128, 255);

        m_buttons = new Button[m_icons.Length];
        m_tierTexts = new TMP_Text[m_icons.Length];
        for (int i = 0; i < m_icons.Length; i++)
        {
            m_tierTexts[i] = m_icons[i].GetComponentInChildren<TMP_Text>(true);

            m_buttons[i] = m_icons[i].GetComponent<Button>();
            if (m_buttons[i] == null) continue;

            int iconIndex = i; // 클로저 캡처용 로컬 복사
            m_buttons[i].onClick.AddListener(() => OnIconClicked(iconIndex));
        }
    }

    // 티어 문자열은 자주 갱신되므로 값별로 한 번만 만들어 재사용(GC 방지)
    private static string GetTierText(int tier)
    {
        if (tier < 0 || tier >= k_tierTextCacheSize) return "T" + tier;

        if (s_tierTextCache[tier] == null)
            s_tierTextCache[tier] = "T" + tier;
        return s_tierTextCache[tier];
    }

    // 이 행의 아이콘 클릭 콜백 등록 — 1회성(초기화 시점에 등록), 인자는 이 행 안에서의 로컬 인덱스
    public void SetOnIconClicked(System.Action<int> onIconClicked)
    {
        m_onIconClicked = onIconClicked;
    }

    private void OnIconClicked(int iconIndex)
    {
        if (m_onIconClicked != null) m_onIconClicked(iconIndex);
    }

    // supported[i]=false면 그 인덱스 아이콘은 숨김. supported[i]=true면 installed[i] 여부로 밝기만 다르게.
    // clickable=false면 보이는 아이콘도 클릭 불가(전투 중/읽기전용 등)로 시각적으로도 비활성화
    // tiers[i] = 장착된 모듈의 티어(미장착이면 0) — 장착돼 있고 티어가 1 이상일 때만 티어 텍스트를 표시
    public void SetStatus(bool[] supported, bool[] installed, int[] tiers, bool clickable)
    {
        for (int i = 0; i < m_icons.Length; i++)
        {
            bool isSupported = i < supported.Length && supported[i] == true;
            m_icons[i].gameObject.SetActive(isSupported);
            if (isSupported == true)
            {
                bool isInstalled = i < installed.Length && installed[i] == true;
                m_icons[i].color = isInstalled == true ? m_colorInstalled : m_colorNotInstalled;
                if (m_buttons[i] != null)
                    m_buttons[i].interactable = clickable;

                int tier = i < tiers.Length ? tiers[i] : 0;
                bool showTier = isInstalled == true && tier > 0;
                if (m_tierTexts[i] != null)
                {
                    m_tierTexts[i].gameObject.SetActive(showTier);
                    if (showTier == true)
                        m_tierTexts[i].text = GetTierText(tier);
                }
            }
        }
    }

    // 선택된 함선이 없을 때 — 이 줄의 아이콘 전부 숨김
    public void Clear()
    {
        for (int i = 0; i < m_icons.Length; i++)
            m_icons[i].gameObject.SetActive(false);
    }
}
