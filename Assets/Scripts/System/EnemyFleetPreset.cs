using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShipCountConditionForEnemyFleetSpawn
{
    [Header("Player Ship Count Range")]
    public int minShips;
    public int maxShips;

    [Header("Enemy Ship Count")]
    public int enemyShipCount;
}

[System.Serializable]
public class LevelConditionForEnemyFleetSpawn
{
    [Header("Player Average Level Range")]
    public int minAverageLevel;
    public int maxAverageLevel;

    [Header("Enemy Average Level")]
    public int enemyAverageLevel;
}

[CreateAssetMenu(fileName = "EnemyFleetPresets", menuName = "Custom/EnemyFleetPresets")]
public class EnemyFleetPreset : ScriptableObject
{
    public List<ShipCountConditionForEnemyFleetSpawn> shipCountPresets = new List<ShipCountConditionForEnemyFleetSpawn>();
    public List<LevelConditionForEnemyFleetSpawn> levelPresets = new List<LevelConditionForEnemyFleetSpawn>();

    public (int enemyShipCount, int enemyLevel) GetMatchingPreset(int shipCount, int averageLevel)
    {
        int enemyShipCount = GetMatchingShipCount(shipCount);
        int enemyLevel = GetMatchingLevel(averageLevel);
        return (enemyShipCount, enemyLevel);
    }

    private int GetMatchingShipCount(int shipCount)
    {
        foreach (var preset in shipCountPresets)
        {
            if (shipCount >= preset.minShips && shipCount <= preset.maxShips)
                return preset.enemyShipCount;
        }

        if (shipCountPresets.Count > 0)
            return shipCountPresets[0].enemyShipCount;

        return 1;
    }

    private int GetMatchingLevel(int averageLevel)
    {
        foreach (var preset in levelPresets)
        {
            if (averageLevel >= preset.minAverageLevel && averageLevel <= preset.maxAverageLevel)
                return preset.enemyAverageLevel;
        }

        if (levelPresets.Count > 0)
            return levelPresets[0].enemyAverageLevel;

        return 1;
    }
}
