// 함선 프리셋 상세 스탯 팝업 — 함대편성 UI에서 배치가능 프리셋 클릭 시 표시
// 항목 구성(게이지/값 표시)은 ShipStatGaugeBuilder를 공유 사용(성능 컬럼에서 배치된 함선 클릭 시와 동일 로직)
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupShipStats : UIPopupBase
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private UIStatGaugeRow m_rowPrefab;
    [SerializeField] private RectTransform m_rowContainer;
    [SerializeField] private Button m_closeButton;

    private readonly List<UIStatGaugeRow> m_rows = new();
    private System.Action m_onClose;

    protected override void Awake()
    {
        base.Awake();
        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(OnCloseClicked);
    }

    public void ShowPopupShipStats(ShipPresetData preset, System.Action onClose = null)
    {
        m_onClose = onClose;

        // 함선 이름 로컬라이즈는 아직 미정 — 프리셋 코드(presetId)를 그대로 표시
        if (m_titleText != null)
            m_titleText.text = preset.presetId;

        List<ShipStatGaugeEntry> entries = ShipStatGaugeBuilder.Build(preset);
        PopulateRows(entries);
        base.ShowPopup();
    }

    private void PopulateRows(List<ShipStatGaugeEntry> entries)
    {
        EnsureRowCount(entries.Count);
        for (int i = 0; i < m_rows.Count; i++)
        {
            if (i >= entries.Count)
            {
                m_rows[i].Hide();
                continue;
            }

            ShipStatGaugeEntry entry = entries[i];
            if (entry.gaugeMax > 0f)
                m_rows[i].SetGauge(entry.label, entry.value, entry.gaugeMax);
            else
                m_rows[i].SetValueOnly(entry.label, entry.rawValueText);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rowContainer);
    }

    private void EnsureRowCount(int neededCount)
    {
        if (m_rowContainer == null || m_rowPrefab == null) return;

        while (m_rows.Count < neededCount)
            m_rows.Add(Instantiate(m_rowPrefab, m_rowContainer));
    }

    private void OnCloseClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        HidePopup();
        if (m_onClose != null) m_onClose();
    }
}
