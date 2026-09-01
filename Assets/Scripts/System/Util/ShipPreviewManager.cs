// 함선 프리셋 3D 미리보기 스테이지 관리 — 1차 프로토타입
// 메인 씬 좌표계 밖(y=-5000)에 코드로 카메라/조명/스폰 앵커를 만들고, 프리셋이 바뀔 때마다 바디 모델만 교체한다.
// 추후 전용 Additive 씬으로 옮겨서 조명을 에디터에서 손으로 튜닝할 예정(지금은 MCP 연결이 끊겨 씬 배치를 대신 못 해줌) —
// EnsureStageReady()를 EnsureSceneLoaded()로 바꾸는 정도로 교체 가능하도록 인터페이스(ShowPreset/Clear/GetPreviewTexture/GetPreviewRoot)는 유지
using UnityEngine;

public class ShipPreviewManager : MonoSingleton<ShipPreviewManager>
{
    private const float k_stageDistance = 5000f; // 메인 씬 콘텐츠와 안 겹치게 멀리 떨어뜨림
    private const int k_textureBaseHeight = 512; // 세로 해상도는 고정, 가로는 요청받은 비율만큼 스케일

    private Transform m_stageRoot;
    private Transform m_spawnAnchor;
    private Camera m_previewCamera;
    private Light m_previewLight;
    private RenderTexture m_previewTexture;
    private GameObject m_currentBodyInstance;
    private float m_currentAspect = 1f;

    public void EnsureStageReady()
    {
        if (m_stageRoot != null) return;

        GameObject stageObj = new GameObject("ShipPreviewStage");
        stageObj.transform.position = new Vector3(0f, -k_stageDistance, 0f);
        m_stageRoot = stageObj.transform;

        GameObject anchorObj = new GameObject("SpawnAnchor");
        anchorObj.transform.SetParent(m_stageRoot, false);
        m_spawnAnchor = anchorObj.transform;

        GameObject cameraObj = new GameObject("PreviewCamera");
        cameraObj.transform.SetParent(m_stageRoot, false);
        m_previewCamera = cameraObj.AddComponent<Camera>();
        m_previewCamera.clearFlags = CameraClearFlags.SolidColor;
        m_previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        m_previewCamera.orthographic = false;
        m_previewCamera.fieldOfView = 30f;

        CreatePreviewTexture(m_currentAspect);

        GameObject lightObj = new GameObject("PreviewLight");
        lightObj.transform.SetParent(m_stageRoot, false);
        m_previewLight = lightObj.AddComponent<Light>();
        m_previewLight.type = LightType.Point; // Directional은 위치 무관하게 씬 전체를 비추므로 여기선 금지 — 감쇠로 스테이지에만 영향
        m_previewLight.intensity = 2f;
    }

    // RawImage의 실제 가로/세로 비율(width/height)에 맞춰 텍스처를 다시 만듦 — 정사각형으로 제한하면 넓은 자리에서 함선이 작아 보이는 문제 방지
    private void CreatePreviewTexture(float aspect)
    {
        int height = k_textureBaseHeight;
        int width = Mathf.Max(1, Mathf.RoundToInt(k_textureBaseHeight * aspect));

        RenderTexture oldTexture = m_previewTexture;

        m_previewTexture = new RenderTexture(width, height, 16);
        m_previewTexture.name = "ShipPreviewRT";
        m_previewCamera.targetTexture = m_previewTexture;

        if (oldTexture != null)
            oldTexture.Release();
    }

    public void ShowPreset(ModuleData hull)
    {
        EnsureStageReady();
        Clear();

        if (hull == null) return;

        GameObject bodyPrefab = ObjectManager.Instance.LoadShipModulePrefab(EModuleType.body.ToString(), hull.moduleSubType.ToString());
        if (bodyPrefab == null) return;

        m_currentBodyInstance = Instantiate(bodyPrefab, m_spawnAnchor);
        m_currentBodyInstance.transform.localPosition = Vector3.zero;
        m_currentBodyInstance.transform.localRotation = Quaternion.identity;

        FrameCameraOnInstance(m_currentBodyInstance);
    }

