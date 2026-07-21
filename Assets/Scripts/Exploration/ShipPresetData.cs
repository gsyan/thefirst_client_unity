// 함선 프리셋 — 식별 정보 + 성능포인트 배분(총량 프리셋마다 가변), 전 필드 CSV로 관리
[System.Serializable]
public class ShipPresetData
{
    public string presetId;
    public string displayNameKey; // Assets/Resources/Localization/csv/UI.csv 참조 키
    public string prefabName;
    public int commandCost;

    public ShipStatAllocation statAllocation = new ShipStatAllocation();
}
