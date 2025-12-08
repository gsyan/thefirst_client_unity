//------------------------------------------------------------------------------
using NUnit.Framework;
using System;
using UnityEngine;
using System.Collections.Generic;

public class AircraftStandard : AircraftBase
{

    public override void Start()
    {
        base.Start();
        
    }


    protected override void ReturnToPool()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }
        
        m_state = EAircraftState.None;
        
        ObjectManager.Instance.m_poolManager.Return(EPoolName.AIRCRAFT_STANDARD, this);    
    }
    
}
