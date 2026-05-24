// Zone 데이터 테이블 — 탐사 존별 라운드·보상·자원 수확 설정 ScriptableObject
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// 행성 하나의 배치 정보
[System.Serializable]
public class CelestialBodyConfig
{
    public Vector3 position;
    public Vector3 scale = new Vector3(20f, 20f, 20f);

    [Header("Surface (Land + Sea)")]
    public Color deepSeaColor    = new Color(0.05f, 0.15f, 0.45f);
    public Color shallowSeaColor = new Color(0.10f, 0.35f, 0.65f);
    public Color coastColor      = new Color(0.75f, 0.70f, 0.50f);
    public Color grasslandColor  = new Color(0.28f, 0.55f, 0.18f);
    public Color forestColor     = new Color(0.08f, 0.28f, 0.08f);
    public Color desertColor     = new Color(0.80f, 0.65f, 0.30f);
    public Color highlandColor   = new Color(0.55f, 0.48f, 0.38f);
    [Range(0f, 1f)]   public float landCoverage  = 0.5f;
    [Range(0f, 360f)] public float landRotation  = 0f;

    [Header("Cloud Layer")]
    public bool  hasClouds     = true;
    public Color cloudColor    = new Color(1f, 1f, 1f, 0.85f);
    [Range(0f, 1f)]   public float cloudCoverage  = 0.5f;  // 구름 영역 비율
    [Range(0f, 360f)] public float cloudRotation  = 0f;
    public float cloudScale    = 1.02f;                 // Surface 구 대비 배율

    [Header("Atmosphere Layer")]
    public bool  hasAtmosphere = true;
    public Color atmosphereColor = new Color(0.3f, 0.6f, 1f);
    public float atmosphereScale = 1.10f;
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
// 각 슬롯에 장착할 모듈 설정
[System.Serializable]
public class EnemyModuleSlotConfig
{
    public EModuleType slotType;      // 슬롯 타입 (Beam, Missile, Hanger)
    public int slotIndex;              // 슬롯 인덱스
    public EModuleSubType moduleSubType; // 장착할 모듈의 SubType
    public int moduleLevel = 1;        // 장착할 모듈의 레벨

    public EnemyModuleSlotConfig() { }
    public EnemyModuleSlotConfig(EModuleType slotType, int slotIndex)
    {
        this.slotType = slotType;
        this.slotIndex = slotIndex;
        this.moduleSubType = CommonUtility.GetDefaultSubType(slotType);
        this.moduleLevel = 1;
    }
}

// 웨이브 1개의 적 함선 템플릿 — 함선별 모듈·스탯 배율 설정
[System.Serializable]
public class EnemyShipConfig
{
    public int shipIndex;
    public EModuleSubType bodySubType;
    public int bodyLevel;
    public List<EnemyModuleSlotConfig> moduleSlots = new List<EnemyModuleSlotConfig>();

    [Header("스탯 배율 (1.0 = 플레이어 동일)")]
    [Range(0.1f, 3.0f)] public float bodyMultiplier    = 1.0f;
    [Range(0.1f, 3.0f)] public float beamMultiplier    = 1.0f;
    [Range(0.1f, 3.0f)] public float missileMultiplier = 1.0f;
    [Range(0.1f, 3.0f)] public float hangerMultiplier  = 1.0f;
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
    // 적 함선 템플릿 큐 — 순서대로 1척씩 스폰, 개수 제한 없음
    public List<EnemyShipConfig> enemyShipConfigs;

    [Header("클리어 보상")]
    public int mineralClearReward = 0;     // [server] 매 클리어마다
    public int techPointClearReward = 0;   // [server] 최초 클리어 1회
    public int modulePointClearReward = 0; // [server] 최초 클리어 1회

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
    public List<ZoneStageConfig> zoneStageList = new List<ZoneStageConfig>();

    // groupIndex로 그룹 설정 조회 (배열 인덱스 기준)
    public ZoneConfig GetZone(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= zoneList.Count)
            return null;
        return zoneList[zoneIndex];
    }

    public ZoneStageConfig GetZoneStage(int zoneStageIndex)
    {
        if (zoneStageIndex < 0 || zoneStageIndex >= zoneStageList.Count)
            return null;
        return zoneStageList[zoneStageIndex];
    }

    public ZoneStageConfig GetZoneStageByName(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return null;
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
    public ZoneStageConfig GetZoneSpawnStage(int zoneIndex)
    {
        return GetZoneStageByName($"{zoneIndex}-0");
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

    // 서버용 export (필요한 필드만)
    public string ExportToJson()
    {
        var serverData = new List<object>();
        foreach (var zoneStage in zoneStageList)
        {
            serverData.Add(new
            {
                zoneName = zoneStage.zoneName,
                mineralClearReward = zoneStage.mineralClearReward,
                techPointClearReward = zoneStage.techPointClearReward,
                modulePointClearReward = zoneStage.modulePointClearReward
            });
        }
        return JsonConvert.SerializeObject(new { zoneStages = serverData }, Formatting.Indented);
    }
}
