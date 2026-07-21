// 함대에 배치된 함선 1개 — 리스트 기반 관리(공간형 3x3 배치 UI 기각, Docs/Exploration_Revamp.md §1-3)
[System.Serializable]
public struct FleetSlotEntry
{
    public string shipPresetId;
    public bool isFront;

    public FleetSlotEntry(string shipPresetId, bool isFront)
    {
        this.shipPresetId = shipPresetId;
        this.isFront = isFront;
    }
}
