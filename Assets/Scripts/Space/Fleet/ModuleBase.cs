//------------------------------------------------------------------------------
using System;
using UnityEngine;

public class ModuleBase : MonoBehaviour
{
    [HideInInspector] public int m_classId;
    [HideInInspector] public ModuleSlot m_moduleSlot;

    [HideInInspector] public float m_health;
    [HideInInspector] public float m_healthMax;
    [HideInInspector] public float m_attack;

    [HideInInspector] public long m_modulePointCostLevelup;

    // 리셋 시 환급할 투자 이력
    [HideInInspector] public int m_investedModulePoint;

    public void SetInvestedModulePoint(int modulePoint)
    {
        m_investedModulePoint = modulePoint;
    }

    public bool HasInvestedModulePoint()
    {
        return m_investedModulePoint > 0;
    }

    // 미네랄 투자 이력 (전투 승리 시 소멸)
    [HideInInspector] public int m_investedMineral;

    public void SetInvestedMineral(int mineral)
    {
        m_investedMineral = mineral;
    }

    // 이 모듈의 UI 모드 상태 (미네랄 모드 여부)
    [HideInInspector] public bool m_isMineralMode = false;

    // 함대 정보
    protected SpaceFleet m_ownerFleet;
    protected SpaceShip m_ownerShip;

    protected EUnitState m_moduleState;

    public virtual void Start()
    {

    }

    public virtual void ApplyShipStateToModule()
    {
        switch (m_ownerShip.m_shipState)
        {
            case EUnitState.Idle:
            case EUnitState.Move:
            case EUnitState.Warp:
                m_moduleState = EUnitState.Idle;
                break;
            case EUnitState.BattleExploration:
            case EUnitState.BattlePvp:
                m_moduleState = m_ownerShip.m_shipState;
                break;
            default:
                m_moduleState = EUnitState.Idle;
                break;
        }
    }

    public virtual void TakeDamage(float damage)
    {
        m_health -= damage;
        if (m_health < 0.0f) m_health = 0.0f;
    }

    public virtual void Attack(SpaceShip target)
    {
        float finalAttack = m_attack;
        if (m_ownerFleet != null)
            finalAttack *= m_ownerFleet.GetShipCountAttackMultiplier() * m_ownerFleet.GetFormationAttackMultiplier();
        target.TakeDamage(finalAttack, transform.position);
    }

    public virtual EModuleType GetModuleType()
    {
        return EModuleType.none;
    }
    public virtual EModuleSubType GetModuleSubType()
    {
        return EModuleSubType.none;
    }
    public virtual int GetModuleSlotIndex()
    {
        return 0;
    }
    public virtual int GetModuleLevel()
    {
        return 0;
    }
    public virtual void SetModuleLevel(int level)
    {
    }

    // 모듈 레벨업 시 스탯 갱신 (각 모듈에서 override) - 모듈 프리팹 갱신까지 하는 경우는 ReplaceModuleInSlot 을 사용
    public virtual void ApplyModuleLevelUp(int newLevel)
    {
        // 기본 구현: 레벨만 설정
        SetModuleLevel(newLevel);
    }
    // 교체 시 구 모듈의 공격 타이머를 신 모듈에 승계 — 무기 모듈에서 override
    public virtual float GetLastAttackTime() { return 0f; }
    public virtual void SetLastAttackTime(float t) { }
    public virtual int GetModuleBodyIndex()
    {
        return 0;
    }
    public virtual void SetModuleBodyIndex(int bodyIndex)
    {
    }

    public virtual int GetSlotIndex()
    {
        if (m_moduleSlot == null) return 0;
        return m_moduleSlot.m_moduleSlotInfo.slotIndex;
    }

    // 함대 정보 설정
    public virtual void SetFleetInfo(SpaceFleet fleet, SpaceShip ship)
    {
        m_ownerFleet = fleet;
        m_ownerShip = ship;
    }

    // 함대 정보 자동 탐지 및 설정
    protected virtual void AutoDetectFleetInfo()
    {
        if (m_ownerShip == null)
            m_ownerShip = GetComponentInParent<SpaceShip>();

        if (m_ownerFleet == null && m_ownerShip != null)
            m_ownerFleet = m_ownerShip.m_ownerFleet;
    }

    // 함대 이름 반환 (로그용)
    public string GetFleetName()
    {
        if (m_ownerFleet != null)
            return m_ownerFleet.m_fleetInfo.fleetName;
        return "Unknown Fleet";
    }

    // 함선 이름 반환 (로그용)
    public string GetShipName()
    {
        if (m_ownerShip != null)
            return m_ownerShip.gameObject.name;
        return "Unknown Ship";
    }

    public SpaceShip GetShip()
    {
        return m_ownerShip;
    }

    // 모듈의 능력치 프로파일 반환 (하위 클래스에서 override)
    public virtual CapabilityProfile GetModuleCapabilityProfile(bool bByInfo = true)
    {
        return new CapabilityProfile();
    }

    // 코루틴 재시작 (Body 교체 등으로 모듈이 재활성화될 때 호출)
    public virtual void RestartCoroutines()
    {
        // 기본 구현 없음 - 각 모듈에서 필요시 override
    }

    public SpaceShip GetSpaceShip()
    {
        // SpaceShip targetShip = GetComponent<SpaceShip>();
        // if (targetShip == null)
        //     targetShip = GetComponentInParent<SpaceShip>();
        // return targetShip;
        return GetComponentInParent<SpaceShip>();
    }

}
