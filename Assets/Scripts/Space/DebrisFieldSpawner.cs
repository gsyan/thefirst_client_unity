// 런타임 잔해 스포너 — Blocked 셀 중 행성이 배치되지 않은 칸마다 셀 중앙에 잔해 1개를 배치
// CelestialBodySpawner와 같은 시점(ObjectManager.ChangeZone)에 호출되는 나란한 컴포넌트.
// 잔해는 데이터테이블에 굽지 않고 매 존 진입마다 즉석 계산 — PoolManager로 풀링해 Instantiate/Destroy 반복을 피함
using System.Collections.Generic;
using UnityEngine;

public class DebrisFieldSpawner : MonoBehaviour
{
    [SerializeField] private Transform m_debrisRockPrefab;
    [SerializeField] private Transform m_debrisJunkPrefab;
    [SerializeField] private Vector3 m_debrisScale = new Vector3(50f, 50f, 50f);

    private const int k_poolInitialSize = 20;
    private const int k_poolMaxSize = 200;

    private struct ActiveDebrisEntry
    {
        public Transform transform;
        public EPoolName poolName;
    }

    private readonly List<ActiveDebrisEntry> m_activeDebris = new List<ActiveDebrisEntry>();
    private int m_activeZoneIndex = -1;
    private bool m_poolsCreated = false;

    private void EnsurePools()
    {
        if (m_poolsCreated == true) return;

        PoolManager poolManager = ObjectManager.Instance.m_poolManager;
        if (m_debrisRockPrefab != null && poolManager.HasPool(EPoolName.DEBRIS_ROCK) == false)
            poolManager.CreatePool(EPoolName.DEBRIS_ROCK, m_debrisRockPrefab, k_poolInitialSize, k_poolMaxSize);
        if (m_debrisJunkPrefab != null && poolManager.HasPool(EPoolName.DEBRIS_JUNK) == false)
            poolManager.CreatePool(EPoolName.DEBRIS_JUNK, m_debrisJunkPrefab, k_poolInitialSize, k_poolMaxSize);

        m_poolsCreated = true;
    }

    public void SpawnZone(int zoneIndex)
    {
        if (m_activeZoneIndex == zoneIndex) return;

        EnsurePools();
        ReturnAllToPool();

        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone != null
            ? DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneIndex)
            : null;
        if (zoneConfig == null || zoneConfig.cellOverrides == null) return;

        m_activeZoneIndex = zoneIndex;

        // 행성이 배치된 셀은 잔해에서 제외 — celestialBodies[0].position을 역산해서 어느 (row,col)인지 구함
        bool hasPlanet = zoneConfig.celestialBodies != null && zoneConfig.celestialBodies.Count > 0;
        int planetRow = -1;
        int planetCol = -1;
        if (hasPlanet == true)
            ExplorationGridGenerator.WorldPosToNearestCell(zoneConfig, zoneConfig.celestialBodies[0].position, out planetRow, out planetCol);

        foreach (GridCellOverride cellOverride in zoneConfig.cellOverrides)
        {
            if (cellOverride.type != EGridCellType.Blocked) continue;
            if (hasPlanet == true && cellOverride.row == planetRow && cellOverride.col == planetCol) continue;

            Vector3 cellWorldPos = ExplorationGridGenerator.ComputeCellWorldPos(zoneConfig, cellOverride.row, cellOverride.col);
            SpawnDebrisAtCell(cellWorldPos);
        }
    }

    private void SpawnDebrisAtCell(Vector3 cellWorldPos)
    {
        bool useRock = Random.value < 0.5f;
        Transform prefab = useRock ? m_debrisRockPrefab : m_debrisJunkPrefab;
        if (prefab == null) return;

        EPoolName poolName = useRock ? EPoolName.DEBRIS_ROCK : EPoolName.DEBRIS_JUNK;
        Transform debris = ObjectManager.Instance.m_poolManager.Get<Transform>(poolName);
        if (debris == null) return;

        debris.SetPositionAndRotation(cellWorldPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        debris.localScale = m_debrisScale;

        m_activeDebris.Add(new ActiveDebrisEntry { transform = debris, poolName = poolName });
    }

    private void ReturnAllToPool()
    {
        for (int i = 0; i < m_activeDebris.Count; i++)
        {
            ActiveDebrisEntry entry = m_activeDebris[i];
            if (entry.transform == null) continue;
            ObjectManager.Instance.m_poolManager.Return(entry.poolName, entry.transform);
        }
        m_activeDebris.Clear();
        m_activeZoneIndex = -1;
    }

    // 존을 완전히 벗어날 때(로컬뷰 등) 전부 반환 — 다음 진입 때 재사용
    public void ClearAll()
    {
        ReturnAllToPool();
    }
}
