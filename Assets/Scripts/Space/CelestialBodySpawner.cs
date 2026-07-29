// 런타임 천체 스포너 — Surface/Cloud/Atmosphere 3레이어 구조, MaterialPropertyBlock으로 다양성 적용
// 존 전환 시(특히 존 탭 스크롤로 빠르게 넘길 때) 매번 생성/파괴하면 GC Alloc이 커서, 자체 풀로 오브젝트를 재사용함
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
    private static readonly int ID_CloudTex           = Shader.PropertyToID("_CloudTex");
    private static readonly int ID_CloudColor         = Shader.PropertyToID("_CloudColor");
    private static readonly int ID_CloudCoverage      = Shader.PropertyToID("_CloudCoverage");
    private static readonly int ID_CloudMidLatOpacity = Shader.PropertyToID("_MidLatOpacity");
    private static readonly int ID_CloudMidLatCenter  = Shader.PropertyToID("_MidLatCenter");
    private static readonly int ID_CloudMidLatWidth   = Shader.PropertyToID("_MidLatWidth");
    private static readonly int ID_CloudSoftness      = Shader.PropertyToID("_CloudSoftness");
    private static readonly int ID_AtmColor           = Shader.PropertyToID("_AtmosphereColor");

    // 재사용되는 행성 하나의 핸들 — Cloud/Atmosphere는 항상 만들어두고 SetActive로만 토글(재사용 시 구조가 늘 동일해야 풀링이 단순해짐)
    private class CelestialBodyHandle
    {
        public GameObject root;
        public Renderer surfaceRenderer;
        public GameObject cloudObj;
        public Renderer cloudRenderer;
        public GameObject atmosphereObj;
        public Renderer atmosphereRenderer;
        public LODGroup lodGroup;
    }

    private Material m_matSurface;
    private Material m_matCloud;
    private Material m_matAtmosphere;

    private GameObject m_root;
    private readonly List<CelestialBodyHandle> m_pool = new List<CelestialBodyHandle>();
    private readonly List<CelestialBodyHandle> m_activeBodies = new List<CelestialBodyHandle>();
    private int m_activeZoneIndex = -1;

    private void LoadSharedMaterials()
    {
        if (m_matSurface != null) return;
        m_matSurface    = ResourceManager.Instance.Load<Material>(MAT_SURFACE_PATH);
        m_matCloud      = ResourceManager.Instance.Load<Material>(MAT_CLOUD_PATH);
        m_matAtmosphere = ResourceManager.Instance.Load<Material>(MAT_ATMOSPHERE_PATH);

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

        ReturnAllToPool();
        LoadSharedMaterials();

        if (m_root == null)
            m_root = new GameObject(ROOT_NAME);

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

        if (zone.celestialBodies != null)
        {
            for (int i = 0; i < zone.celestialBodies.Count; i++)
            {
                CelestialBodyHandle handle = GetFromPool();
                ConfigureBody(handle, zone.celestialBodies[i], zoneIndex, i);
                m_activeBodies.Add(handle);
            }
        }
    }

    private CelestialBodyHandle GetFromPool()
    {
        if (m_pool.Count > 0)
        {
            CelestialBodyHandle handle = m_pool[^1];
            m_pool.RemoveAt(m_pool.Count - 1);
            handle.root.SetActive(true);
            return handle;
        }
        return CreateBodyHandle();
    }

    private void ReturnAllToPool()
    {
        for (int i = 0; i < m_activeBodies.Count; i++)
        {
            m_activeBodies[i].root.SetActive(false);
            m_pool.Add(m_activeBodies[i]);
        }
        m_activeBodies.Clear();
        m_activeZoneIndex = -1;
    }

    private void ConfigureBody(CelestialBodyHandle handle, CelestialBodyConfig cfg, int zoneIndex, int bodyIndex)
    {
        handle.root.name = $"Planet_z{zoneIndex}_{bodyIndex}";
        handle.root.transform.SetPositionAndRotation(cfg.position, Quaternion.Euler(cfg.rotation));

        handle.surfaceRenderer.transform.localScale = cfg.scale;
        handle.surfaceRenderer.SetPropertyBlock(BuildSurfaceBlock(cfg));

        bool showCloud = cfg.hasClouds && m_matCloud != null;
        handle.cloudObj.SetActive(showCloud);
        if (showCloud == true)
        {
            handle.cloudRenderer.transform.localScale    = cfg.scale * cfg.cloudScale;
            handle.cloudRenderer.transform.localRotation = Quaternion.Euler(0f, cfg.cloudRotation, 0f);
            handle.cloudRenderer.SetPropertyBlock(BuildCloudBlock(cfg));
        }

        bool showAtmosphere = cfg.hasAtmosphere && m_matAtmosphere != null;
        handle.atmosphereObj.SetActive(showAtmosphere);
        if (showAtmosphere == true)
        {
            handle.atmosphereRenderer.transform.localScale = cfg.scale * cfg.atmosphereScale;
            handle.atmosphereRenderer.SetPropertyBlock(BuildAtmosphereBlock(cfg));
        }

        // LODGroup의 바운즈는 계산 시점의 스케일로 캐싱됨 — 핸들 생성 시(스케일 적용 전) 1회만 계산해두면
        // 재사용마다 달라지는 실제 크기와 어긋나 화면 비율 오판(컬링/잘못된 LOD)이 나므로 스케일 확정 후 매번 재계산
        handle.lodGroup.RecalculateBounds();
    }

    // 풀에 없을 때만 호출 — Cloud/Atmosphere까지 항상 만들어두고 이후로는 SetActive만 토글(재생성 없음)
    private CelestialBodyHandle CreateBodyHandle()
    {
        GameObject root = new GameObject("Planet");
        root.transform.SetParent(m_root.transform);

        CelestialBodyHandle handle = new CelestialBodyHandle { root = root };

        handle.surfaceRenderer = SpawnLayer(root.transform, "Surface", m_matSurface);

        handle.cloudRenderer = SpawnLayer(root.transform, "Cloud", m_matCloud);
        handle.cloudObj = handle.cloudRenderer.gameObject;

        handle.atmosphereRenderer = SpawnLayer(root.transform, "Atmosphere", m_matAtmosphere);
        handle.atmosphereObj = handle.atmosphereRenderer.gameObject;

        handle.lodGroup = AddLODGroup(root, handle.surfaceRenderer, handle.cloudRenderer, handle.atmosphereRenderer);
        return handle;
    }

    private Renderer SpawnLayer(Transform parent, string layerName, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = layerName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        Destroy(go.GetComponent<Collider>());

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
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
        block.SetColor(ID_CloudColor,          cfg.cloudColor);
        block.SetFloat(ID_CloudCoverage,       cfg.cloudCoverage);
        block.SetFloat(ID_CloudMidLatOpacity,  cfg.cloudMidLatOpacity);
        block.SetFloat(ID_CloudMidLatCenter,   cfg.cloudMidLatCenter);
        block.SetFloat(ID_CloudMidLatWidth,    cfg.cloudMidLatWidth);
        block.SetFloat(ID_CloudSoftness,       cfg.cloudSoftness);
        return block;
    }

    private MaterialPropertyBlock BuildAtmosphereBlock(CelestialBodyConfig cfg)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor(ID_AtmColor, cfg.atmosphereColor);
        return block;
    }

    private LODGroup AddLODGroup(GameObject root,
        Renderer surfaceRenderer, Renderer cloudRenderer, Renderer atmRenderer)
    {
        LODGroup lodGroup = root.AddComponent<LODGroup>();

        // LOD0: 전체 레이어, LOD1: Surface+Cloud(대기 생략) — Cloud/Atmosphere가 비활성이어도 배열에 포함해두는 건 안전(LODGroup이 알아서 무시)
        LOD[] lods = new LOD[2];
        lods[0] = new LOD(LOD_ATM_THRESHOLD,  new[] { surfaceRenderer, cloudRenderer, atmRenderer });
        lods[1] = new LOD(LOD_CULL_THRESHOLD, new[] { surfaceRenderer, cloudRenderer });
        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();
        return lodGroup;
    }

    // 존을 완전히 벗어날 때(로컬뷰 등) 전부 비활성화만 하고 풀은 유지 — 다음 진입 때 재사용
    public void ClearAll()
    {
        ReturnAllToPool();
    }
}
