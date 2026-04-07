// Zone 데이터 테이블 — 탐사 존별 라운드·보상·자원 수확 설정 ScriptableObject
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// Zone 그룹 공유 설정 — 같은 Zone(1-1, 1-2, 1-3...)이 skybox를 공유
[System.Serializable]
public class ZoneConfig
{
    public int zoneIndex;          // 그룹 키 (0 = 안전구역 Zone-0, X-Y의 X값)
    public Material skyboxMaterial; // 이 Zone의 스카이박스

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

// 웨이브 1개의 적 함선 템플릿 — shipCount만큼 복제 스폰, 배율 포함
[System.Serializable]
public class EnemyShipConfig
{
    public int shipIndex;
    public EModuleSubType bodySubType;
    public int bodyLevel;
    public List<EnemyModuleSlotConfig> moduleSlots = new List<EnemyModuleSlotConfig>();
}

// Zone 설정
[System.Serializable]
public class ZoneStageConfig
{
    public string zoneName;
    public string zoneDescription;
    public int zoneIndex = 1;      // 그룹 키 (X-Y의 X, 스카이박스 공유 단위)

    public float delayBeforeSpawn = 3f;
    // 적 함대를 구성하는 함선 템플릿 목록 — 모든 템플릿이 한 함대로 동시 스폰
    public List<EnemyShipConfig> enemyShipConfigs;

    [Header("적 함선 킬 보상 (즉시 지급)")]
    public float killRewardMineral = 0f;
    public float killRewardMineralRare = 0f;
    public float killRewardMineralExotic = 0f;
    public float killRewardMineralDark = 0f;

    [Header("시간당 자원 수확량 (클리어 후)")]
    public float mineralPerHour = 0f;
    public float mineralRarePerHour = 0f;
    public float mineralExoticPerHour = 0f;
    public float mineralDarkPerHour = 0f;

    [Header("스카이박스 회전 (스테이지별)")]
    [Range(0f, 360f)] public float skyboxRotation = 0f;

    [Header("아군 함대 위치 (절대 좌표)")]
    public Vector3 fleetPosition;   // 이 존 진입 시 아군 함대가 배치될 월드 좌표

    // 실제 계산용 (시간당 → 초당 변환)
    public float MineralPerSecond => mineralPerHour / 3600f;
    public float MineralRarePerSecond => mineralRarePerHour / 3600f;
    public float MineralExoticPerSecond => mineralExoticPerHour / 3600f;
    public float MineralDarkPerSecond => mineralDarkPerHour / 3600f;

    [Header("스탯 배율 (1.0 = 플레이어 동일)")]
    [Range(0.1f, 3.0f)] public float enemyBodyMultiplier    = 1.0f;
    [Range(0.1f, 3.0f)] public float enemyBeamMultiplier    = 1.0f;
    [Range(0.1f, 3.0f)] public float enemyMissileMultiplier = 1.0f;
    [Range(0.1f, 3.0f)] public float enemyHangerMultiplier  = 1.0f;
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
                killRewardMineral = zoneStage.killRewardMineral,
                killRewardMineralRare = zoneStage.killRewardMineralRare,
                killRewardMineralExotic = zoneStage.killRewardMineralExotic,
                killRewardMineralDark = zoneStage.killRewardMineralDark,
                mineralPerHour = zoneStage.mineralPerHour,
                mineralRarePerHour = zoneStage.mineralRarePerHour,
                mineralExoticPerHour = zoneStage.mineralExoticPerHour,
                mineralDarkPerHour = zoneStage.mineralDarkPerHour
            });
        }
        return JsonConvert.SerializeObject(new { zoneStages = serverData }, Formatting.Indented);
    }
}
