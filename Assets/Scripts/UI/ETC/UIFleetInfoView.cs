// 함대 정보 열람 UI — UITabFleetComposition의 1열(배치 목록)+2열(선택 함선 상세 스탯)에 해당하는 부분만 뽑아
// 편집 기능(3열 배치가능 프리셋, 드래그 배치) 없이 읽기전용으로 보여줌. 현재는 대치 화면(UIFleetStandoffView)의
// 적 함대 정보 열람에서 사용 — 데이터 소스가 FleetComposition이 아니라 SpaceFleet(적 함대)의 살아있는 함선 목록이라
// UITabFleetComposition과 별개 컴포넌트로 둠(추후 UITabFleetComposition이 이 컴포넌트를 내부에서 재사용하도록 리팩터링 여지 있음)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIFleetInfoView : MonoBehaviour
{
    [SerializeField] private RectTransform m_placedShipsContainer;
    [SerializeField] private UIPlacedShipRow m_placedShipRowPrefab;
    [SerializeField] private RectTransform m_statsContainer;
    [SerializeField] private UIStatGaugeRow m_statsRowPrefab;

    private readonly List<UIPlacedShipRow> m_placedShipRows = new();
    private readonly List<UIStatGaugeRow> m_statsRows = new();

    private string m_selectedShipPresetId;

    public void Open(SpaceFleet fleet)
    {
        if (fleet == null || fleet.m_fleetInfo == null || fleet.m_fleetInfo.ships == null) return;

        m_selectedShipPresetId = null;
        RefreshPlacedShips(fleet.m_fleetInfo.ships);
        RefreshStats();
        RebuildAllLayouts();

        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 3D에서 특정 함선을 클릭했을 때 그 함선을 선택 상태로 표시 — positionIndex는 목록 내 슬롯 순서 기준
    public void SelectShipByPositionIndex(SpaceFleet fleet, int positionIndex)
    {
        if (fleet == null || fleet.m_fleetInfo == null || fleet.m_fleetInfo.ships == null) return;

        List<ShipInfo> ships = fleet.m_fleetInfo.ships;
        if (positionIndex < 0 || positionIndex >= ships.Count) return;

        OnPlacedShipRowClicked(positionIndex, ships[positionIndex].shipPresetId);
    }

    private void RebuildAllLayouts()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_placedShipsContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer);
    }

    // 풀에 행이 부족하면 새로 Instantiate해서 채움 — 실제 활성/비활성은 호출부가 SetRow()/Hide()로 처리
    private void EnsureRowCount<T>(List<T> pool, RectTransform container, T prefab, int neededCount) where T : Component
    {
        if (container == null || prefab == null) return;

        while (pool.Count < neededCount)
            pool.Add(Instantiate(prefab, container));
    }

    private void RefreshPlacedShips(List<ShipInfo> ships)
    {
        EnsureRowCount(m_placedShipRows, m_placedShipsContainer, m_placedShipRowPrefab, ships.Count);

        for (int i = 0; i < m_placedShipRows.Count; i++)
        {
            if (i >= ships.Count)
            {
                m_placedShipRows[i].Hide();
                continue;
            }

            ShipInfo ship = ships[i];
            m_placedShipRows[i].Setup(i, ship.shipPresetId, ship.isFront, onFrontToggled: null, OnPlacedShipRowClicked, showFrontToggle: false);
        }
    }

    private void RefreshStats()
    {
        if (string.IsNullOrEmpty(m_selectedShipPresetId) == true)
        {
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();
            return;
        }

        ShipPresetData selectedPreset = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(m_selectedShipPresetId);
        if (selectedPreset == null)
        {
            m_selectedShipPresetId = null;
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();
            return;
        }

        List<ShipStatGaugeEntry> entries = ShipStatGaugeBuilder.Build(selectedPreset);
        EnsureRowCount(m_statsRows, m_statsContainer, m_statsRowPrefab, entries.Count);

        for (int i = 0; i < m_statsRows.Count; i++)
        {
            if (i >= entries.Count)
            {
                m_statsRows[i].Hide();
                continue;
            }

            ShipStatGaugeEntry entry = entries[i];
            switch (entry.mode)
            {
                case EGaugeMode.Normal:
                    m_statsRows[i].SetGauge(entry.label, entry.value, entry.gaugeMax);
                    break;
                case EGaugeMode.Reverse:
                    m_statsRows[i].SetReverseGauge(entry.label, entry.rawValueText, entry.reverseFillAmount);
                    break;
                default:
                    m_statsRows[i].SetValueOnly(entry.label, entry.rawValueText);
                    break;
            }
        }
    }

    private void OnPlacedShipRowClicked(int index, string shipPresetId)
    {
        m_selectedShipPresetId = shipPresetId;
        RefreshStats();
        RebuildAllLayouts();
    }
}
