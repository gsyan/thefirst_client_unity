// 함체 선택 팝업(UIHullPickerView) — 배치 가능한 함체 1행. 클릭으로 선택
using UnityEngine;
using UnityEngine.UI;

public class UIAvailableHullRow : MonoBehaviour
{
    [SerializeField] private RowLabelValue m_nameRow;
    [SerializeField] private RowLabelValue m_costRow; // 라벨은 "배치 코스트", 값은 이 함체 자신의 코스트(모듈은 지휘력에 영향 없음)
    [SerializeField] private TMPro.TMP_Text m_starRatingText; // 우측 하단 성급 표시(1~5)
    [SerializeField] private Button m_button; // 클릭(선택) — 눌림 시각 피드백까지 기본 제공
    [SerializeField] private Image m_selectedImage; // 이 함체가 현재 선택 상태임을 표시 — 색 변경이 아니라 오브젝트 자체를 켜고 끔

    [Header("티어4+ 언락 상태 표시 전용(버튼 없음 — 언락 버튼은 UIHullPickerView 패널 공용 버튼으로 이동)")]
    [SerializeField] private GameObject m_lockedRoot; // 잠김 상태에서만 켜지는 잠금 아이콘 등 시각 표시용

    [Header("M7 — 이 아키타입이 함대 내 다른 슬롯에 이미 배치돼 있을 때만 표시(이 슬롯 자신의 현재 함체는 제외)")]
    [SerializeField] private GameObject m_settedImage;
    [SerializeField] private TMPro.TMP_Text m_settedText; // "배치됨" 고정 라벨 — Awake에서 1회만 세팅

    private ModuleData m_hull;
    private System.Action<ModuleData> m_onClick;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnButtonClicked);
        if (m_selectedImage != null)
            m_selectedImage.gameObject.SetActive(false);
        if (m_settedText != null)
            CommonUtility.SetUILocText(m_settedText, "UIAvailableHullRow_Setted");
    }

    // 이 함체가 현재 선택 상태임을 표시 — 색 변경이 아니라 오브젝트 자체를 켜고 끔
    public void SetSelectedAvailableHullRow(bool selected)
    {
        if (m_selectedImage == null) return;
        m_selectedImage.gameObject.SetActive(selected);
    }

    // placementCost: 이 함체를 이 슬롯에 적용했을 때 추가로 점유하는 지휘력(이 함체 자신의 코스트, 모듈은 포함되지 않음)
    // isOverCommandPowerIfSelected: 이 함체를 선택하면 지휘력 최대치를 넘기게 되는지 — 경고색 표시용
    // isLocked==true면 선택 불가 상태를 시각적으로만 표시(언락은 UIHullPickerView 패널 공용 버튼이 담당)
    // isPlacedElsewhere==true면 M7(아키타입 1척 제한)에 걸려 이 슬롯엔 배치 불가 — "배치됨" 표시만, 클릭/프리뷰는 그대로 허용(실제 배치 시도하면 서버가 거부)
    public void Setup(ModuleData hull, int placementCost, bool isOverCommandPowerIfSelected, bool isLocked, bool isPlacedElsewhere, System.Action<ModuleData> onClick)
    {
        gameObject.SetActive(true);
        m_hull = hull;
        m_onClick = onClick;

        // 함선 이름은 moduleSubType 이름을 UI.csv 로컬라이즈 키로 그대로 사용(별도 displayNameKey 없음)
        if (m_nameRow != null)
            m_nameRow.SetRow("UIAvailableHullRow_Name", CommonUtility.BuildHullDisplayName(hull.moduleSubType), rawValue: true);
        if (m_costRow != null)
        {
            m_costRow.SetRow("UIAvailableHullRow_Cost", CommonUtility.FormatNumber(placementCost), rawValue: true);
            m_costRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPowerIfSelected == true ? "Text.Warning" : "Text.Dark1"));
        }
        if (m_starRatingText != null)
            m_starRatingText.text = new string('★', Mathf.Max(CommonUtility.ParseTier(hull.moduleSubType), 0));

        // 잠긴 함체도 클릭은 허용(우측 패널에서 프리뷰/스펙 확인 가능) — 실제 확정(Confirm)만 UIHullPickerView가 별도로 막음
        if (m_lockedRoot != null)
            m_lockedRoot.SetActive(isLocked);
        if (m_settedImage != null)
            m_settedImage.SetActive(isPlacedElsewhere);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClick != null) m_onClick(m_hull);
    }
}
