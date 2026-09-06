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
    PROJECTILE_INTERCEPTOR,
    EFFECT_BEAM_MUZZLE,
    EFFECT_BEAM_HEAD,
    EFFECT_BEAM_HIT,
    EFFECT_SHIELD_HIT,
    EFFECT_EXPLOSION_SHIP,
    EFFECT_EXPLOSION_MISSILE_SMALL,
    EFFECT_WARP_SPEEDLINES,
    EFFECT_FIRE_ON_SHIP,
    EFFECT_SCORCH_MARK,
    AIRCRAFT_STANDARD,
    DEBRIS_ROCK,
    DEBRIS_JUNK
}

public enum EUnitState
{
    Idle,
    Move,
    Warp,
    BattleExploration,
    BattlePvp,
}

public static class EUnitStateExtensions
{
    public static bool IsBattleState(this EUnitState state)
    {
        return state == EUnitState.BattleExploration
            || state == EUnitState.BattlePvp;
    }
}
