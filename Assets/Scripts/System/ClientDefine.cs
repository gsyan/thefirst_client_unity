// ------------------------------------------------------------
public class ClientDefine
{


}

public enum EPoolName
{
    PROJECTILE_BEAM,
    PROJECTILE_BEAM_HITSCAN,
    PROJECTILE_MISSILE_SMALL,
    PROJECTILE_MISSILE_MEDIUM,
    PROJECTILE_MISSILE_LARGE,
    EFFECT_BEAM_MUZZLE,
    EFFECT_BEAM_HEAD,
    EFFECT_BEAM_HIT,
    EFFECT_EXPLOSION_SHIP,
    EFFECT_EXPLOSION_MISSILE_SMALL,
    EFFECT_WARP_SPEEDLINES,
    EFFECT_FIRE_ON_SHIP,
    EFFECT_SCORCH_MARK,
    AIRCRAFT_STANDARD
}

public enum EUnitState
{
    Idle,
    Move,
    Warp,
    BattleReady,        // 워프 완료 대기 — 타겟팅 시작, 발사 대기
    BattleExploration,
    BattlePvp,
}

public static class EUnitStateExtensions
{
    public static bool IsBattleState(this EUnitState state)
    {
        return state == EUnitState.BattleReady
            || state == EUnitState.BattleExploration
            || state == EUnitState.BattlePvp;
    }
}
