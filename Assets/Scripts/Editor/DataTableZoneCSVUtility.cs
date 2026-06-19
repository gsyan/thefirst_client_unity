#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

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

    // zoneList → datatable_zone_camera.csv
    public static void ExportZone(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone_camera.csv";
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

    // enemyFleet.ships → datatable_zone_enemy.csv
    public static void ExportEnemy(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone_enemy.csv";
        var sb = new StringBuilder();
        sb.AppendLine("zone_stage,stage,ship_index,body_type,body_level,beam_type,beam_level,beam_count,missile_type,missile_level,missile_count,hanger_type,hanger_level,hanger_count,body_ratio,beam_ratio,missile_ratio,hanger_ratio");
        foreach (ZoneStageConfig s in table.zoneStageList)
        {
            if (s.enemyFleet == null || s.enemyFleet.ships == null) continue;
            int stage = ParseStage(s.zoneName);
            foreach (ShipInfo ship in s.enemyFleet.ships)
            {
                ModuleBodyInfo body = (ship.bodies != null && ship.bodies.Count > 0) ? ship.bodies[0] : null;
                EModuleSubType bodySubType = body != null ? body.moduleSubType : EModuleSubType.none;
                int bodyLevel = body != null ? body.moduleLevel : 1;

                var beams    = body != null && body.beams    != null ? body.beams.Where(m    => m.moduleSubType != EModuleSubType.none).OrderBy(m => m.slotIndex).ToList() : new System.Collections.Generic.List<ModuleInfo>();
                var missiles = body != null && body.missiles != null ? body.missiles.Where(m => m.moduleSubType != EModuleSubType.none).OrderBy(m => m.slotIndex).ToList() : new System.Collections.Generic.List<ModuleInfo>();
                var hangers  = body != null && body.hangers  != null ? body.hangers.Where(m  => m.moduleSubType != EModuleSubType.none).OrderBy(m => m.slotIndex).ToList() : new System.Collections.Generic.List<ModuleInfo>();

                string beamType    = beams.Count > 0    ? beams[0].moduleSubType.ToString()    : "";
                string beamLv      = beams.Count > 0    ? beams[0].moduleLevel.ToString()      : "";
                string beamCnt     = beams.Count > 0    ? beams.Count.ToString()               : "";
                string missileType = missiles.Count > 0 ? missiles[0].moduleSubType.ToString() : "";
                string missileLv   = missiles.Count > 0 ? missiles[0].moduleLevel.ToString()   : "";
                string missileCnt  = missiles.Count > 0 ? missiles.Count.ToString()            : "";
                string hangerType  = hangers.Count > 0  ? hangers[0].moduleSubType.ToString()  : "";
                string hangerLv    = hangers.Count > 0  ? hangers[0].moduleLevel.ToString()    : "";
                string hangerCnt   = hangers.Count > 0  ? hangers.Count.ToString()             : "";

                sb.AppendLine(
                    $"{s.zoneIndex},{stage},{ship.positionIndex},{bodySubType},{bodyLevel}," +
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
            "zone_index,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,scale_x,scale_y,scale_z," +
            "land_coverage,biome_blend,g_blend," +
            "deep_sea_color,shallow_sea_color,lowland_sand_color,lowland_green_color," +
            "plains_desert_color,plains_grass_color,plains_forest_color,highland_snow_color," +
            "has_polar_ice,ice_color,ice_color_edge,pole_ice_width," +
            "has_clouds,cloud_color,cloud_coverage,cloud_rotation,cloud_scale," +
            "cloud_mid_lat_opacity,cloud_mid_lat_center,cloud_mid_lat_width,cloud_softness," +
            "has_atmosphere,atmosphere_color,atmosphere_scale");
        foreach (ZoneConfig z in table.zoneList)
        {
            if (z.celestialBodies == null) continue;
            foreach (CelestialBodyConfig c in z.celestialBodies)
            {
                sb.AppendLine(
                    $"{z.zoneIndex}," +
                    $"{c.position.x},{c.position.y},{c.position.z}," +
                    $"{c.rotation.x},{c.rotation.y},{c.rotation.z}," +
                    $"{c.scale.x},{c.scale.y},{c.scale.z}," +
                    $"{c.landCoverage},{c.biomeBlend},{c.gBlend}," +
                    $"{ToHex(c.deepSeaColor)},{ToHex(c.shallowSeaColor)}," +
                    $"{ToHex(c.lowlandSandColor)},{ToHex(c.lowlandGreenColor)}," +
                    $"{ToHex(c.plainsDesertColor)},{ToHex(c.plainsGrassColor)},{ToHex(c.plainsForestColor)}," +
                    $"{ToHex(c.highlandSnowColor)}," +
                    $"{c.hasPolarIce},{ToHex(c.iceColor)},{ToHex(c.iceColorEdge)},{c.poleIceWidth}," +
                    $"{c.hasClouds},{ToHexA(c.cloudColor)},{c.cloudCoverage},{c.cloudRotation},{c.cloudScale}," +
                    $"{c.cloudMidLatOpacity},{c.cloudMidLatCenter},{c.cloudMidLatWidth},{c.cloudSoftness}," +
                    $"{c.hasAtmosphere},{ToHex(c.atmosphereColor)},{c.atmosphereScale}");
            }
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string ToHex(Color c) =>
        $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";

    private static string ToHexA(Color c) =>
        $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}{(int)(c.a * 255):X2}";

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
