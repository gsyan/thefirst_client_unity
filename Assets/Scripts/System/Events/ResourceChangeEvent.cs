using System;
using UnityEngine;

public static class EventManager
{
    public static event Action<long> OnMoneyChanged;
    public static event Action<long> OnMineralChanged;
    public static event Action<int> OnTechLevelChanged;
    public static event Action OnFleetChanged;
    public static event Action OnShipChanged;

    // Money
    public static void TriggerMoneyChange(long money)
    {
        OnMoneyChanged?.Invoke(money);
    }
    public static void Subscribe_MoneyChanged(Action<long> callback)
    {
        OnMoneyChanged += callback;
    }
    public static void Unsubscribe_MoneyChanged(Action<long> callback)
    {
        OnMoneyChanged -= callback;
    }

    // mineral
    public static void TriggerMineralChange(long mineral)
    {
        OnMineralChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralChanged(Action<long> callback)
    {
        OnMineralChanged += callback;
    }
    public static void Unsubscribe_MineralChanged(Action<long> callback)
    {
        OnMineralChanged -= callback;
    }

    // TechLevel
    public static void TriggerTechLevelChange(int techLevel)
    {
        OnTechLevelChanged?.Invoke(techLevel);
    }
    public static void Subscribe_TechLevelChanged(Action<long> callback)
    {
        OnMineralChanged += callback;
    }
    public static void Unsubscribe_TechLevelChanged(Action<long> callback)
    {
        OnMineralChanged -= callback;
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