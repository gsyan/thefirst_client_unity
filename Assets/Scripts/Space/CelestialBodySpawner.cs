// 런타임 천체 스포너 — DataTableZone.zoneList의 celestialBodies를 씬에 생성
using System.Collections.Generic;
using UnityEngine;

public class CelestialBodySpawner : MonoBehaviour
{
    private const string ROOT_NAME = "CelestialBodies";

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
        string bodyName = cfg.isStar ? $"Star_z{zoneIndex}_{bodyIndex}" : $"Planet_z{zoneIndex}_{bodyIndex}";

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = bodyName;
        go.transform.SetParent(m_root.transform);
        go.transform.position   = cfg.position;
        go.transform.localScale = cfg.scale;

        // 천체는 물리 상호작용 불필요
        Destroy(go.GetComponent<Collider>());

        if (cfg.material != null)
            go.GetComponent<Renderer>().sharedMaterial = cfg.material;

        return go;
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
