#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// DataTableZone CSV 입출력 공통 유틸리티 — DataTableZoneEditor / ZonePreviewComponentEditor 공유
public static class DataTableZoneCSVUtility
{
    // CSV 전부 내보내기 (AssetDatabase.Refresh 포함)
    public static void ExportAll(DataTableZone table)
    {
        ExportZone(table);
        ExportCelestial(table);
        ExportGrid(table);
        AssetDatabase.Refresh();
    }

    // ZonePreview에서 편집하는 2개만 내보내기
    public static void ExportZoneAndCelestial(DataTableZone table)
    {
        ExportZone(table);
        ExportCelestial(table);
        AssetDatabase.Refresh();
    }

    // zoneList.gridWidth/gridHeight/enemy* → datatable_zone_grid.csv
    public static void ExportGrid(DataTableZone table)
    {
        const string path = "Assets/Resources/DataTable/Zone/datatable_zone_grid.csv";
        var sb = new StringBuilder();
        sb.AppendLine("zone_index,grid_width,grid_height,enemy_fleets,enemy_budget,enemy_max_cost_of_one_ship,enemy_deviation,enemy_max_ships_per_fleet,enemy_health_multiplier,enemy_attack_multiplier,enemy_beam_equip_slots,enemy_missile_equip_slots,enemy_hanger_equip_slots,enemy_shield_equip_slots,enemy_interceptor_equip_slots,exploration_point_reward,commander_exp_reward");
        foreach (ZoneConfig z in table.zoneList)
        {
            sb.AppendLine(
                $"{z.zoneIndex},{z.gridWidth},{z.gridHeight}," +
                $"{z.enemyFleetsPerCell},{z.enemyBudget},{z.enemyMaxCostOfOneShip},{z.enemyDeviation},{z.enemyMaxShipsPerFleet},{z.enemyHealthMultiplier},{z.enemyAttackMultiplier}," +
                $"{z.enemyBeamEquipSlots},{z.enemyMissileEquipSlots},{z.enemyHangerEquipSlots},{z.enemyShieldEquipSlots},{z.enemyInterceptorEquipSlots}," +
                $"{z.explorationPointReward},{z.commanderExpReward}");
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
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
