using System;
using UnityEngine;

public static class EventManager
{
    // TechLevel
    public static event Action<int> OnTechLevelChanged;
    public static void TriggerTechLevelChange(int techLevel)
    {
        OnTechLevelChanged?.Invoke(techLevel);
    }
    public static void Subscribe_TechLevelChanged(Action<int> callback)
    {
        OnTechLevelChanged += callback;
    }
    public static void Unsubscribe_TechLevelChanged(Action<int> callback)
    {
        OnTechLevelChanged -= callback;
    }

    // mineral
    public static event Action<long> OnMineralChanged;
    public static void TriggerMineralChange(long money)
    {
        OnMineralChanged?.Invoke(money);
    }
    public static void Subscribe_MineralChanged(Action<long> callback)
    {
        OnMineralChanged += callback;
    }
    public static void Unsubscribe_MineralChanged(Action<long> callback)
    {
        OnMineralChanged -= callback;
    }

    // mineral rare
    public static event Action<long> OnMineralRareChanged;
    public static void TriggerMineralRareChange(long mineral)
    {
        OnMineralRareChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralRareChanged(Action<long> callback)
    {
        OnMineralRareChanged += callback;
    }
    public static void Unsubscribe_MineralRareChanged(Action<long> callback)
    {
        OnMineralRareChanged -= callback;
    }
    
    // mineral Exotic
    public static event Action<long> OnMineralExoticChanged;
    public static void TriggerMineralExoticChange(long mineral)
    {
        OnMineralExoticChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralExoticChanged(Action<long> callback)
    {
        OnMineralExoticChanged += callback;
    }
    public static void Unsubscribe_MineralExoticChanged(Action<long> callback)
    {
        OnMineralExoticChanged -= callback;
    }

    // mineral Dark
    public static event Action<long> OnMineralDarkChanged;
    public static void TriggerMineralDarkChange(long mineral)
    {
        OnMineralDarkChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralDarkChanged(Action<long> callback)
    {
        OnMineralDarkChanged += callback;
    }
    public static void Unsubscribe_MineralDarkChanged(Action<long> callback)
    {
        OnMineralDarkChanged -= callback;
    }

    // Fleet
    public static event Action OnFleetChanged;
    public static void TriggerFleetChange()
    {
        OnFleetChanged?.Invoke();
    }
    public static void Subscribe_FleetChange(Action callback)
    {
        OnFleetChanged += callback;
    }
    public static void Unsubscribe_FleetChange(Action callback)
    {
        OnFleetChanged -= callback;
    }

    // SpaceShip Stat Changed
    public static event Action OnShipChanged;
    public static void TriggerShipChange()
    {
        OnShipChanged?.Invoke();
    }
    public static void Subscribe_ShipChange(Action callback)
    {
        OnShipChanged += callback;
    }
    public static void Unsubscribe_ShipChange(Action callback)
    {
        OnShipChanged -= callback;
    }

    // SpaceShip Selection
    public static event Action<SpaceShip> OnSpaceShipSelected_TabUpgrade;
    public static void TriggerSpaceShipSelected_TabUpgrade(SpaceShip ship)
    {
        OnSpaceShipSelected_TabUpgrade?.Invoke(ship);
    }
    public static void Subscribe_SpaceShipSelected_TabUpgrade(Action<SpaceShip> callback)
    {
        OnSpaceShipSelected_TabUpgrade += callback;
    }
    public static void Unsubscribe_SpaceShipSelected_TabUpgrade(Action<SpaceShip> callback)
    {
        OnSpaceShipSelected_TabUpgrade -= callback;
    }

    // SpaceShip Module Selection
    public static event Action<SpaceShip, ModuleBase> OnSpaceShipModuleSelected;
    public static void TriggerSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        OnSpaceShipModuleSelected?.Invoke(ship, module);
    }
    public static void Subscribe_SpaceShipModuleSelected(Action<SpaceShip, ModuleBase> callback)
    {
        OnSpaceShipModuleSelected += callback;
    }
    public static void Unsubscribe_SpaceShipModuleSelected(Action<SpaceShip, ModuleBase> callback)
    {
        OnSpaceShipModuleSelected -= callback;
    }


}