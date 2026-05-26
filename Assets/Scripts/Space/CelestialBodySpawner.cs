// 런타임 천체 스포너 — Surface/Cloud/Atmosphere 3레이어 구조, MaterialPropertyBlock으로 다양성 적용
using System.Collections.Generic;
using UnityEngine;

public class CelestialBodySpawner : MonoBehaviour
{
    private const string ROOT_NAME            = "CelestialBodies";
    private const string MAT_SURFACE_PATH     = "Materials/CelestialBody/PlanetSurface";
    private const string MAT_CLOUD_PATH       = "Materials/CelestialBody/PlanetCloud";
    private const string MAT_ATMOSPHERE_PATH  = "Materials/CelestialBody/PlanetAtmosphere";

    // LOD0: 대기권까지 렌더, LOD1: Surface+Cloud만, Cull
    private const float LOD_ATM_THRESHOLD  = 0.06f;
    private const float LOD_CULL_THRESHOLD = 0.005f;

    // 셰이더 프로퍼티 ID (캐시)
    private static readonly int ID_DeepSeaColor      = Shader.PropertyToID("_DeepSeaColor");
    private static readonly int ID_ShallowSeaColor   = Shader.PropertyToID("_ShallowSeaColor");
    private static readonly int ID_LowlandSandColor  = Shader.PropertyToID("_LowlandSandColor");
    private static readonly int ID_LowlandGreenColor = Shader.PropertyToID("_LowlandGreenColor");
    private static readonly int ID_PlainsDesertColor = Shader.PropertyToID("_PlainsDesertColor");
    private static readonly int ID_PlainsGrassColor  = Shader.PropertyToID("_PlainsGrassColor");
    private static readonly int ID_PlainsForestColor = Shader.PropertyToID("_PlainsForestColor");
    private static readonly int ID_HighlandSnowColor    = Shader.PropertyToID("_HighlandSnowColor");
    private static readonly int ID_LandCoverage    = Shader.PropertyToID("_LandCoverage");
    private static readonly int ID_BiomeBlend   = Shader.PropertyToID("_BiomeBlend");
    private static readonly int ID_GBlend       = Shader.PropertyToID("_GBlend");
    private static readonly int ID_HasPolarIce  = Shader.PropertyToID("_HasPolarIce");
    private static readonly int ID_IceColor     = Shader.PropertyToID("_IceColor");
    private static readonly int ID_IceColorEdge = Shader.PropertyToID("_IceColorEdge");
    private static readonly int ID_PoleIceWidth = Shader.PropertyToID("_PoleIceWidth");
    private static readonly int ID_CloudTex      = Shader.PropertyToID("_CloudTex");
    private static readonly int ID_CloudColor    = Shader.PropertyToID("_CloudColor");
    private static readonly int ID_CloudCoverage = Shader.PropertyToID("_CloudCoverage");
    private static readonly int ID_AtmColor      = Shader.PropertyToID("_AtmosphereColor");

    private Material m_matSurface;
    private Material m_matCloud;
    private Material m_matAtmosphere;

    private GameObject          m_root;
    private readonly List<GameObject> m_spawnedBodies = new List<GameObject>();
    private int m_activeZoneIndex = -1;

    private void LoadSharedMaterials()
    {
        if (m_matSurface != null) return;
        m_matSurface    = Resources.Load<Material>(MAT_SURFACE_PATH);
        m_matCloud      = Resources.Load<Material>(MAT_CLOUD_PATH);
        m_matAtmosphere = Resources.Load<Material>(MAT_ATMOSPHERE_PATH);

        if (m_matSurface == null)
            Debug.LogError($"[CelestialBodySpawner] 메터리얼 없음: {MAT_SURFACE_PATH}");
        if (m_matCloud == null)
            Debug.LogError($"[CelestialBodySpawner] 메터리얼 없음: {MAT_CLOUD_PATH}");
        if (m_matAtmosphere == null)
            Debug.LogError($"[CelestialBodySpawner] 메터리얼 없음: {MAT_ATMOSPHERE_PATH}");
    }

    public void SpawnZone(int zoneIndex)
    {
        if (m_activeZoneIndex == zoneIndex) return;

        ClearAll();
        LoadSharedMaterials();

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
                m_spawnedBodies.Add(SpawnBody(zone.celestialBodies[i], zoneIndex, i));
        }

