// 함체 3D 미리보기 스테이지 관리 — 1차 프로토타입
// 메인 씬 좌표계 밖(y=-5000)에 코드로 카메라/조명/스폰 앵커를 만들고, 함체가 바뀔 때마다 바디 모델만 교체한다.
// 추후 전용 Additive 씬으로 옮겨서 조명을 에디터에서 손으로 튜닝할 예정(지금은 MCP 연결이 끊겨 씬 배치를 대신 못 해줌) —
// EnsureStageReady()를 EnsureSceneLoaded()로 바꾸는 정도로 교체 가능하도록 인터페이스(ShowHull/Clear/GetPreviewTexture/GetPreviewRoot)는 유지
using System.Collections.Generic;
using UnityEngine;

public class ShipPreviewManager : MonoSingleton<ShipPreviewManager>
{
    private const float k_stageDistance = 5000f; // 메인 씬 콘텐츠와 안 겹치게 멀리 떨어뜨림
    private const float k_cameraDistance = 18f; // 프리뷰 카메라와 함선(바운즈 중심) 사이의 고정 거리
    private const int k_textureBaseHeight = 512; // 호출부가 화면 픽셀 크기를 넘기기 전 기본 세로 해상도
    private const int k_textureMinHeight = 256;
    private const int k_textureMaxHeight = 1024; // 메모리/성능 상한
    private const int k_textureAntiAliasing = 4; // 프리뷰 모서리 계단 현상 완화(MSAA)

    private Transform m_stageRoot;
    private Transform m_spawnAnchor;
    private Camera m_previewCamera;
    private Light m_previewLight;
    private RenderTexture m_previewTexture;
    private GameObject m_currentBodyInstance;
    private float m_currentAspect = 1f;
    private int m_currentHeight = k_textureBaseHeight;

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

        CreatePreviewTexture(m_currentAspect, m_currentHeight);

