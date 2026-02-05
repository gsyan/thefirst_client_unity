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
        this.moduleSubType = GetDefaultSubType(slotType);
        this.moduleLevel = 1;
    }

    public static EModuleSubType GetDefaultSubType(EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.Engine: return EModuleSubType.Engine_Standard;
            case EModuleType.Beam: return EModuleSubType.Beam_Standard;
            case EModuleType.Missile: return EModuleSubType.Missile_Standard;
            case EModuleType.Hanger: return EModuleSubType.Hanger_Standard;
            default: return EModuleSubType.None;
        }
    }
}

// 개별 적 함선 설정
[System.Serializable]
public class EnemyShipConfig
{
    public EModuleSubType bodySubType = EModuleSubType.Body_Battle;
    public int bodyLevel = 1;
    public List<EnemyModuleSlotConfig> moduleSlots = new List<EnemyModuleSlotConfig>();
}

// Wave별 설정
[System.Serializable]
public class WaveConfig
{
    [Header("Wave Timing")]
    public float delayBeforeWave = 5f;  // 이 Wave 시작 전 대기 시간(초)

    [Header("Enemy Ships")]
    public List<EnemyShipConfig> enemyShips = new List<EnemyShipConfig>();
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
    public List<WaveConfig> waves = new List<WaveConfig>();
    
    public float clearMineral = 0f;
    public float clearMineralRare = 0f;
    public float clearMineralExotic = 0f;
    public float clearMineralDark = 0f;
    
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

    public int TotalWaveCount => waves.Count;
    public int TotalEnemyShipCount
    {
        get
        {
            int count = 0;
            foreach (var wave in waves)
                count += wave.enemyShips.Count;
            return count;
        }
    }
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
                clearMineral = zone.clearMineral,
                clearMineralRare = zone.clearMineralRare,
                clearMineralExotic = zone.clearMineralExotic,
                clearMineralDark = zone.clearMineralDark,
                mineralPerHour = zone.mineralPerHour,
                mineralRarePerHour = zone.mineralRarePerHour,
                mineralExoticPerHour = zone.mineralExoticPerHour,
                mineralDarkPerHour = zone.mineralDarkPerHour
            });
        }
        return JsonConvert.SerializeObject(new { zones = serverData }, Formatting.Indented);
    }
}
