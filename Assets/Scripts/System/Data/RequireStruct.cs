[System.Serializable]
public class RequireStruct
{
    public int commanderLevel; // 0이면 요구 없음

    public RequireStruct() { commanderLevel = 0; }
    public RequireStruct(int commanderLevel) { this.commanderLevel = commanderLevel; }
}
