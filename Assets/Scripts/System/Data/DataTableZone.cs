// Zone 데이터 테이블 — 탐사 존별 라운드·보상·자원 수확 설정 ScriptableObject
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// 행성 하나의 배치 정보
[System.Serializable]
public class CelestialBodyConfig
{
    public Vector3 position = new Vector3(0f, 0f, 1000f);
    public Vector3 rotation = Vector3.zero;
    public Vector3 scale = new Vector3(500f, 500f, 500f);

    [HideInInspector] public Color deepSeaColor       = CommonUtility.HexColor("#0D2673");
    [HideInInspector] public Color shallowSeaColor    = CommonUtility.HexColor("#1A59A6");
    [HideInInspector] public Color lowlandSandColor   = CommonUtility.HexColor("#BFB380");
    [HideInInspector] public Color lowlandGreenColor  = CommonUtility.HexColor("#90C060");
    [HideInInspector] public Color plainsDesertColor  = CommonUtility.HexColor("#A99159");
    [HideInInspector] public Color plainsGrassColor   = CommonUtility.HexColor("#478C2E");
    [HideInInspector] public Color plainsForestColor  = CommonUtility.HexColor("#236523");
    [HideInInspector] public Color highlandSnowColor  = CommonUtility.HexColor("#E8F0F5");
    [Header("Surface (Common)")]
    [Range(0f, 1f)]   public float landCoverage = 0.5f;
    [Range(0f, 0.2f)] public float biomeBlend   = 0.01f;
    [Range(0f, 5f)]   public float gBlend       = 0.02f;

    [Header("Cloud Layer")]
    public bool      hasClouds     = true;
    public Color     cloudColor    = CommonUtility.HexColor("#FFFFFFD9");
    public Texture2D cloudMaskTex;                          // CloudMaskPainter로 생성한 R채널 텍스처
    [Range(0f, 1f)]   public float cloudCoverage  = 0.5f;  // 구름 영역 비율
    [Range(0f, 360f)] public float cloudRotation     = 0f;
    public float cloudScale       = 1.01f;              // Surface 구 대비 배율
    [Range(0f, 1f)]   public float cloudMidLatOpacity = 0.75f;
    [Range(0.1f, 0.45f)] public float cloudMidLatCenter = 0.37f;
    [Range(0f, 0.5f)] public float cloudMidLatWidth   = 0.19f;
    [Range(0f, 0.5f)] public float cloudSoftness      = 0.5f;

    [Header("Atmosphere Layer")]
    public bool  hasAtmosphere = true;
    public Color atmosphereColor = CommonUtility.HexColor("#4D99FF");
    public float atmosphereScale = 1.01f;

    [Header("Polar Ice")]
    public bool  hasPolarIce   = false;
    public Color iceColor     = CommonUtility.HexColor("#F2FAFF");
    public Color iceColorEdge = CommonUtility.HexColor("#ADD1F0");
    [Range(0f, 0.4f)] public float poleIceWidth = 0.12f;
}

// Zone 그룹 공유 설정 — 같은 Zone(1-1, 1-2, 1-3...)이 천체·카메라 설정을 공유
[System.Serializable]
public class ZoneConfig
{
    public int zoneIndex; // 그룹 키 (X-Y의 X값)

    [Header("갤럭시 뷰 카메라 앵커 (탐사 탭 그룹 선택 시)")]
    public Vector3 galaxyCameraTarget;
    public float   galaxyCameraZoom;   // 줌값 (100~400 범위 권장)
    public float   galaxyCameraRotX;   // 앙각 (0~80)
    public float   galaxyCameraRotY;   // 수평 회전

    [Header("천체 배치 (ZonePreviewComponent로 시각 편집)")]
    public List<CelestialBodyConfig> celestialBodies = new List<CelestialBodyConfig>();
}
// Zone 설정
[System.Serializable]
public class ZoneStageConfig
{
    public string zoneName; // [server]
    public string zoneDescription;
    public int zoneIndex = 1;      // 그룹 키 (X-Y의 X, 스카이박스 공유 단위)

    public float delayBeforeSpawn = 3f;
    public float shipSpawnInterval = 1.5f;   // 함선 간 스폰 딜레이
    public FleetInfo enemyFleet; // [server]

    [Header("클리어 보상")]
    public int mineralClearReward = 0;     // [server] 매 클리어마다
    public int techPointClearReward = 0;   // [server] 최초 클리어 1회
    public int modulePointClearReward = 0; // [server] 최초 클리어 1회

    [Header("전투 시작 딜레이 (초, 0 = 즉시 발사)")]
    public float playerFireDelaySec = 0f;
    public float enemyFireDelaySec  = 0f;

    [Header("아군 함대 위치/방향 (galaxyCameraTarget 기준 상대 좌표)")]
    public Vector3 fleetPosition;
    [Range(0f, 360f)] public float fleetRotationY;

    [Header("갤럭시 뷰 UI — 마커 레이블 오프셋 (스크린 픽셀)")]
    public Vector2 labelScreenOffset = new Vector2(80f, 60f);
}

