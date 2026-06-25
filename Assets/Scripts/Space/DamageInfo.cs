public enum EDamageType
{
    None    = 0,
    Beam    = 1,
    Missile = 2,
    Aircraft = 3,
}

// 데미지 파이프라인 - 기본 공격력과 누적 배율을 분리해 최종 데미지를 계산
public struct DamageInfo
{
    public float        baseDamage;       // 모듈 원본 공격력 (변하지 않음)
    public float        attackMultiplier; // 공격 측 누적 배율 (진형·함선수·전술 등)
    public EDamageType  damageType;       // 공격 타입

    public float GetFinalDamage()
    {
        float finalDamage = baseDamage * attackMultiplier;
        return finalDamage;
    }
}
