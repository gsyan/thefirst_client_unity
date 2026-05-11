// 런타임 천체 스포너 — DataTableZone.zoneList의 celestialBodies를 씬에 생성 (단일 존만 유지)
using System.Collections.Generic;
using UnityEngine;

public class CelestialBodySpawner : MonoBehaviour
{
    private const string ROOT_NAME = "CelestialBodies";

    // LOD0 임계값: 화면 높이의 6% 이상이면 대기권 포함, 미만이면 행성만
    private const float LOD_ATM_THRESHOLD  = 0.06f;
    // Cull 임계값: 화면 높이의 0.5% 미만이면 컬링
    private const float LOD_CULL_THRESHOLD = 0.005f;

    private GameObject m_root;
    private readonly List<GameObject> m_spawnedBodies = new List<GameObject>();
    private int m_activeZoneIndex = -1;

    public void SpawnZone(int zoneIndex)
    {
        if (m_activeZoneIndex == zoneIndex) return;

        ClearAll();

        DataTableZone table = DataManager.Instance.m_dataTableZone;
        if (table == null)
        {
            Debug.LogError("[CelestialBodySpawner] DataTableZone 없음");
            return;
        }

        ZoneConfig zone = table.GetZoneByZoneIndex(zoneIndex);
        if (zone == null)
        {
            Debug.LogWarning($"[CelestialBodySpawner] zoneIndex {zoneIndex} 없음");
            return;
        }

        m_activeZoneIndex = zoneIndex;
        m_root = new GameObject(ROOT_NAME);

        if (zone.celestialBodies != null)
        {
            for (int i = 0; i < zone.celestialBodies.Count; i++)
            {
                CelestialBodyConfig cfg = zone.celestialBodies[i];
                m_spawnedBodies.Add(SpawnBody(cfg, zoneIndex, i));
            }
        }

        Debug.Log($"[CelestialBodySpawner] Zone {zoneIndex} 천체 {m_spawnedBodies.Count}개 생성");
    }

    private GameObject SpawnBody(CelestialBodyConfig cfg, int zoneIndex, int bodyIndex)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Planet_z{zoneIndex}_{bodyIndex}";
        go.transform.SetParent(m_root.transform);
        go.transform.position   = cfg.position;
        go.transform.localScale = cfg.scale;

        Destroy(go.GetComponent<Collider>());

        Renderer planetRenderer = go.GetComponent<Renderer>();
        if (string.IsNullOrEmpty(cfg.materialPath) == false)
            planetRenderer.sharedMaterial = Resources.Load<Material>(cfg.materialPath);

        if (string.IsNullOrEmpty(cfg.atmosphereMaterialPath) == false)
        {
            Renderer atmRenderer = SpawnAtmosphere(go.transform, cfg);
            AddLODGroup(go, planetRenderer, atmRenderer);
        }

        return go;
    }

    private Renderer SpawnAtmosphere(Transform parent, CelestialBodyConfig cfg)
    {
        GameObject atm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atm.name = "Atmosphere";
        atm.transform.SetParent(parent);
        atm.transform.localPosition = Vector3.zero;
        atm.transform.localScale    = Vector3.one * cfg.atmosphereScale;

        Destroy(atm.GetComponent<Collider>());
        Renderer r = atm.GetComponent<Renderer>();
        r.sharedMaterial = Resources.Load<Material>(cfg.atmosphereMaterialPath);
        return r;
    }

    private void AddLODGroup(GameObject go, Renderer planetRenderer, Renderer atmRenderer)
    {
        LODGroup lodGroup = go.AddComponent<LODGroup>();
        LOD[] lods = new LOD[2];
        lods[0] = new LOD(LOD_ATM_THRESHOLD,  new Renderer[] { planetRenderer, atmRenderer });
        lods[1] = new LOD(LOD_CULL_THRESHOLD, new Renderer[] { planetRenderer });
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
    }

    public void ClearAll()
    {
        m_spawnedBodies.Clear();
        m_activeZoneIndex = -1;
        GameObject existing = GameObject.Find(ROOT_NAME);
        if (existing != null)
            Destroy(existing);
        m_root = null;
    }
}
