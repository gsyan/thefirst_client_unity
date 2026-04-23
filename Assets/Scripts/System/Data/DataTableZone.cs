// Zone 데이터 테이블 — 탐사 존별 라운드·보상·자원 수확 설정 ScriptableObject
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// Zone 그룹 공유 설정 — 같은 Zone(1-1, 1-2, 1-3...)이 skybox를 공유
[System.Serializable]
public class ZoneConfig
{
    public int zoneIndex;           // 그룹 키 (0 = 안전구역 Zone-0, X-Y의 X값)
    public Material skyboxMaterial; // 이 Zone의 스카이박스

    [Header("갤럭시 뷰 카메라 앵커 (탐사 탭 그룹 선택 시)")]
    public Vector3 galaxyCameraTarget; // 카메라가 바라볼 월드 좌표
    public float   galaxyCameraZoom;   // 줌값 (100~400 범위 권장)
    public float   galaxyCameraRotX;   // 앙각 (0~80)
    public float   galaxyCameraRotY;   // 수평 회전
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
    public bool isFlagShip; // true = 기함(슬롯 0 전용), false = 일반(슬롯 0 제외)
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
    public int maxConcurrentEnemyShips = 3;  // 동시에 존재 가능한 최대 적 함선 수 (슬롯 수)
    // 적 함선 템플릿 큐 — 순서대로 1척씩 스폰, 개수 제한 없음
    public List<EnemyShipConfig> enemyShipConfigs;

    [Header("클리어시 광물 획득량")]
    public int mineralClearReward = 0;   // [server]

    [Header("스카이박스 회전 (스테이지별)")]
    [Range(0f, 360f)] public float skyboxRotation = 0f;

    [Header("아군 함대 위치 (절대 좌표)")]
    public Vector3 fleetPosition;   // 이 존 진입 시 아군 함대가 배치될 월드 좌표
}

[CreateAssetMenu(fileName = "DataTableZone", menuName = "Custom/DataTableZone")]
public class DataTableZone : ScriptableObject
{
    // 함선개수(x) 그룹별 행성 세트 — 같은 그룹의 모든 스테이지가 공유
    public List<ZoneConfig> zoneList = new List<ZoneConfig>();
    public List<ZoneStageConfig> zoneStageList = new List<ZoneStageConfig>();

    // groupIndex로 그룹 설정 조회 (0 = Zone-0 안전구역)
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

    // 서버용 export (필요한 필드만)
    public string ExportToJson()
    {
        var serverData = new List<object>();
        foreach (var zoneStage in zoneStageList)
        {
            serverData.Add(new
            {
                zoneName = zoneStage.zoneName,
                mineralClearReward = zoneStage.mineralClearReward
            });
        }
        return JsonConvert.SerializeObject(new { zoneStages = serverData }, Formatting.Indented);
    }
}
