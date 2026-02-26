// Zone 데이터 테이블 — 탐사 존별 라운드·보상·자원 수확 설정 ScriptableObject
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// 각 슬롯에 장착할 모듈 설정
[System.Serializable]
public class EnemyModuleSlotConfig
{
    public EModuleType slotType;      // 슬롯 타입 (Engine, Beam, Missile, Hanger)
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

// 개별 적 함선 설정
[System.Serializable]
public class EnemyShipConfig
{
    public EModuleSubType bodySubType = EModuleSubType.body_t1_std;
    public int bodyLevel = 1;
    public List<EnemyModuleSlotConfig> moduleSlots = new List<EnemyModuleSlotConfig>();
}

// Zone 설정
[System.Serializable]
public class ZoneConfig
{
    public string zoneName;
    public string zoneDescription;
    public int shipCount = 1;      // 적 함선 개수
    public int moduleLevel = 1;    // 적 모듈 레벨
    public Material skyboxMaterial;  // 스카이박스 머티리얼
    
    public int zoneClearCount = 10;
    public float delayBeforeSpawn = 3f;
    public List<EnemyShipConfig> enemyShipConfigs;
    
    [Header("적 함선 킬 보상 (즉시 지급)")]
    public float killRewardMineral = 0f;
    public float killRewardMineralRare = 0f;
    public float killRewardMineralExotic = 0f;
    public float killRewardMineralDark = 0f;

    [Header("시간당 자원 수확량 (클리어 후)")]
    public float mineralPerHour = 3600f;
    public float mineralRarePerHour = 0f;
    public float mineralExoticPerHour = 0f;
    public float mineralDarkPerHour = 0f;

    // 실제 계산용 (시간당 → 초당 변환)
    public float MineralPerSecond => mineralPerHour / 3600f;
    public float MineralRarePerSecond => mineralRarePerHour / 3600f;
    public float MineralExoticPerSecond => mineralExoticPerHour / 3600f;
    public float MineralDarkPerSecond => mineralDarkPerHour / 3600f;


}

[CreateAssetMenu(fileName = "DataTableZone", menuName = "Custom/DataTableZone")]
public class DataTableZone : ScriptableObject
{
    public List<ZoneConfig> zones = new List<ZoneConfig>();

    public ZoneConfig GetZone(int index)
    {
        if (index < 0 || index >= zones.Count)
            return null;
        return zones[index];
    }

    public ZoneConfig GetZoneByName(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return null;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].zoneName == zoneName)
                return zones[i];
        }
        return null;
    }

    public int GetZoneIndex(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return -1;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].zoneName == zoneName)
                return i;
        }
        return -1;
    }

    // 다음 zone 반환 (없으면 null)
    public ZoneConfig GetNextZone(string currentZoneName)
    {
        int currentIndex = GetZoneIndex(currentZoneName);
        if (currentIndex < 0 || currentIndex + 1 >= zones.Count)
            return null;
        return zones[currentIndex + 1];
    }

    public int ZoneCount => zones.Count;

    // 서버용 export (필요한 필드만)
    public string ExportToJson()
    {
        var serverData = new List<object>();
        foreach (var zone in zones)
        {
            serverData.Add(new
            {
                zoneName = zone.zoneName,
                killRewardMineral = zone.killRewardMineral,
                killRewardMineralRare = zone.killRewardMineralRare,
                killRewardMineralExotic = zone.killRewardMineralExotic,
                killRewardMineralDark = zone.killRewardMineralDark,
                mineralPerHour = zone.mineralPerHour,
                mineralRarePerHour = zone.mineralRarePerHour,
                mineralExoticPerHour = zone.mineralExoticPerHour,
                mineralDarkPerHour = zone.mineralDarkPerHour
            });
        }
        return JsonConvert.SerializeObject(new { zones = serverData }, Formatting.Indented);
    }
}
