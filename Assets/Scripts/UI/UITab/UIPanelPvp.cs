// PvP 패널 — 구조(진입 버튼 + UIManager 등록)만 다른 패널들과 동일하게 맞춰둠, 내부 로직은 아직 미구현
// 실제 PvP 로직(상대 목록/전투 시작/랭킹)은 Assets/Scripts/UI/UITab/UITabPvp.cs에 #if false로 보존되어 있음 —
// 함선 시스템 대격변으로 구식 ShipInfo/FleetInfo 기반이라 그대로 못 씀, 마이그레이션 시 이 클래스 안에 옮겨 구현할 것
public class UIPanelPvp : UIPanelBase
{
    // TODO: UITabPvp.cs의 ShipInfo/FleetInfo 기반 로직을 새 함선 시스템에 맞춰 이식
}
