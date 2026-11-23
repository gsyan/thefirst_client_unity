// 갤럭시뷰(탐사 그리드) 우측 상단 함선 체력 목록의 1행 — 체력 게이지 위에 함선 이름을 겹쳐 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGridShipHealthRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_nameText;    // 게이지 위에 겹쳐 표시되는 함선 이름
    [SerializeField] private Image m_healthGauge;    // 체력 게이지(Image Type=Filled, Fill Method=Horizontal)

    private const float k_destroyedNameAlpha = 0.4f; // 파괴된 함선은 행을 남기되 이름을 흐리게

    public void SetShipName(string shipName)
    {
        gameObject.SetActive(true);
        if (m_nameText != null)
            m_nameText.text = shipName;
    }

    // isDestroyed면 게이지 0 + 이름 흐리게
    public void SetHealthRatio(float healthRatio, bool isDestroyed)
    {
        float shownRatio = isDestroyed == true ? 0f : Mathf.Clamp01(healthRatio);
        if (m_healthGauge != null)
            m_healthGauge.fillAmount = shownRatio;
        if (m_nameText != null)
            m_nameText.alpha = isDestroyed == true ? k_destroyedNameAlpha : 1f;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