        GameObject lightObj = new GameObject("PreviewLight");
        lightObj.transform.SetParent(m_stageRoot, false);
        m_previewLight = lightObj.AddComponent<Light>();
        m_previewLight.type = LightType.Point; // Directional은 위치 무관하게 씬 전체를 비추므로 여기선 금지 — 감쇠로 스테이지에만 영향
        m_previewLight.intensity = 2f;
    }

    // RawImage의 실제 가로/세로 비율(width/height)에 맞춰 텍스처를 다시 만듦 — 정사각형으로 제한하면 넓은 자리에서 함선이 작아 보이는 문제 방지
    private void CreatePreviewTexture(float aspect, int height)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(height * aspect));

        RenderTexture oldTexture = m_previewTexture;

        m_previewTexture = new RenderTexture(width, height, 24);
        m_previewTexture.antiAliasing = k_textureAntiAliasing;
        m_previewTexture.name = "ShipPreviewRT";
        m_previewCamera.targetTexture = m_previewTexture;

        if (oldTexture != null)
            oldTexture.Release();
    }

    // moduleInfo를 넘기면 빔/미사일/격납고 슬롯에 실제 모듈 메시를 붙여서 보여줌(비주얼 전용 — 전투/스탯 로직은 초기화 안 함)
    // null이면 함체 프리팹 기본 상태(슬롯마다 ModulePlaceholder 더미 메시) 그대로 표시
    public void ShowHull(ModuleData hull, ModuleHullInfo moduleInfo = null)
    {
        EnsureStageReady();
        Clear();

        if (hull == null) return;

        // 프리팹은 아키타입당 1개뿐이라 moduleSubType(레벨별로 다른 이름)이 아니라 CSV Import 시 캐싱된 실제 파일명을 사용
        string prefabName = string.IsNullOrEmpty(hull.hullPrefabName) == false ? hull.hullPrefabName : hull.moduleSubType;
        GameObject bodyPrefab = ObjectManager.Instance.LoadShipModulePrefab(EModuleType.hull.ToString(), prefabName);
        if (bodyPrefab == null) return;

        m_currentBodyInstance = Instantiate(bodyPrefab, m_spawnAnchor);
        m_currentBodyInstance.transform.localPosition = Vector3.zero;
        m_currentBodyInstance.transform.localRotation = Quaternion.identity;

        if (moduleInfo != null)
            AttachPreviewModules(m_currentBodyInstance, moduleInfo);

        FrameCameraOnInstance(m_currentBodyInstance);
    }

    // 슬롯에 실제 모듈 프리팹을 비주얼로만 붙임 — ModuleBeam/Missile/Hangar의 InitializeModule* 호출은 생략(SpaceShip/함대 컨텍스트가
    // 없는 정적 미리보기 스테이지라 전투/스탯 초기화 로직을 타면 널 참조 위험) — 메시만 붙이고 게임플레이 컴포넌트는 바로 제거
    private void AttachPreviewModules(GameObject bodyInstance, ModuleHullInfo moduleInfo)
    {
        ModuleSlot[] slots = bodyInstance.GetComponentsInChildren<ModuleSlot>(true);
        AttachPreviewModulesForList(slots, moduleInfo.beams);
        AttachPreviewModulesForList(slots, moduleInfo.missiles);
        AttachPreviewModulesForList(slots, moduleInfo.hangars);
        // 실드/요격체는 3D 슬롯/비주얼이 없는 논리 컴포넌트라(ModuleShield.cs 주석 참고) 미리보기 대상에서 제외
    }

    private void AttachPreviewModulesForList(ModuleSlot[] slots, List<ModuleInfo> moduleInfoList)
    {
        if (moduleInfoList == null) return;

        foreach (ModuleInfo info in moduleInfoList)
        {
            ModuleSlot targetSlot = FindSlot(slots, info.moduleType, info.slotIndex);
            if (targetSlot == null) continue;

            GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(info.moduleType.ToString(), info.moduleSubType);
            if (modulePrefab == null) continue;

            GameObject moduleObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation, targetSlot.transform);
            moduleObj.transform.localScale = Vector3.one;

            ModuleBase moduleComponent = moduleObj.GetComponent<ModuleBase>();
            if (moduleComponent != null)
                Destroy(moduleComponent);

            ModulePlaceholder placeholder = targetSlot.GetComponentInChildren<ModulePlaceholder>(true);
            if (placeholder != null)
                placeholder.gameObject.SetActive(false);
        }
    }

    private ModuleSlot FindSlot(ModuleSlot[] slots, EModuleType moduleType, int slotIndex)
    {
        foreach (ModuleSlot slot in slots)
        {
            if (slot.m_moduleSlotInfo.moduleType == moduleType && slot.m_moduleSlotInfo.slotIndex == slotIndex)
                return slot;
        }
        return null;
    }

    // 함선 크기/각도와 무관하게 카메라를 바운즈 중심에서 항상 k_cameraDistance만큼 떨어뜨려 배치하고, 그 거리에 맞춰 조명을 재배치.
    private void FrameCameraOnInstance(GameObject instance)
    {
        // ShieldGrid가 모든 함체에 미리 만들어두는 "ShieldSurface" 표면 메쉬는 실제 선체보다 boundScale/margin만큼
        // 일부러 더 크게 잡혀있어(ShieldGrid.ComputeBoundingBox) 바운즈 중심 산정에 포함되면 중심이 어긋난다 —
        // QuickOutline/Outline.cs가 아웃라인 대상에서 이 렌더러를 제외하는 것과 동일한 방식으로 여기서도 제외한다
        Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>();
        List<Renderer> hullRenderers = new List<Renderer>();
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i].gameObject.name == "ShieldSurface") continue;
            hullRenderers.Add(allRenderers[i]);
        }
        if (hullRenderers.Count == 0) return;

        Bounds bounds = hullRenderers[0].bounds;
        for (int i = 1; i < hullRenderers.Count; i++)
            bounds.Encapsulate(hullRenderers[i].bounds);

        // 함선의 전장은 보통 로컬 Z축 방향이라, 시선이 Z축에 가까우면 길이가 그대로 앞뒤로 찌부러져 보인다 —
        // X축 비중을 높여 옆에서 비스듬히 보는 각도로 고정
        Vector3 viewDirection = new Vector3(0.85f, -0.3f, 0.45f).normalized;
        Quaternion viewRotation = Quaternion.LookRotation(viewDirection, Vector3.up);

        m_previewCamera.transform.position = bounds.center - viewDirection * k_cameraDistance;
        m_previewCamera.transform.rotation = viewRotation;
        m_previewCamera.nearClipPlane = 0.05f;

        float boundsRadius = bounds.extents.magnitude;
        if (boundsRadius <= 0f) boundsRadius = 1f;
        m_previewCamera.farClipPlane = k_cameraDistance + boundsRadius * 2f + 10f;

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

    // aspectRatio: RawImage의 width/height — 함체 선택 화면을 열 때마다 최신 레이아웃 비율을 넘겨받아 텍스처를 맞춤
    // pixelHeight: RawImage가 화면에서 실제로 차지하는 세로 픽셀 수 — 텍스처가 그보다 작으면 늘려 그려져 흐려지므로 맞춰서 만듦(상·하한 클램프)
    public RenderTexture GetPreviewTexture(float aspectRatio = 1f, int pixelHeight = k_textureBaseHeight)
    {
        EnsureStageReady();

        int clampedHeight = Mathf.Clamp(pixelHeight, k_textureMinHeight, k_textureMaxHeight);
        bool isAspectChanged = aspectRatio > 0f && Mathf.Approximately(aspectRatio, m_currentAspect) == false;
        bool isHeightChanged = clampedHeight != m_currentHeight;
        if (isAspectChanged == true || isHeightChanged == true)
        {
            if (aspectRatio > 0f)
                m_currentAspect = aspectRatio;
            m_currentHeight = clampedHeight;
            CreatePreviewTexture(m_currentAspect, m_currentHeight);
        }

        return m_previewTexture;
    }

    public Transform GetPreviewRoot()
    {
        EnsureStageReady();
        return m_spawnAnchor;
    }
}
