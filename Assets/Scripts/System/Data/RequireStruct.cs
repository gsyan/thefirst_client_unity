[System.Serializable]
public class RequireStruct
{
    public int techLevel; // 0이면 요구 없음

    public RequireStruct() { techLevel = 0; }
    public RequireStruct(int techLevel) { this.techLevel = techLevel; }
}
