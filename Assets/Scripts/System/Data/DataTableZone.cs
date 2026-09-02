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

// 그리드 셀 하나의 타입 오버라이드 — Normal(기본)이 아닌 셀만 담음(희소 리스트)
// EGridCellType/EGridEventType은 CommonDefine.cs에 정의(서버 enum 생성기(generate_common_define.py)가 그 파일만 스캔하기 때문)
[System.Serializable]
public class GridCellOverride
{
    public int row;
    public int col;
    public EGridCellType type;
    public EGridEventType eventType; // type == Event 일 때만 유효
}

// Zone 그룹 공유 설정 — 같은 Zone(1-1, 1-2, 1-3...)이 천체·카메라 설정을 공유
[System.Serializable]
public class ZoneConfig
{
    public int zoneIndex; // [server] 그룹 키 (X-Y의 X값)

    [Header("갤럭시 뷰 카메라 앵커 (탐사 탭 그룹 선택 시)")]
    public Vector3 galaxyCameraTarget;
    public float   galaxyCameraZoom;   // 줌값 (100~400 범위 권장)
    public float   galaxyCameraRotX;   // 앙각 (0~80)
    public float   galaxyCameraRotY;   // 수평 회전

    [Header("천체 배치 (ZonePreviewComponent로 시각 편집)")]
    public List<CelestialBodyConfig> celestialBodies = new List<CelestialBodyConfig>();

    [Header("탐사 그리드")]
    public int gridWidth = 3;  // [server] 서버가 클라와 동일하게 셀 적함대를 재계산하려면 그리드 크기가 필요
    public int gridHeight = 3; // [server]
    public List<GridCellOverride> cellOverrides = new List<GridCellOverride>(); // [server] Normal이 아닌 셀만 저장(희소 리스트) — DataTableZoneEditor 그리드 버튼으로 편집

    [Header("셀 적함대 절차적 생성")]
    public int enemyFleetsPerCell = 1;    // [server] 셀당 순차 웨이브 개수
    public int enemyBudget = 100;         // [server] 웨이브 1개의 지휘력 예산
    public int enemyMaxCostOfOneShip = 100; // [server] 웨이브에 편성 가능한 함선 1척의 fullEquipCost 상한
    public int enemyDeviation = 0;        // [server] enemyMaxCostOfOneShip 랜덤 편차
    public int enemyMaxShipsPerFleet = 5; // [server] 웨이브 1개의 함선 수 상한
    public float enemyHealthMultiplier = 1.0f; // [server] 이 존의 적함대 체력 배율 (0.1=10%, 1.0=원본)
    public float enemyAttackMultiplier = 1.0f; // [server] 이 존의 적함대 공격력 배율 (0.1=10%, 1.0=원본)
    public int enemyBeamEquipSlots = 9;        // [server] 빔 슬롯 총 장착 목표 개수(기본 로드아웃 포함, 함체 최대 슬롯 수를 넘으면 전부 장착)
    public int enemyMissileEquipSlots = 9;     // [server] 미사일 슬롯 총 장착 목표 개수
    public int enemyHangarEquipSlots = 9;      // [server] 함재기 슬롯 총 장착 목표 개수
    public int enemyShieldEquipSlots = 9;      // [server] 실드 장착 여부 — 실드는 슬롯 1개뿐이라 사실상 0/1 스위치 (0=미장착, 1 이상=장착)
    public int enemyInterceptorEquipSlots = 9; // [server] 요격체 장착 여부 — 요격체도 슬롯 1개뿐이라 사실상 0/1 스위치

    [Header("셀 클리어 보상 (웨이브가 있던 셀만 적립, 존 단위 고정값)")]
    public int explorationPointReward = 0; // [server] 적 함대 성능(commandCost)과 무관한 고정 탐험 포인트 적립량
    public int commanderExpReward = 0;     // [server] 고정 지휘관 경험치 적립량
}
[CreateAssetMenu(fileName = "DataTableZone", menuName = "Custom/DataTableZone")]
public class DataTableZone : ScriptableObject
{
    // 함선개수(x) 그룹별 행성 세트 — 같은 그룹의 모든 스테이지가 공유
    public List<ZoneConfig> zoneList = new List<ZoneConfig>();

    // groupIndex로 그룹 설정 조회 (배열 인덱스 기준)
    public ZoneConfig GetZone(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= zoneList.Count)
            return null;
        return zoneList[zoneIndex];
    }

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

    // 존 중심점 (galaxyCameraTarget 기준) — ZoneConfig가 없으면 zero 반환
    public Vector3 GetZoneCenter(int zoneIndex)
    {
        ZoneConfig zone = GetZoneByZoneIndex(zoneIndex);
        if (zone == null)
            return Vector3.zero;
        return zone.galaxyCameraTarget;
    }

    // 서버용 export — ZoneConfig의 [server] 마커 필드만 골라 서버 ZoneConfigData(generate_zone_config.py로 생성)와 동일한 shape로 내보냄
    // 서버가 ZoneEnemyFleetGenerator.java(ExplorationEnemyFleetGenerator.cs 포팅)로 클라와 동일한 셀 적함대를 재계산하는 데 사용
    public string ExportToJson()
    {
        var zoneConfigs = new List<object>();
        foreach (ZoneConfig z in zoneList)
        {
            zoneConfigs.Add(new
            {
                zoneIndex             = z.zoneIndex,
                gridWidth             = z.gridWidth,
                gridHeight            = z.gridHeight,
                enemyFleetsPerCell    = z.enemyFleetsPerCell,
                enemyBudget           = z.enemyBudget,
                enemyMaxCostOfOneShip = z.enemyMaxCostOfOneShip,
                enemyDeviation        = z.enemyDeviation,
                enemyMaxShipsPerFleet = z.enemyMaxShipsPerFleet,
                enemyHealthMultiplier = z.enemyHealthMultiplier,
                enemyAttackMultiplier = z.enemyAttackMultiplier,
                enemyBeamEquipSlots        = z.enemyBeamEquipSlots,
                enemyMissileEquipSlots     = z.enemyMissileEquipSlots,
                enemyHangarEquipSlots      = z.enemyHangarEquipSlots,
                enemyShieldEquipSlots      = z.enemyShieldEquipSlots,
                enemyInterceptorEquipSlots = z.enemyInterceptorEquipSlots,
                explorationPointReward = z.explorationPointReward,
                commanderExpReward     = z.commanderExpReward,
                cellOverrides         = z.cellOverrides.ConvertAll(o => (object)new { row = o.row, col = o.col, type = o.type.ToString(), eventType = o.eventType.ToString() }),
            });
        }
        return JsonConvert.SerializeObject(new { zoneConfigs = zoneConfigs }, Formatting.Indented);
    }
}
