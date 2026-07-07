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

// 적 함대 스폰 위치 프리셋 — index로 참조 (datatable_zone_enemy_fleet_position.csv)
// grade: 이 프리셋 세트가 유효한 기함 함체 그레이드 상한 — 스폰할 함대의 기함 그레이드 이하인 것 중 가장 작은 grade 그룹을 사용
[System.Serializable]
public class FleetPositionPreset
{
    public int   grade;
    public int   index;
    public float distance;
    public float rotX;
    public float rotY;
    public float rotZ;
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

    public float spawnTerm = 20f;   // 두 번째 함대부터의 등장 간격(초). fleetIndex번째 함대 등장 시각 = fleetIndex * spawnTerm
    public List<StageEnemyFleetSpawnConfig> enemyFleets = new List<StageEnemyFleetSpawnConfig>(); // [server]

    [Header("클리어 보상")]
    public int mineralClearReward = 0;     // [server] 매 클리어마다
    public int expClearReward = 0;         // [server] 매 클리어마다
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
    public List<FleetPositionPreset> fleetPositionPresets = new List<FleetPositionPreset>(); // 적 함대 스폰 위치 프리셋
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

    // zoneStageList 순서 기준 1-1부터 stageName까지(포함) expClearReward 누적합 — 커맨더 레벨 require_exp 산출용
    public int GetCumulativeExpUpToStage(string stageName)
    {
        if (string.IsNullOrEmpty(stageName)) return 0;
        int endIndex = GetZoneStageIndex(stageName);
        if (endIndex < 0) return 0;

        int sum = 0;
        for (int i = 0; i <= endIndex; i++)
            sum += zoneStageList[i].expClearReward;
        return sum;
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

    // positionIndex로 함대 스폰 위치 프리셋 조회 — 없으면 null
    public FleetPositionPreset GetFleetPosition(int positionIndex)
    {
        for (int i = 0; i < fleetPositionPresets.Count; i++)
        {
            if (fleetPositionPresets[i].index == positionIndex)
                return fleetPositionPresets[i];
        }
        return null;
    }

    // shipGradeLevel(기함 함체 그레이드) 이상인 grade 값 중 가장 작은 그룹에서 positionIndex로 조회
    // 그레이드가 높을수록(함선이 커질수록) 더 넓은 distance/각도 세트를 쓰도록 grade별로 프리셋을 나눠둘 수 있음
    public FleetPositionPreset GetFleetPosition(int shipGradeLevel, int positionIndex)
    {
        for (int i = fleetPositionPresets.Count - 1; i >= 0; i--)
        {
            FleetPositionPreset preset = fleetPositionPresets[i];
            if (shipGradeLevel >= preset.grade && positionIndex == preset.index)
                return preset;
        }
        return null;
    }

    // shipGradeLevel에 적용되는 grade 그룹에 몇 개의 positionIndex가 있는지 (0 = 해당 그레이드용 프리셋 없음)
    public int GetFleetPositionCount(int shipGradeLevel)
    {
        int bestGrade = int.MaxValue;
        for (int i = 0; i < fleetPositionPresets.Count; i++)
        {
            int grade = fleetPositionPresets[i].grade;
            if (grade >= shipGradeLevel && grade < bestGrade)
                bestGrade = grade;
        }
        if (bestGrade == int.MaxValue) return 0;

        int count = 0;
        for (int i = 0; i < fleetPositionPresets.Count; i++)
        {
            if (fleetPositionPresets[i].grade == bestGrade)
                count++;
        }
        return count;
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

    // 서버용 export — enemyFleets 리스트 직렬화
    public string ExportToJson()
    {
        var serverData = new List<object>();
        foreach (var zoneStage in zoneStageList)
        {
            serverData.Add(new
            {
                zoneName               = zoneStage.zoneName,
                mineralClearReward     = zoneStage.mineralClearReward,
                expClearReward         = zoneStage.expClearReward,
                modulePointClearReward = zoneStage.modulePointClearReward,
                enemyFleets            = zoneStage.enemyFleets
            });
        }
        return JsonConvert.SerializeObject(new { zoneStages = serverData }, Formatting.Indented);
    }
}
