using UnityEngine;

[AddComponentMenu("Debug/Zone Preview")]
public class ZonePreviewComponent : MonoBehaviour
{
    public DataTableZone dataTableZone;
    public int selectedZoneIndex = 0;

    private const string PREVIEW_ROOT_NAME  = "ZonePreView";
    private const string CAMERA_TARGET_NAME = "CameraTarget";

    private void Awake()
    {
        // 플레이 진입 시 씬에 남아 있는 프리뷰 오브젝트 제거
        Transform existing = transform.Find(PREVIEW_ROOT_NAME);
        if (existing != null)
            Destroy(existing.gameObject);
    }

#if UNITY_EDITOR
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

        GameObject root = new(PREVIEW_ROOT_NAME);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;

        for (int i = 0; i < zone.celestialBodies.Count; i++)
        {
            CelestialBodyConfig body = zone.celestialBodies[i];
            string objName = $"Planet_{i}";

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = objName;
            sphere.transform.SetParent(root.transform);
            sphere.transform.position  = body.position;
            sphere.transform.localScale = body.scale;
            DestroyImmediate(sphere.GetComponent<Collider>());

            if (string.IsNullOrEmpty(body.materialPath) == false)
                sphere.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>(body.materialPath);
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = CAMERA_TARGET_NAME;
        marker.transform.SetParent(root.transform);
        marker.transform.position   = zone.galaxyCameraTarget;
        marker.transform.localScale = Vector3.one * 5f;
        DestroyImmediate(marker.GetComponent<Collider>());
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
        planet.position   = body.position;
        planet.localScale = body.scale;

        Renderer rend = planet.GetComponent<Renderer>();
        if (rend != null && string.IsNullOrEmpty(body.materialPath) == false)
            rend.sharedMaterial = Resources.Load<Material>(body.materialPath);
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
            Renderer rend = child.GetComponent<Renderer>();
            zone.celestialBodies.Add(new CelestialBodyConfig
            {
                position    = child.position,
                scale       = child.localScale,
                materialPath = rend != null ? ToResourcesPath(rend.sharedMaterial) : "",
            });
        }

        UnityEditor.EditorUtility.SetDirty(dataTableZone);
        Debug.Log($"[ZonePreview] 천체 {zone.celestialBodies.Count}개 DataTableZone 반영 완료");
    }

    // sharedMaterial 에셋 경로 → Resources 기준 경로 (확장자 제외)
    private static string ToResourcesPath(Material mat)
    {
        if (mat == null) return "";
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mat);
        const string prefix = "Assets/Resources/";
        if (assetPath.StartsWith(prefix))
            return assetPath.Substring(prefix.Length, assetPath.Length - prefix.Length - 4); // strip .mat
        Debug.LogWarning($"[ZonePreview] {assetPath} 가 Resources 폴더 밖에 있습니다.");
        return "";
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
