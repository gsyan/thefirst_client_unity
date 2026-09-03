// 함대에 배치된 함선 1개 — 리스트 기반 관리(공간형 3x3 배치 UI 기각, Docs/Exploration_Revamp.md §1-3)
// hullSubType은 "함체(모듈 서브타입 이름 문자열)"만 가리키고, 실제 장착된 모듈(빔/미사일/격납고 on-off 상태)은 modules에 별도로 들고 있음 —
// 서버 Ship/Module 엔티티와 1:1 동기화(배치 시 기본 로드아웃으로 시딩, 토글 응답으로 갱신)
[System.Serializable]
public struct FleetSlotEntry
{
    public string hullSubType;
    public bool isFront;
    public ModuleHullInfo modules;

    public FleetSlotEntry(string hullSubType, bool isFront, ModuleHullInfo modules)
    {
        this.hullSubType = hullSubType;
        this.isFront = isFront;
        this.modules = modules;
    }
}
