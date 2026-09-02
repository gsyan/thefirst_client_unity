// 게임 전역 설정 ScriptableObject — 함선 추가 비용(addShipCost), PvP 설정, 모듈 해금 비용 관리
// 커맨더 레벨별 최대 함선 수(ship_count)는 DataTableCommander.GetShipCount()에서 조회
using UnityEngine;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class GameSettings
{
    [Header("Game Settings")]
    public string version = "0.0.1";

    [Tooltip("함선 추가 시 필요한 ModulePoint 비용")]
    public int addShipCost = 10;

    [Tooltip("신규 지휘관 생성 시 초기 지휘력 최대치")]
    public int commandPowerMaxInit = 120;

[Header("Pvp Settings")]
    public int pvpMinCommanderLevel = 2;
    public int pvpListCount = 3;
    public int pvpListRefreshCount = 5;
    public int pvpRankScoreInit = 1000;
    public int pvpRankScorePenalty = 1;
    
    public int moduleUnlockPrice = 1;

    [Header("Tactic - Repair")]
    [Tooltip("수리 부스트 ON 시 1초당 소모하는 탐험 포인트 (함대 단위)")]
    public int repairBoostExplorationPointPerSec = 1;
    [Tooltip("수리 부스트 ON 시 수리 속도 배율")]
    public float repairBoostMultiplier = 2f;
    [Tooltip("즉시 수리 비용 기준 시간(초) — 비용 = repairBoostExplorationPointPerSec × instantRepairBaseSecs")]
    public int instantRepairBaseSecs = 60;

    [Header("Tactic - Missile")]
    [Tooltip("미사일 전술 강화 ON 시 개방된 슬롯 1개당 1초당 소모하는 탐험 포인트")]
    public int missileTacticExplorationPointPerSec = 1;
    [Tooltip("미사일 전술 강화 ON 시 데미지 배율")]
    public float missileTacticDamageMultiplier = 2f;
    [Tooltip("미사일 전술 강화 ON 시 폭발 반경 배율")]
    public float missileTacticExplosionMultiplier = 2f;

    [Header("Tactic - Aircraft")]
    [Tooltip("함재기 전술 강화 ON 시 개방된 슬롯 1개당 1초당 소모하는 탐험 포인트")]
    public int aircraftTacticExplorationPointPerSec = 1;
    [Tooltip("함재기 전술 강화 ON 시 공격력 배율")]
    public float aircraftTacticDamageMultiplier = 2f;
    [Tooltip("함재기 전술 강화 ON 시 미사일 장착 개수 배율")]
    public float aircraftTacticAmmoMultiplier = 2f;

    [Header("Tactic - Shield")]
    [Tooltip("실드 ON 시 1초당 소모하는 탐험 포인트 — 게이지가 남아 방어가 실제로 발동 중일 때만 소모(풀게이지 대기 상태는 미소모)")]
    public int shieldTacticExplorationPointPerSec = 1;

    [Header("Exploration - Ship Stat Formula")]
    public ShipStatFormulaSettings shipStatFormula = new ShipStatFormulaSettings();
}

// 성능포인트 1000 배분 → 최종 전투 수치 변환 공식의 기준값/계수
// Docs/Exploration_Revamp.md §1-1(장착+강화), §1-4(실드/요격체) 확정 공식 참고
// 카테고리별 중첩 클래스 — Inspector에서 각각 폴드아웃으로 접고 펼 수 있음
[System.Serializable]
public class ShipStatFormulaSettings
{
    [Tooltip("카테고리(빔/미사일/함재기/요격체)별 슬롯 상한 — 함체 데이터의 슬롯 배열 크기 기준값")]
    public int maxModuleSlots = 6;

    [Tooltip("공격모듈(빔/미사일/격납고) 슬롯 1개당 투자 가능한 강화 포인트 상한")]
    public int maxAttackReinforcePointsPerSlot = 10;

    public BeamFormula beam = new BeamFormula();
    public MissileFormula missile = new MissileFormula();
    public HangarFormula hangar = new HangarFormula();
    public ShieldFormula shield = new ShieldFormula();
    public InterceptorFormula interceptor = new InterceptorFormula();
    public FlatStatFormula flatStats = new FlatStatFormula();
}

[System.Serializable]
public class BeamFormula
{
    [Tooltip("공격력 강화 1포인트당 가산")]
    public float attackPerPoint = 0.1f;
    [Tooltip("연사력 강화 1포인트당 쿨다운 감소량")]
    public float attackCoolReductionPerPoint = 0.02f;
    public float attackCoolFloor = 0.5f;
    [Tooltip("발사체 속도 강화 1포인트당 가산")]
    public float projectileSpeedPerPoint = 1f;
}

[System.Serializable]
public class MissileFormula
{
    [Tooltip("공격력 강화 1포인트당 가산")]
    public float attackPerPoint = 0.1f;
    [Tooltip("연사력 강화 1포인트당 쿨다운 감소량")]
    public float attackCoolReductionPerPoint = 0.02f;
    public float attackCoolFloor = 0.5f;
    [Tooltip("발사체 속도 강화 1포인트당 가산")]
    public float projectileSpeedPerPoint = 1f;
    [Tooltip("침묵 강화 1포인트당 침묵 시간 가산(초)")]
    public float silenceTimePerPoint = 0.1f;
}

[System.Serializable]
public class HangarFormula
{
    public float baseShipAttack = 10f;
    public float baseFighterAttack = 10f;
    public float baseAmmo = 10f;
    public float baseHealth = 50f;
    [Tooltip("강화 서브스탯(4종) 1포인트당 가산")]
    public float reinforcePerPoint = 0.1f;
}

[System.Serializable]
public class ShieldFormula
{
    public float gaugePerPoint = 0.5f;
    public float regenRatePerPoint = 0.1f;
}

[System.Serializable]
public class InterceptorFormula
{
    [Tooltip("딜레이/회복속도 계수는 실드와 동일하게 임시 적용 — 실측 후 별도 조정 필요 (미확정)")]
    public float delayReductionPerPoint = 0.02f;
    public float regenRatePerPoint = 0.1f;
    public float delayFloor = 1f;
}

[System.Serializable]
public class FlatStatFormula
{
    [Tooltip("체력/선회력/수리능력 — 장착 개념 없는 순수 포인트 배분. 기본값/계수 미확정 — 임시값")]
    public float perPoint = 0.1f;
}

[CreateAssetMenu(fileName = "DataTableConfig", menuName = "Custom/DataTableConfig")]
public class DataTableConfig : ScriptableObject
{
    public GameSettings gameSettings = new GameSettings();

    [HideInInspector]
    [SerializeField] private string exportedJson = "";

    public bool IsValid()
    {
        return gameSettings != null;
    }

    public string GetExportFileName()
    {
        return "DataTableConfig";
    }

    public string GetDefaultServerPath()
    {
        return System.IO.Path.Combine(Application.dataPath, "..", "..", "server", "src", "main", "resources", "data", GetExportFileName() + ".json");
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        string json = JsonConvert.SerializeObject(gameSettings, Formatting.Indented);
        exportedJson = json;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        return json;
    }

    public void ImportFromJson(string json)
    {
        try
        {
            var importData = JsonConvert.DeserializeObject<GameSettings>(json);
            if (importData != null)
            {
                gameSettings = importData;

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to import GameSettings JSON: {e.Message}");
        }
    }

    #endregion
}