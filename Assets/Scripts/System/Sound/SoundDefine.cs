// ------------------------------------------------------------
// 사운드 리소스 경로: Resources/Sound/BGM/, Resources/Sound/FX/
// 파일명은 enum 이름 소문자 기준 (예: EBgm.Main -> "bgm_main")

public enum EBgm
{
    None = 0,
    Main,
    Space,
    Battle,
    Defeat,
}

public enum EMissileSource { Ship, Aircraft }

public enum EFx
{
    None = 0,
    Main,
        
    // UI
    Grade_Up,
    Grade_Down,
    Level_Up,
    Level_Down,
    Add_Ship,
    Button_Clicked,

    // Stage
    Tech_Level_Up,
    Stage_Clear_First,
    Stage_Clear,
    
    // game
    Explosion_Ship,
    Beam_Impact1,
    Explosion_Missile,
    Explosion_Aircraft_Missile,
    Beam_Fire1,
    Missile_Fire1,
    Aircraft_Launch,
    
    // Not Important
    Fleet_Recovery,
    Ship_Warp,
}
