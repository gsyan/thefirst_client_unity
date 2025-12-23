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
    [HideInInspector] public float m_attackPower;

    [HideInInspector] public CostStruct m_upgradeCost;
    
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
        target.TakeDamage(m_attackPower);
    }

    public virtual EModuleType GetModuleType()
    {
        return EModuleType.None;
    }
    public virtual int GetModuleTypePacked()
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
    public virtual int GetModuleBodyIndex()
    {
        return 0;
    }
    public virtual void SetModuleBodyIndex(int bodyIndex)
    {
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
            m_myFleet = m_myShip.GetComponentInParent<SpaceFleet>();
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

    public virtual string GetUpgradeComparisonText()
    {
        return "";
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
