#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

// DataTableZone CSV 입출력 공통 유틸리티 — DataTableZoneEditor / ZonePreviewComponentEditor 공유
public static class DataTableZoneCSVUtility
{
    // 4개 CSV 전부 내보내기 (AssetDatabase.Refresh 포함)
    public static void ExportAll(DataTableZone table)
    {
        ExportZone(table);
        ExportZoneStage(table);
        ExportEnemy(table);
        ExportCelestial(table);
        AssetDatabase.Refresh();
    }

    // ZonePreview에서 편집하는 3개만 내보내기
    public static void ExportZoneAndCelestial(DataTableZone table)
    {
        ExportZone(table);
        ExportZoneStage(table);
        ExportCelestial(table);
        AssetDatabase.Refresh();
    }

    // zoneList → datatable_zone.csv
    public static void ExportZone(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone.csv";
        var sb = new StringBuilder();
        sb.AppendLine("zone_index,cam_target_x,cam_target_y,cam_target_z,cam_zoom,cam_rot_x,cam_rot_y");
        foreach (ZoneConfig z in table.zoneList)
        {
            sb.AppendLine(
                $"{z.zoneIndex}," +
                $"{z.galaxyCameraTarget.x},{z.galaxyCameraTarget.y},{z.galaxyCameraTarget.z}," +
                $"{z.galaxyCameraZoom},{z.galaxyCameraRotX},{z.galaxyCameraRotY}");
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // zoneStageList → datatable_zone_stage.csv
    public static void ExportZoneStage(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone_stage.csv";
        var sb = new StringBuilder();
        sb.AppendLine("zone,stage,mineral_clear_reward,tech_point_clear_reward,module_point_clear_reward,spawn_delay,ship_spawn_interval,fleet_pos_x,fleet_pos_y,fleet_pos_z,fleet_rot_y");
        foreach (ZoneStageConfig s in table.zoneStageList)
        {
            int stage = ParseStage(s.zoneName);
            sb.AppendLine(
                $"{s.zoneIndex},{stage},{s.mineralClearReward},{s.techPointClearReward},{s.modulePointClearReward}," +
                $"{s.delayBeforeSpawn},{s.shipSpawnInterval}," +
                $"{s.fleetPosition.x},{s.fleetPosition.y},{s.fleetPosition.z}," +
                $"{s.fleetRotationY}");
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // enemyShipConfigs → datatable_zone_enemy.csv
    public static void ExportEnemy(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone_enemy.csv";
        var sb = new StringBuilder();
        sb.AppendLine("zone_stage,stage,ship_index,body_type,body_level,beam_type,beam_level,beam_count,missile_type,missile_level,missile_count,hanger_type,hanger_level,hanger_count,body_ratio,beam_ratio,missile_ratio,hanger_ratio");
        foreach (ZoneStageConfig s in table.zoneStageList)
        {
            if (s.enemyShipConfigs == null) continue;
            int stage = ParseStage(s.zoneName);
            foreach (EnemyShipConfig ship in s.enemyShipConfigs)
            {
                var beamSlots    = ship.moduleSlots.Where(sl => sl.slotType == EModuleType.beam    && sl.moduleSubType != EModuleSubType.none).OrderBy(sl => sl.slotIndex).ToList();
                var missileSlots = ship.moduleSlots.Where(sl => sl.slotType == EModuleType.missile  && sl.moduleSubType != EModuleSubType.none).OrderBy(sl => sl.slotIndex).ToList();
                var hangerSlots  = ship.moduleSlots.Where(sl => sl.slotType == EModuleType.hanger   && sl.moduleSubType != EModuleSubType.none).OrderBy(sl => sl.slotIndex).ToList();

                string beamType    = beamSlots.Count > 0    ? beamSlots[0].moduleSubType.ToString()    : "";
                string beamLv      = beamSlots.Count > 0    ? beamSlots[0].moduleLevel.ToString()      : "";
                string beamCnt     = beamSlots.Count > 0    ? beamSlots.Count.ToString()               : "";
                string missileType = missileSlots.Count > 0 ? missileSlots[0].moduleSubType.ToString() : "";
                string missileLv   = missileSlots.Count > 0 ? missileSlots[0].moduleLevel.ToString()   : "";
                string missileCnt  = missileSlots.Count > 0 ? missileSlots.Count.ToString()            : "";
                string hangerType  = hangerSlots.Count > 0  ? hangerSlots[0].moduleSubType.ToString()  : "";
                string hangerLv    = hangerSlots.Count > 0  ? hangerSlots[0].moduleLevel.ToString()    : "";
                string hangerCnt   = hangerSlots.Count > 0  ? hangerSlots.Count.ToString()             : "";

                sb.AppendLine(
                    $"{s.zoneIndex},{stage},{ship.shipIndex},{ship.bodySubType},{ship.bodyLevel}," +
                    $"{beamType},{beamLv},{beamCnt}," +
                    $"{missileType},{missileLv},{missileCnt}," +
                    $"{hangerType},{hangerLv},{hangerCnt}," +
                    $"{ship.bodyMultiplier},{ship.beamMultiplier},{ship.missileMultiplier},{ship.hangerMultiplier}");
            }
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // zoneList.celestialBodies → datatable_zone_celestial.csv
    public static void ExportCelestial(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone_celestial.csv";
        var sb = new StringBuilder();
        sb.AppendLine(
            "zone_index,pos_x,pos_y,pos_z,scale_x,scale_y,scale_z," +
            "deepsea_r,deepsea_g,deepsea_b,shallowsea_r,shallowsea_g,shallowsea_b," +
            "coast_r,coast_g,coast_b,grassland_r,grassland_g,grassland_b,forest_r,forest_g,forest_b,desert_r,desert_g,desert_b,highland_r,highland_g,highland_b," +
            "land_coverage,land_rotation," +
            "has_clouds,cloud_r,cloud_g,cloud_b,cloud_a,cloud_coverage,cloud_rotation,cloud_scale," +
            "has_atmosphere,atm_r,atm_g,atm_b,atmosphere_scale");
        foreach (ZoneConfig z in table.zoneList)
        {
            if (z.celestialBodies == null) continue;
            foreach (CelestialBodyConfig c in z.celestialBodies)
            {
                sb.AppendLine(
                    $"{z.zoneIndex}," +
                    $"{c.position.x},{c.position.y},{c.position.z}," +
                    $"{c.scale.x},{c.scale.y},{c.scale.z}," +
                    $"{c.deepSeaColor.r},{c.deepSeaColor.g},{c.deepSeaColor.b}," +
                    $"{c.shallowSeaColor.r},{c.shallowSeaColor.g},{c.shallowSeaColor.b}," +
                    $"{c.coastColor.r},{c.coastColor.g},{c.coastColor.b}," +
                    $"{c.grasslandColor.r},{c.grasslandColor.g},{c.grasslandColor.b}," +
                    $"{c.forestColor.r},{c.forestColor.g},{c.forestColor.b}," +
                    $"{c.desertColor.r},{c.desertColor.g},{c.desertColor.b}," +
                    $"{c.highlandColor.r},{c.highlandColor.g},{c.highlandColor.b}," +
                    $"{c.landCoverage},{c.landRotation}," +
                    $"{c.hasClouds},{c.cloudColor.r},{c.cloudColor.g},{c.cloudColor.b},{c.cloudColor.a}," +
                    $"{c.cloudCoverage},{c.cloudRotation},{c.cloudScale}," +
                    $"{c.hasAtmosphere},{c.atmosphereColor.r},{c.atmosphereColor.g},{c.atmosphereColor.b}," +
                    $"{c.atmosphereScale}");
            }
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // "1-3" → 3, 파싱 실패 시 0
    public static int ParseStage(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return 0;
        string[] parts = zoneName.Split('-');
        if (parts.Length >= 2 && int.TryParse(parts[1], out int stage))
            return stage;
        return 0;
    }
}
#endif
