// 함선 프리셋 — 식별 정보 + 성능포인트 배분(총량 프리셋마다 가변), 전 필드 CSV로 관리
[System.Serializable]
public class ShipPresetData
{
    public string presetId; // UI.csv 로컬라이즈 키로도 그대로 사용(별도 displayNameKey 없음)
    public int unlockCommanderLevel = 1; // 이 프리셋을 사용할 수 있는 최소 커맨더 레벨 (예: 10 = 10레벨부터 사용 가능)
    public string prefabName;
    public int commandCost;

    public ShipStatAllocation statAllocation = new ShipStatAllocation();
}
