public enum ECostType
{
    PvpPoint
}

[System.Serializable]
public class CostStruct
{
    public ECostType costType;
    public long amount;

    public CostStruct(ECostType costType, long amount)
    {
        this.costType = costType;
        this.amount = amount;
    }
}
