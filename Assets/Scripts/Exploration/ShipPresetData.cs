// 함선 프리셋 — 식별 정보 + 성능포인트 배분(총량 프리셋마다 가변), 전 필드 CSV로 관리
[System.Serializable]
public class ShipPresetData
{
    public string presetId;
    public int unlockCommanderLevel = 1; // 이 프리셋을 사용할 수 있는 최소 커맨더 레벨 (예: 10 = 10레벨부터 사용 가능)
    public string displayNameKey; // Assets/Resources/Localization/csv/UI.csv 참조 키
    public string prefabName;
    public int commandCost;

    public ShipStatAllocation statAllocation = new ShipStatAllocation();
}
