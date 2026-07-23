// 성능포인트 배분(ShipStatAllocation)을 실제 전투에 쓰이는 최종 수치로 변환한 결과
// 카테고리별 슬롯마다 독립적으로 강화되므로 슬롯 단위 배열로 보관 (설치 안 된 슬롯은 배열에서 제외됨)
[System.Serializable]
public struct ShipFinalStats
{
    public float health;
    public float turnRate;
    public float repair;

    public float[] beamAttacks;         // 장착된 빔 슬롯 개수만큼, 슬롯별 공격력
    public float[] beamAttackCools;     // 슬롯별 연사력(쿨다운, 낮을수록 빠름)
    public float[] beamProjectileSpeeds;// 슬롯별 발사체 속도
    public string[] beamModuleSubType;  // beamAttacks와 인덱스 대응(compact) — 장착 서브타입
    public float[] missileAttacks;      // 장착된 미사일 슬롯 개수만큼, 슬롯별 공격력
    public float[] missileAttackCools;
    public float[] missileProjectileSpeeds;
    public float[] missileSilenceTimes; // 적중 시 대상 무장 침묵 시간(초) — 미사일 전용
    public string[] missileModuleSubType;

    public float[] hangarShipAttacks;    // 장착된 격납고 슬롯 개수만큼
    public float[] hangarFighterAttacks;
    public float[] hangarAmmos;
    public float[] hangarHealths;
    public string[] hangarModuleSubType;

    public bool shieldInstalled;
    public float shieldGauge;
    public float shieldDelay;
    public float shieldRegenRate;

    public float[] interceptorDelays;     // 장착된 요격체 슬롯 개수만큼
    public float[] interceptorRegenRates;
}
