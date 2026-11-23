// 갤럭시뷰(탐사 그리드) 우측 상단 — 내 함대에 배치된 함선들의 이름과 체력을 슬롯 순서대로 표시(보상카드 구매 판단용)
// 행은 배치된 함선 수만큼만 보임(배치/해제 시 즉시 반영). 해제된 행은 비활성화해 두었다가 재사용, 파괴된 함선은 행을 남기고 게이지 0
using System.Collections.Generic;
using UnityEngine;

public class UIGridFleetHealthList : MonoBehaviour
{
    [SerializeField] private UIGridShipHealthRow m_rowPrefab; // 행 프리팹(Prefabs/UI/ETC/UIGridShipHealthRow)

    private readonly List<UIGridShipHealthRow> m_rows = new List<UIGridShipHealthRow>();
    private readonly List<ShipInfo> m_sortedShips = new List<ShipInfo>(); // 표시 순서(positionIndex 오름차순) — 재사용해 할당 최소화

    private static readonly System.Comparison<ShipInfo> s_compareByPositionIndex = (a, b) => a.positionIndex.CompareTo(b.positionIndex);

    // 프리팹에 미리 넣어둔 행이 있으면 재사용 목록으로 흡수 — 부족한 만큼만 m_rowPrefab으로 추가 생성
    private void Awake()
    {
        GetComponentsInChildren(true, m_rows);
    }

    private void OnEnable()
    {
        EventManager.Subscribe_FleetUpdateHP(RefreshHealth);
        EventManager.Subscribe_FleetShipCountChanged(Rebuild);
        EventManager.Subscribe_MyFleetSet(Rebuild);
        Rebuild();
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe_FleetUpdateHP(RefreshHealth);
        EventManager.Unsubscribe_FleetShipCountChanged(Rebuild);
        EventManager.Unsubscribe_MyFleetSet(Rebuild);
    }

    // 편성(로스터)이 바뀌었을 때 — 행 개수를 함선 수에 맞추고 이름까지 다시 채움
    private void Rebuild()
    {
        m_sortedShips.Clear();
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet != null && myFleet.m_fleetInfo != null && myFleet.m_fleetInfo.ships != null)
            m_sortedShips.AddRange(myFleet.m_fleetInfo.ships);
        m_sortedShips.Sort(s_compareByPositionIndex);

        int shipCount = m_sortedShips.Count;
        while (m_rows.Count < shipCount && m_rowPrefab != null)
        {
            UIGridShipHealthRow newRow = Instantiate(m_rowPrefab, transform);
            m_rows.Add(newRow);
        }

        for (int i = 0; i < m_rows.Count; i++)
        {
            if (i >= shipCount)
            {
                m_rows[i].Hide();
                continue;
            }

            string shipName = CommonUtility.BuildHullDisplayName(m_sortedShips[i].hullSubType);
            m_rows[i].SetShipName(shipName);
        }

        RefreshHealth();
    }

    // 체력만 바뀌었을 때 — 이름은 건드리지 않음(전투 중 잦은 호출에서 텍스트 재생성 방지)
    private void RefreshHealth()
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        int shownCount = Mathf.Min(m_rows.Count, m_sortedShips.Count);
        for (int i = 0; i < shownCount; i++)
        {
            int positionIndex = m_sortedShips[i].positionIndex;
            SpaceShip ship = FindAliveShip(myFleet, positionIndex);
            float healthRatio = ship != null ? ship.GetHealthRatio() : 0f;
            // 격침 직후 HP 이벤트는 함선이 목록에서 빠지기 전에 오므로 체력 0도 파괴로 취급
            bool isDestroyed = ship == null || healthRatio <= 0f;
            m_rows[i].SetHealthRatio(healthRatio, isDestroyed);
        }
    }

    // m_ships에는 살아있는 함선만 있음 — 슬롯 위치(positionIndex)로 매칭(람다 Find는 클로저 할당이 생겨 직접 순회)
    private SpaceShip FindAliveShip(SpaceFleet fleet, int positionIndex)
    {
        for (int i = 0; i < fleet.m_ships.Count; i++)
        {
            SpaceShip ship = fleet.m_ships[i];
            if (ship != null && ship.m_shipInfo.positionIndex == positionIndex)
                return ship;
        }
        return null;
    }
}