[CreateAssetMenu(fileName = "DataTableZone", menuName = "Custom/DataTableZone")]
public class DataTableZone : ScriptableObject
{
    // 함선개수(x) 그룹별 행성 세트 — 같은 그룹의 모든 스테이지가 공유
    public List<ZoneConfig> zoneList = new List<ZoneConfig>();
    public List<ZoneStageConfig> zoneStageList = new List<ZoneStageConfig>();   // 인스팩터 에서만 사용
    private Dictionary<int, List<ZoneStageConfig>> m_stagesByZone;              // 게임 로직에 사용

    private void OnEnable()
    {
        BuildRuntimeCache();
    }

    public void BuildRuntimeCache()
    {
        m_stagesByZone = new Dictionary<int, List<ZoneStageConfig>>();
        for (int i = 0; i < zoneStageList.Count; i++)
        {
            int zoneIndex = zoneStageList[i].zoneIndex;
            if (m_stagesByZone.ContainsKey(zoneIndex) == false)
                m_stagesByZone[zoneIndex] = new List<ZoneStageConfig>();
            m_stagesByZone[zoneIndex].Add(zoneStageList[i]);
        }
    }

    public List<ZoneStageConfig> GetStagesByZone(int zoneIndex)
    {
        if (m_stagesByZone == null) BuildRuntimeCache();
        m_stagesByZone.TryGetValue(zoneIndex, out List<ZoneStageConfig> list);
        return list;
    }

    // groupIndex로 그룹 설정 조회 (배열 인덱스 기준)
    public ZoneConfig GetZone(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= zoneList.Count)
            return null;
        return zoneList[zoneIndex];
    }

    public ZoneStageConfig GetZoneStageByName(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return null;
        int dashIdx = zoneName.IndexOf('-');
        if (dashIdx > 0 && int.TryParse(zoneName[..dashIdx], out int zoneIndex))
        {
            List<ZoneStageConfig> stages = GetStagesByZone(zoneIndex);
            if (stages != null)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    if (stages[i].zoneName == zoneName)
                        return stages[i];
                }
            }
            return null;
        }
        for (int i = 0; i < zoneStageList.Count; i++)
        {
            if (zoneStageList[i].zoneName == zoneName)
                return zoneStageList[i];
        }
        return null;
    }

    public int GetZoneStageIndex(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return -1;
        for (int i = 0; i < zoneStageList.Count; i++)
        {
            if (zoneStageList[i].zoneName == zoneName)
                return i;
        }
        return -1;
    }

    // 이름 목록으로 ZoneConfig 반환 (순서 무관, Zone-0 제외)
    public List<ZoneStageConfig> GetZoneStagesByNames(List<string> zoneNames)
    {
        var result = new List<ZoneStageConfig>();
        if (zoneNames == null) return result;
        for (int i = 0; i < zoneNames.Count; i++)
        {
            var zone = GetZoneStageByName(zoneNames[i]);
            if (zone != null) result.Add(zone);
        }
        return result;
    }

    public int ZoneStageCount => zoneStageList.Count;

    // zoneIndex 필드 기준 ZoneConfig 검색 (GetZone은 배열 인덱스 기준)
    public ZoneConfig GetZoneByZoneIndex(int zoneIndex)
    {
        for (int i = 0; i < zoneList.Count; i++)
        {
            if (zoneList[i].zoneIndex == zoneIndex)
                return zoneList[i];
        }
        return null;
    }

    // 존의 x-0 스폰 마커 스테이지 반환 — 없으면 null
    public ZoneStageConfig GetZoneFirstStage(int zoneIndex)
    {
        return GetZoneStageByName($"{zoneIndex}-1");
    }

    // 존 중심점 (galaxyCameraTarget 기준) — ZoneConfig가 없으면 zero 반환
    public Vector3 GetZoneCenter(int zoneIndex)
    {
        ZoneConfig zone = GetZoneByZoneIndex(zoneIndex);
        if (zone == null)
            return Vector3.zero;
        return zone.galaxyCameraTarget;
    }

    // fleetPosition(상대) + 존 중심점 → 절대 월드 좌표
    public Vector3 ResolveFleetWorldPosition(ZoneStageConfig stage)
    {
        return GetZoneCenter(stage.zoneIndex) + stage.fleetPosition;
    }

    // 서버용 export — enemyFleet은 FleetInfo 그대로 직렬화
    public string ExportToJson()
    {
        var serverData = new List<object>();
        foreach (var zoneStage in zoneStageList)
        {
            serverData.Add(new
            {
                zoneName               = zoneStage.zoneName,
                mineralClearReward     = zoneStage.mineralClearReward,
                techPointClearReward   = zoneStage.techPointClearReward,
                modulePointClearReward = zoneStage.modulePointClearReward,
                enemyFleet             = zoneStage.enemyFleet
            });
        }
        return JsonConvert.SerializeObject(new { zoneStages = serverData }, Formatting.Indented);
    }
}
