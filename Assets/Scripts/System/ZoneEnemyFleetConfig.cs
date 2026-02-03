using System.Collections.Generic;
using UnityEngine;

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
    public List<WaveConfig> waves = new List<WaveConfig>();

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

[CreateAssetMenu(fileName = "ZoneEnemyFleetConfig", menuName = "Custom/ZoneEnemyFleetConfig")]
public class ZoneEnemyFleetConfig : ScriptableObject
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
}