        Debug.Log($"[CelestialBodySpawner] Zone {zoneIndex} 천체 {m_spawnedBodies.Count}개 생성");
    }

    private GameObject SpawnBody(CelestialBodyConfig cfg, int zoneIndex, int bodyIndex)
    {
        GameObject root = new GameObject($"Planet_z{zoneIndex}_{bodyIndex}");
        root.transform.SetParent(m_root.transform);
        root.transform.SetPositionAndRotation(cfg.position, Quaternion.Euler(cfg.rotation));

        Renderer surfaceRenderer = SpawnLayer(root.transform, "Surface",
            cfg.scale, m_matSurface, BuildSurfaceBlock(cfg));

        Renderer cloudRenderer = null;
        if (cfg.hasClouds && m_matCloud != null)
        {
            cloudRenderer = SpawnLayer(root.transform, "Cloud",
                cfg.scale * cfg.cloudScale, m_matCloud, BuildCloudBlock(cfg));
            cloudRenderer.transform.localRotation = Quaternion.Euler(0f, cfg.cloudRotation, 0f);
        }

        Renderer atmRenderer = null;
        if (cfg.hasAtmosphere && m_matAtmosphere != null)
            atmRenderer = SpawnLayer(root.transform, "Atmosphere",
                cfg.scale * cfg.atmosphereScale, m_matAtmosphere, BuildAtmosphereBlock(cfg));

        AddLODGroup(root, surfaceRenderer, cloudRenderer, atmRenderer);
        return root;
    }

    private Renderer SpawnLayer(Transform parent, string layerName,
        Vector3 scale, Material mat, MaterialPropertyBlock block)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = layerName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = scale;
        Destroy(go.GetComponent<Collider>());

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.SetPropertyBlock(block);
        return r;
    }

    private MaterialPropertyBlock BuildSurfaceBlock(CelestialBodyConfig cfg)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor(ID_DeepSeaColor,      cfg.deepSeaColor);
        block.SetColor(ID_ShallowSeaColor,   cfg.shallowSeaColor);
        block.SetColor(ID_LowlandSandColor,  cfg.lowlandSandColor);
        block.SetColor(ID_LowlandGreenColor, cfg.lowlandGreenColor);
        block.SetColor(ID_PlainsDesertColor, cfg.plainsDesertColor);
        block.SetColor(ID_PlainsGrassColor,  cfg.plainsGrassColor);
        block.SetColor(ID_PlainsForestColor, cfg.plainsForestColor);
        block.SetColor(ID_HighlandSnowColor,    cfg.highlandSnowColor);
        block.SetFloat(ID_LandCoverage, cfg.landCoverage);
        block.SetFloat(ID_BiomeBlend,   cfg.biomeBlend);
        block.SetFloat(ID_GBlend,       cfg.gBlend);
        block.SetFloat(ID_HasPolarIce,  cfg.hasPolarIce ? 1f : 0f);
        block.SetColor(ID_IceColor,     cfg.iceColor);
        block.SetColor(ID_IceColorEdge, cfg.iceColorEdge);
        block.SetFloat(ID_PoleIceWidth, cfg.poleIceWidth);
        return block;
    }

    private MaterialPropertyBlock BuildCloudBlock(CelestialBodyConfig cfg)
    {
        var block = new MaterialPropertyBlock();
        if (cfg.cloudMaskTex != null)
            block.SetTexture(ID_CloudTex, cfg.cloudMaskTex);
        block.SetColor(ID_CloudColor,    cfg.cloudColor);
        block.SetFloat(ID_CloudCoverage, cfg.cloudCoverage);
        return block;
    }

    private MaterialPropertyBlock BuildAtmosphereBlock(CelestialBodyConfig cfg)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor(ID_AtmColor, cfg.atmosphereColor);
        return block;
    }

    private void AddLODGroup(GameObject root,
        Renderer surfaceRenderer, Renderer cloudRenderer, Renderer atmRenderer)
    {
        LODGroup lodGroup = root.AddComponent<LODGroup>();

        // LOD0: 전체 레이어
        var lod0Renderers = new List<Renderer> { surfaceRenderer };
        if (cloudRenderer != null)   lod0Renderers.Add(cloudRenderer);
        if (atmRenderer != null)     lod0Renderers.Add(atmRenderer);

        // LOD1: Surface + Cloud (대기 생략)
        var lod1Renderers = new List<Renderer> { surfaceRenderer };
        if (cloudRenderer != null)   lod1Renderers.Add(cloudRenderer);

        LOD[] lods = new LOD[2];
        lods[0] = new LOD(LOD_ATM_THRESHOLD,  lod0Renderers.ToArray());
        lods[1] = new LOD(LOD_CULL_THRESHOLD, lod1Renderers.ToArray());
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
