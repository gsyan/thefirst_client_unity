// 함선 프리셋의 성능포인트 배분 입력값(총량은 프리셋마다 가변) — 디자이너가 프리셋 제작 시 채우는 값
// 장착 코스트/기본값 출처: DataTableConfig.gameSettings.shipStatFormula (ShipStatFormulaSettings)
// 카테고리별 슬롯 상한(maxModuleSlots)은 DataTableConfig에서 관리 — 이 클래스는 배열 길이 그대로 사용
// 슬롯 장착 여부는 별도 bool 배열로 관리(CSV 빈 칸 = 미장착)
// Docs/Exploration_Revamp.md §1-1(장착+강화), §1-4(실드/요격체) 참고
[System.Serializable]
public class ShipStatAllocation
{
    // Flat Stats — 장착 개념 없이 순수 포인트 배분. 기본값/계수 미확정 — 임시 1p=+0.1
    public int healthPoints;
    public int turnRatePoints;
    public int repairPoints;

    // Beam — 슬롯당 장착 서브타입(EModuleSubType 이름, 예: beam_t1_m1) + 강화 포인트. 빈 문자열 = 미장착. 장착 코스트는 shipStatFormula.beam.installCost
    public string[] beamModuleSubType = new string[0];
    public int[] beamReinforcePoints = new int[0];

    // Missile — 빔과 동일 구조
    public string[] missileModuleSubType = new string[0];
    public int[] missileReinforcePoints = new int[0];

    // Hangar — 슬롯당 장착 서브타입 + 4개 서브스탯
    public string[] hangarModuleSubType = new string[0];
    public int[] hangarShipAttackPoints = new int[0];
    public int[] hangarFighterAttackPoints = new int[0];
    public int[] hangarAmmoPoints = new int[0];
    public int[] hangarHealthPoints = new int[0];

    // Interceptor — 슬롯당 장착 여부 + 2개 서브스탯
    public bool[] interceptorSlotInstalled = new bool[0];
    public int[] interceptorDelayPoints = new int[0];
    public int[] interceptorRegenRatePoints = new int[0];

    // Shield — 장착 여부(0/1), 코스트는 shipStatFormula.shield.installCost. 강화 서브스탯 3종은 1p=1선택
    public bool shieldInstalled;
    public int shieldGaugePoints;
    public int shieldDelayPoints;
    public int shieldRegenRatePoints; // 회복속도(초당 게이지 회복량)

    public int GetTotalPointsUsed(ShipStatFormulaSettings formula)
    {
        int total = healthPoints + turnRatePoints + repairPoints;

        for (int i = 0; i < beamModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(beamModuleSubType[i]) == false)
                total += formula.beam.installCost + beamReinforcePoints[i];
        }

        for (int i = 0; i < missileModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(missileModuleSubType[i]) == false)
                total += formula.missile.installCost + missileReinforcePoints[i];
        }

        for (int i = 0; i < hangarModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(hangarModuleSubType[i]) == false)
                total += formula.hangar.installCost + hangarShipAttackPoints[i] + hangarFighterAttackPoints[i] + hangarAmmoPoints[i] + hangarHealthPoints[i];
        }

        for (int i = 0; i < interceptorSlotInstalled.Length; i++)
        {
            if (interceptorSlotInstalled[i])
                total += formula.interceptor.installCost + interceptorDelayPoints[i] + interceptorRegenRatePoints[i];
        }

        total += shieldInstalled ? formula.shield.installCost + shieldGaugePoints + shieldDelayPoints + shieldRegenRatePoints : 0;

        return total;
    }
}