    // 모델 실제 크기를 몰라도 항상 화면에 맞게 잡히도록, 스폰 직후 바운즈를 계산해서 카메라/조명을 재배치.
    // "가장 긴 축 하나를 세로 FOV에 맞춘다"는 이전 방식은, 그 긴 축이 카메라의 위쪽 방향과 거의 무관한(예: 옆으로 누운 전장) 경우
    // 실제 세로로 보이는 크기는 그보다 훨씬 작은데도 카메라를 필요 이상으로 멀리 빼버려 여백이 크게 남는 문제가 실측으로 확인됨
    // (함선 예시: X=1.96 Y=1.95 Z=4.33인데 카메라 위쪽은 월드 Y와 거의 같아서, 세로로는 1.95만 보이는데 4.33 기준으로 거리를 잡았었음) —
    // 대신 카메라가 실제로 보는 방향의 위/오른쪽 축에 바운즈를 투영해서 진짜 필요한 세로/가로 크기를 구해 맞춘다
    private void FrameCameraOnInstance(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // 함선의 전장은 보통 로컬 Z축 방향이라, 시선이 Z축에 가까우면 길이가 그대로 앞뒤로 찌부러져 보인다 —
        // X축 비중을 높여 옆에서 비스듬히 보는 각도로 고정
        Vector3 viewDirection = new Vector3(0.85f, -0.3f, 0.45f).normalized;
        Quaternion viewRotation = Quaternion.LookRotation(viewDirection, Vector3.up);
        Vector3 viewRight = viewRotation * Vector3.right;
        Vector3 viewUp = viewRotation * Vector3.up;

        float halfWidth = 0f;
        float halfHeight = 0f;
        Vector3 extents = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? extents.x : -extents.x,
                (i & 2) == 0 ? extents.y : -extents.y,
                (i & 4) == 0 ? extents.z : -extents.z);

            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(corner, viewRight)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(corner, viewUp)));
        }
        if (halfWidth <= 0f) halfWidth = 1f;
        if (halfHeight <= 0f) halfHeight = 1f;

        const float k_paddingFactor = 1.1f; // 여유 마진
        float halfVFovRad = m_previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfHFovRad = Mathf.Atan(Mathf.Tan(halfVFovRad) * m_previewCamera.aspect);

        float distanceForHeight = halfHeight / Mathf.Tan(halfVFovRad);
        float distanceForWidth = halfWidth / Mathf.Tan(halfHFovRad);
        float fitDistance = Mathf.Max(distanceForHeight, distanceForWidth) * k_paddingFactor;

        m_previewCamera.transform.position = bounds.center - viewDirection * fitDistance;
        m_previewCamera.transform.rotation = viewRotation;
        m_previewCamera.nearClipPlane = 0.05f;

        float boundsRadius = bounds.extents.magnitude;
        if (boundsRadius <= 0f) boundsRadius = 1f;
        m_previewCamera.farClipPlane = fitDistance + boundsRadius * 2f + 10f;

        m_previewLight.transform.position = bounds.center + new Vector3(boundsRadius, boundsRadius, -boundsRadius);
        m_previewLight.range = boundsRadius * 6f;
    }

    public void Clear()
    {
        if (m_currentBodyInstance != null)
        {
            Destroy(m_currentBodyInstance);
            m_currentBodyInstance = null;
        }
    }

    // aspectRatio: RawImage의 width/height — 프리셋 선택 화면을 열 때마다 최신 레이아웃 비율을 넘겨받아 텍스처를 맞춤
    public RenderTexture GetPreviewTexture(float aspectRatio = 1f)
    {
        EnsureStageReady();

        if (aspectRatio > 0f && Mathf.Approximately(aspectRatio, m_currentAspect) == false)
        {
            m_currentAspect = aspectRatio;
            CreatePreviewTexture(m_currentAspect);
        }

        return m_previewTexture;
    }

    public Transform GetPreviewRoot()
    {
        EnsureStageReady();
        return m_spawnAnchor;
    }
}
