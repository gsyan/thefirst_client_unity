using UnityEngine;

[AddComponentMenu("Debug/Zone Preview")]
public class ZonePreviewComponent : MonoBehaviour
{
    public DataTableZone dataTableZone;
    public int selectedZoneIndex = 0;

    private const string PREVIEW_ROOT_NAME  = "ZonePreView";
    private const string CAMERA_TARGET_NAME = "CameraTarget";
    private const string LAYER_SURFACE      = "Surface";
    private const string LAYER_CLOUD        = "Cloud";
    private const string LAYER_ATMOSPHERE   = "Atmosphere";

    private const string MAT_SURFACE_PATH    = "Materials/CelestialBody/PlanetSurface";
    private const string MAT_CLOUD_PATH      = "Materials/CelestialBody/PlanetCloud";
    private const string MAT_ATMOSPHERE_PATH = "Materials/CelestialBody/PlanetAtmosphere";

    private void Awake()
    {
        // 플레이 진입 시 씬에 남아 있는 프리뷰 오브젝트 제거
        Transform existing = transform.Find(PREVIEW_ROOT_NAME);
        if (existing != null)
            Destroy(existing.gameObject);
    }

#if UNITY_EDITOR
    private static readonly int ID_DeepSeaColor      = Shader.PropertyToID("_DeepSeaColor");
    private static readonly int ID_ShallowSeaColor   = Shader.PropertyToID("_ShallowSeaColor");
    private static readonly int ID_LowlandSandColor  = Shader.PropertyToID("_LowlandSandColor");
    private static readonly int ID_LowlandGreenColor = Shader.PropertyToID("_LowlandGreenColor");
    private static readonly int ID_PlainsDesertColor = Shader.PropertyToID("_PlainsDesertColor");
    private static readonly int ID_PlainsGrassColor  = Shader.PropertyToID("_PlainsGrassColor");
    private static readonly int ID_PlainsForestColor = Shader.PropertyToID("_PlainsForestColor");
    private static readonly int ID_HighlandSnowColor = Shader.PropertyToID("_HighlandSnowColor");
    private static readonly int ID_LandCoverage      = Shader.PropertyToID("_LandCoverage");
    private static readonly int ID_BiomeBlend        = Shader.PropertyToID("_BiomeBlend");
    private static readonly int ID_GBlend            = Shader.PropertyToID("_GBlend");
    private static readonly int ID_HasPolarIce       = Shader.PropertyToID("_HasPolarIce");
    private static readonly int ID_IceColor          = Shader.PropertyToID("_IceColor");
    private static readonly int ID_IceColorEdge      = Shader.PropertyToID("_IceColorEdge");
    private static readonly int ID_PoleIceWidth      = Shader.PropertyToID("_PoleIceWidth");
    private static readonly int ID_CloudTex           = Shader.PropertyToID("_CloudTex");
    private static readonly int ID_CloudColor         = Shader.PropertyToID("_CloudColor");
    private static readonly int ID_CloudCoverage      = Shader.PropertyToID("_CloudCoverage");
    private static readonly int ID_CloudMidLatOpacity = Shader.PropertyToID("_MidLatOpacity");
    private static readonly int ID_CloudMidLatCenter  = Shader.PropertyToID("_MidLatCenter");
    private static readonly int ID_CloudMidLatWidth   = Shader.PropertyToID("_MidLatWidth");
    private static readonly int ID_CloudSoftness      = Shader.PropertyToID("_CloudSoftness");
    private static readonly int ID_AtmColor           = Shader.PropertyToID("_AtmosphereColor");

