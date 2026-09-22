// 게임 전역 설정 ScriptableObject — PvP 설정, 전술 강화 탐험 포인트 소모량 관리
// 커맨더 레벨별 최대 함선 수(ship_count)는 DataTableCommander.GetShipCount()에서 조회
using UnityEngine;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class GameSettings
{
    public GeneralSettings general = new GeneralSettings();
    public PvpSettings pvp = new PvpSettings();
    public TacticSettings tactic = new TacticSettings();
    public ExplorationSettings exploration = new ExplorationSettings();
    public ShipStatFormulaSettings shipStatFormula = new ShipStatFormulaSettings();
}

[System.Serializable]
public class GeneralSettings
{
    [Tooltip("신규 지휘관 생성 시 초기 지휘력 최대치")]
    public int commandPowerMaxInit = 400;

    [Tooltip("발사 타이밍 지터 상한(초) — 쿨다운 완료(발사 신호) 후 실제 발사까지의 랜덤 지연 최대값. ModuleBase.ArmAttackSignal()에서 사용")]
    public float attackJitterMax = 1f;

    [Tooltip("강화 포인트 1점당 소모하는 지휘력(CP)")]
    public int reinforceCpCostPerPoint = 10;
}

[System.Serializable]
public class PvpSettings
{
    public int pvpMinCommanderLevel = 2;
    public int pvpListCount = 3;
    public int pvpListRefreshCount = 5;
    public int pvpRankScoreInit = 1000;
    public int pvpRankScorePenalty = 1;
}

[System.Serializable]
public class ExplorationSettings
{
    [Tooltip("보상카드 다시 뽑기(광고 시청 리롤) 1일 최대 횟수 — UTC 자정 기준 리셋")]
    public int rewardCardRerollLimit = 10;
}

[System.Serializable]
public class TacticSettings
{
    [Tooltip("수리 전술 ON 시 회복 틱마다, 체력이 100%가 아닌 함선 1척당 소모하는 탐험 포인트")]
    public int tacticRepairCost = 10;
    [Tooltip("미사일 전술 ON 시 미사일 발사 1건마다 소모하는 탐험 포인트")]
    public int tacticMissileCost = 1;
    [Tooltip("함재기 전술 ON 시 함재기 발진 1건마다 소모하는 탐험 포인트")]
    public int tacticHangerCost = 1;
    [Tooltip("실드 전술 ON 시 실드 회복 틱마다, 게이지가 가득 차지 않은 함선 1척당 소모하는 탐험 포인트")]
    public int tacticShieldCost = 10;
    [Tooltip("요격체 전술 ON 시 요격체 1기 생성마다 소모하는 탐험 포인트")]
    public int tacticInterceptorCost = 10;
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
}

[System.Serializable]
public class BeamFormula
{
    [Tooltip("공격력 강화 1포인트당 증가량 — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float attackPerPoint;
    [Tooltip("연사력 강화를 슬롯 상한까지 채웠을 때의 쿨다운 감소 비율(0.3 = -30%) — 쿨다운 = max(하한, 기본값 × (1 - 비율 × 투자포인트 / 상한))")]
    public float maxCoolReductionRatio;
    [Tooltip("쿨다운 하한(초)")]
    public float attackCoolFloor = 0.5f;
}

[System.Serializable]
public class MissileFormula
{
    [Tooltip("공격력 강화 1포인트당 증가량 — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float attackPerPoint;
    [Tooltip("연사력 강화를 슬롯 상한까지 채웠을 때의 쿨다운 감소 비율(0.3 = -30%) — 쿨다운 = max(하한, 기본값 × (1 - 비율 × 투자포인트 / 상한))")]
    public float maxCoolReductionRatio;
    [Tooltip("쿨다운 하한(초)")]
    public float attackCoolFloor = 0.5f;
    [Tooltip("침묵시간 강화 1포인트당 증가량(초) — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float silenceTimePerPoint;
}

[System.Serializable]
public class HangarFormula
{
    [Tooltip("대함/대전투기 공격력 강화 1포인트당 증가량 — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float attackPerPoint;
    [Tooltip("탄약 강화 1포인트당 증가 수량(정수)")]
    public int ammoPerPoint;
    [Tooltip("체력 강화를 슬롯 상한까지 채웠을 때의 기본값 대비 증가 비율(0.5 = +50%) — 최종값 = 기본값 × (1 + 비율 × 투자포인트 / 상한)")]
    public float maxHealthBonusRatio;
    [Tooltip("교란(명중 시 공격 딜레이) 강화 1포인트당 증가량 — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float disruptPerPoint;
}

[System.Serializable]
public class ShieldFormula
{
    [Tooltip("게이지 강화 1포인트당 증가량 — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float gaugePerPoint;
    [Tooltip("회복속도 강화 1포인트당 증가량 — 최종값 = 기본값 + 투자포인트 × 이 값")]
    public float regenRatePerPoint;
}

[System.Serializable]
public class InterceptorFormula
{
    [Tooltip("회복시간 강화를 슬롯 상한까지 채웠을 때의 회복시간 감소 비율(0.5 = -50%) — 회복시간 = 기본값 × (1 - 비율 × 투자포인트 / 상한)")]
    public float maxRegenTimeReductionRatio;
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