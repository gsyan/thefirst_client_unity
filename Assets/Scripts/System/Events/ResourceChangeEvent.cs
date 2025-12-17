using System;
using UnityEngine;

public static class EventManager
{
    public static event Action<int> OnTechLevelChanged;
    public static event Action<long> OnMineralChanged;
    public static event Action<long> OnMineralRareChanged;
    public static event Action<long> OnMineralExoticChanged;
    public static event Action<long> OnMineralDarkChanged;

    public static event Action OnFleetChanged;
    public static event Action OnShipChanged;

    // TechLevel
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

    // Ship
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

    
}