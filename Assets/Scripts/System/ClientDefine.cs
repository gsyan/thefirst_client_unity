// ------------------------------------------------------------
public class ClientDefine
{


}

public enum EPoolName
{
    PROJECTILE_BEAM,
    PROJECTILE_BEAM_INSTANT,
    PROJECTILE_MISSILE_SMALL,
    PROJECTILE_MISSILE_MEDIUM,
    PROJECTILE_MISSILE_LARGE,
    EFFECT_BEAM_MUZZLE,
    EFFECT_BEAM_HEAD,
    EFFECT_BEAM_HIT,
    EFFECT_MISSILE_HIT,
    //EFFECT_SHIP_EXPLOSION,
    EFFECT_WARP_SPEEDLINES,
    EFFECT_EXPLOSION_SHIP,
    AIRCRAFT_STANDARD
}

public enum EFleetState
{
    None,
    Move,
    Battle,
    Max
}

public enum EShipState
{
    None,
    Move,
    Battle,
    Max
}

public enum EModuleState
{
    None,
    Battle,
    Max
}
