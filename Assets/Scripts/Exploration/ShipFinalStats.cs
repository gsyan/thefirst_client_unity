// 성능포인트 배분(ShipStatAllocation)을 실제 전투에 쓰이는 최종 수치로 변환한 결과
// 카테고리별 슬롯마다 독립적으로 강화되므로 슬롯 단위 배열로 보관 — 배열 인덱스 = 슬롯 인덱스, 미장착 슬롯은 값 0 / 서브타입 ""
[System.Serializable]
public struct ShipFinalStats
{
    public float health;
    public float turnRate;
    public float repair;

    public float[] beamAttacks;         // 슬롯별 공격력
    public float[] beamAttackCools;     // 슬롯별 연사력(쿨다운, 낮을수록 빠름)
    public float[] beamProjectileSpeeds;// 슬롯별 발사체 속도
    public string[] beamModuleSubType;  // 슬롯별 장착 서브타입, 미장착은 ""
    public float[] missileAttacks;      // 슬롯별 공격력
    public float[] missileAttackCools;
    public float[] missileProjectileSpeeds;
    public float[] missileSilenceTimes; // 적중 시 대상 무장 침묵 시간(초) — 미사일 전용
    public string[] missileModuleSubType;

    public float[] hangarShipAttacks;
    public float[] hangarFighterAttacks;
    public float[] hangarAmmos;
    public float[] hangarHealths;
    public float[] hangarAirDisrupts;    // 함재기 명중 시 타겟 함선에 거는 공격 딜레이(교란) — 강화 대상 아닌 모듈 고정값
    public string[] hangarModuleSubType;

    public bool shieldInstalled;
    public float shieldGauge;
    public float shieldRegenRate;

    public float[] interceptorCounts;         // 요격체 최대 개수(티어별 고정, 강화 대상 아님)
    public float[] interceptorRegenTimes;     // 요격체 1기 생성에 걸리는 시간(초)
    public string[] interceptorModuleSubType; // 슬롯별 장착 서브타입, 미장착은 ""
}
