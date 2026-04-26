// 런타임 천체 스포너 — DataTableZone.zoneList의 celestialBodies를 씬에 생성
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

    public void SpawnAll()
    {
        ClearAll();

        DataTableZone table = DataManager.Instance.m_dataTableZone;
        if (table == null)
        {
            Debug.LogError("[CelestialBodySpawner] DataTableZone 없음");
            return;
        }

        m_root = new GameObject(ROOT_NAME);

        foreach (ZoneConfig zone in table.zoneList)
        {
            if (zone.celestialBodies == null) continue;

            for (int i = 0; i < zone.celestialBodies.Count; i++)
            {
                CelestialBodyConfig cfg = zone.celestialBodies[i];
                GameObject body = SpawnBody(cfg, zone.zoneIndex, i);
                m_spawnedBodies.Add(body);
            }
        }

        Debug.Log($"[CelestialBodySpawner] 천체 {m_spawnedBodies.Count}개 생성 완료");
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
        if (cfg.material != null)
            planetRenderer.sharedMaterial = cfg.material;

        if (cfg.atmosphereMaterial != null)
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
        r.sharedMaterial = cfg.atmosphereMaterial;
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
        GameObject existing = GameObject.Find(ROOT_NAME);
        if (existing != null)
            Destroy(existing);
        m_root = null;
    }
}
