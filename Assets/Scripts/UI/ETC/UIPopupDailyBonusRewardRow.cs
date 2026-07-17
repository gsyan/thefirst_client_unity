using UnityEngine;
using UnityEngine.UI;

// 일일 보상 캘린더 셀의 보상 한 줄(체크마크 + 아이콘/텍스트 + VIP 아이콘) — 프리팹에 미리 배치된 구조를 그대로 사용
// VIP 아이콘은 VIP 줄에만 배치되어 있고 일반 줄에는 없음(null) — 호출부에서 null 체크 필요
public class UIPopupDailyBonusRewardRow : MonoBehaviour
{
    [SerializeField] private Image        m_checkmark;
    [SerializeField] private RowImageText m_row;
    [SerializeField] private Image        m_vipIcon;

    public Image        GetCheckmark() { return m_checkmark; }
    public RowImageText GetRow()       { return m_row; }
    public Image        GetVipIcon()   { return m_vipIcon; }
}