    public void RefreshPreview()
    {
        ClearPreview();

        if (dataTableZone == null) return;
        ZoneConfig zone = dataTableZone.GetZone(selectedZoneIndex);
        if (zone == null)
        {
            Debug.LogWarning($"[ZonePreview] zoneIndex {selectedZoneIndex} 없음");
            return;
        }

        Material matSurface    = Resources.Load<Material>(MAT_SURFACE_PATH);
        Material matCloud      = Resources.Load<Material>(MAT_CLOUD_PATH);
        Material matAtmosphere = Resources.Load<Material>(MAT_ATMOSPHERE_PATH);

        GameObject root = new(PREVIEW_ROOT_NAME);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;

        for (int i = 0; i < zone.celestialBodies.Count; i++)
        {
            CelestialBodyConfig body = zone.celestialBodies[i];

            GameObject planet = new($"Planet_{i}");
            planet.transform.SetParent(root.transform);
            planet.transform.SetPositionAndRotation(body.position, Quaternion.Euler(body.rotation));

            SpawnLayer(planet.transform, LAYER_SURFACE, body.scale, matSurface, BuildSurfaceBlock(body));

            if (body.hasClouds && matCloud != null)
            {
                Renderer cloudRend = SpawnLayer(planet.transform, LAYER_CLOUD, body.scale * body.cloudScale, matCloud, BuildCloudBlock(body));
                cloudRend.transform.localRotation = Quaternion.Euler(0f, body.cloudRotation, 0f);
            }

            if (body.hasAtmosphere && matAtmosphere != null)
                SpawnLayer(planet.transform, LAYER_ATMOSPHERE, body.scale * body.atmosphereScale, matAtmosphere, BuildAtmosphereBlock(body));
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = CAMERA_TARGET_NAME;
        marker.transform.SetParent(root.transform);
        marker.transform.position   = zone.galaxyCameraTarget;
        marker.transform.localScale = Vector3.one * 5f;
        DestroyImmediate(marker.GetComponent<Collider>());
    }

    private Renderer SpawnLayer(Transform parent, string layerName, Vector3 scale, Material mat, MaterialPropertyBlock block)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = layerName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = scale;
        DestroyImmediate(go.GetComponent<Collider>());

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
        block.SetColor(ID_HighlandSnowColor, cfg.highlandSnowColor);
        block.SetFloat(ID_LandCoverage, cfg.landCoverage);
        block.SetFloat(ID_BiomeBlend,   cfg.biomeBlend);
        block.SetFloat(ID_GBlend,       cfg.gBlend);
        block.SetFloat(ID_HasPolarIce,       cfg.hasPolarIce ? 1f : 0f);
        block.SetColor(ID_IceColor,          cfg.iceColor);
        block.SetColor(ID_IceColorEdge,      cfg.iceColorEdge);
        block.SetFloat(ID_PoleIceWidth,      cfg.poleIceWidth);
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

    public void ClearPreview()
    {
        Transform existing = transform.Find(PREVIEW_ROOT_NAME);
        if (existing != null)
            DestroyImmediate(existing.gameObject);
    }

    public void SyncPreviewPlanet(int index)
    {
        if (dataTableZone == null) return;
        ZoneConfig zone = dataTableZone.GetZone(selectedZoneIndex);
        if (zone == null || index >= zone.celestialBodies.Count) return;

        Transform root = transform.Find(PREVIEW_ROOT_NAME);
        if (root == null) return;

        Transform planet = root.Find($"Planet_{index}");
        if (planet == null) return;

        CelestialBodyConfig body = zone.celestialBodies[index];
        planet.SetPositionAndRotation(body.position, Quaternion.Euler(body.rotation));

        Transform surface = planet.Find(LAYER_SURFACE);
        if (surface != null && surface.TryGetComponent(out Renderer surfRend) == true)
            surfRend.SetPropertyBlock(BuildSurfaceBlock(body));

        Transform cloud = planet.Find(LAYER_CLOUD);
        if (cloud != null && cloud.TryGetComponent(out Renderer cloudRend) == true)
        {
            cloud.localRotation = Quaternion.Euler(0f, body.cloudRotation, 0f);
            cloudRend.SetPropertyBlock(BuildCloudBlock(body));
        }

        Transform atm = planet.Find(LAYER_ATMOSPHERE);
        if (atm != null && atm.TryGetComponent(out Renderer atmRend) == true)
            atmRend.SetPropertyBlock(BuildAtmosphereBlock(body));
    }

    public void SyncPreviewCameraTarget()
    {
        if (dataTableZone == null) return;
        ZoneConfig zone = dataTableZone.GetZone(selectedZoneIndex);
        if (zone == null) return;

        Transform root = transform.Find(PREVIEW_ROOT_NAME);
        if (root == null) return;

        Transform camTarget = root.Find(CAMERA_TARGET_NAME);
        if (camTarget != null)
            camTarget.position = zone.galaxyCameraTarget;
    }

    // 씬 오브젝트 위치·크기 → DataTableZone에 반영
    public void ApplyFromScene()
    {
        if (dataTableZone == null) return;
        ZoneConfig zone = dataTableZone.GetZone(selectedZoneIndex);
        if (zone == null) return;

        Transform root = transform.Find(PREVIEW_ROOT_NAME);
        if (root == null)
        {
            Debug.LogWarning("[ZonePreview] ZonePreView 없음 — Refresh 먼저 실행하세요.");
            return;
        }

        UnityEditor.Undo.RecordObject(dataTableZone, "Apply Zone Preview to DataTable");

        Transform camTarget = root.Find(CAMERA_TARGET_NAME);
        if (camTarget != null)
            zone.galaxyCameraTarget = camTarget.position;

        zone.celestialBodies.Clear();
        foreach (Transform child in root)
        {
            if (child.name == CAMERA_TARGET_NAME) continue;

            Transform surface = child.Find(LAYER_SURFACE);
            Vector3 scale = surface != null ? surface.localScale : Vector3.one * 20f;

            zone.celestialBodies.Add(new CelestialBodyConfig
            {
                position = child.position,
                rotation = child.eulerAngles,
                scale    = scale,
            });
        }

        UnityEditor.EditorUtility.SetDirty(dataTableZone);
        Debug.Log($"[ZonePreview] 천체 {zone.celestialBodies.Count}개 DataTableZone 반영 완료");
    }

    private void OnDrawGizmosSelected()
    {
        if (dataTableZone == null) return;
        ZoneConfig zone = dataTableZone.GetZone(selectedZoneIndex);
        if (zone == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawWireSphere(zone.galaxyCameraTarget, 8f);
        Gizmos.DrawWireSphere(zone.galaxyCameraTarget, 2f);
    }
#endif
}
