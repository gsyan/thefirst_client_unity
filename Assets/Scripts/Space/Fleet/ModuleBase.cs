//------------------------------------------------------------------------------
using NUnit.Framework;
using System;
using UnityEngine;
using System.Collections.Generic;

public class ModuleBase : MonoBehaviour
{
    [HideInInspector] public int m_classId;
    [HideInInspector] public ModuleSlot m_moduleSlot;

    [HideInInspector] public float m_health;
    [HideInInspector] public float m_healthMax;
    [HideInInspector] public float m_attack;

    [HideInInspector] public long m_mineralCostLevelup;

    // 이 슬롯에서 subTypeAddCost 납부 이력 (비용 없이 교체 가능한 서브타입 목록)
    [HideInInspector] public List<EModuleSubType> m_unlockedSubTypes = new List<EModuleSubType>();

    // 리셋 시 환급할 투자 이력
    [HideInInspector] public int m_investedMineral;
    [HideInInspector] public int m_investedPvpMineral;
    [HideInInspector] public int m_investedTempMineral;

    public void SetUnlockedSubTypes(List<EModuleSubType> list)
    {
        m_unlockedSubTypes = list ?? new List<EModuleSubType>();
    }

    public void SetInvestedMinerals(int mineral, int pvpMineral, int tempMineral)
    {
        m_investedMineral    = mineral;
        m_investedPvpMineral = pvpMineral;
        m_investedTempMineral = tempMineral;
    }

    public bool HasInvestedMineral()
    {
        return m_investedMineral > 0 || m_investedPvpMineral > 0 || m_investedTempMineral > 0;
    }

    public bool IsSubTypeFree(EModuleSubType subType)
    {
        return m_unlockedSubTypes != null && m_unlockedSubTypes.Contains(subType);
    }
    
    // 함대 정보
    protected SpaceFleet m_myFleet;
    protected SpaceShip m_myShip;

    protected EModuleState m_moduleState;

    public virtual void Start()
    {

    }

    public virtual void ApplyShipStateToModule()
    {
        switch (m_myShip.m_shipState)
        {
            case EShipState.None:
            case EShipState.Move:
                m_moduleState = EModuleState.None;
                break;
            case EShipState.Battle:
                m_moduleState = EModuleState.Battle;
                break;
            default:
                m_moduleState = EModuleState.None;
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
        target.TakeDamage(m_attack);
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
        m_myFleet = fleet;
        m_myShip = ship;
    }

    // 함대 정보 자동 탐지 및 설정
    protected virtual void AutoDetectFleetInfo()
    {
        if (m_myShip == null)
            m_myShip = GetComponentInParent<SpaceShip>();

        if (m_myFleet == null && m_myShip != null)
            m_myFleet = m_myShip.m_myFleet;
    }

    // 함대 이름 반환 (로그용)
    public string GetFleetName()
    {
        if (m_myFleet != null)
            return m_myFleet.m_fleetInfo.fleetName;
        return "Unknown Fleet";
    }

    // 함선 이름 반환 (로그용)
    public string GetShipName()
    {
        if (m_myShip != null)
            return m_myShip.gameObject.name;
        return "Unknown Ship";
    }

    public SpaceShip GetMyShip()
    {
        return m_myShip;
    }

    // 팝업용 아이콘+수치 문자열 반환 (fromLevel == toLevel이면 단순 현재 수치 표시, 다르면 "현재 → 다음" 비교 표시 용도)
    public virtual string GetDetailText(int fromLevel, int toLevel)
    {
        return CommonUtility.GetModuleDetailText(GetModuleType(), GetModuleSubType(), fromLevel, toLevel);
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
