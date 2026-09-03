//------------------------------------------------------------------------------
using System;
using System.Collections;
using UnityEngine;

public class ModuleBase : MonoBehaviour
{
    [HideInInspector] public int m_classId;
    [HideInInspector] public ModuleSlot m_moduleSlot;

    [HideInInspector] public float m_health;
    [HideInInspector] public float m_healthMax;
    [HideInInspector] public float m_attack;

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

    public virtual EModuleType GetModuleType()
    {
        return EModuleType.none;
    }
    public virtual string GetModuleSubType()
    {
        return "";
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

    // 교체 시 구 모듈의 공격 타이머를 신 모듈에 승계 — 무기 모듈에서 override
    public virtual float GetLastAttackTime() { return 0f; }
    public virtual void SetLastAttackTime(float t) { }
    public virtual int GetModuleHullIndex()
    {
        return 0;
    }
    public virtual void SetModuleHullIndex(int bodyIndex)
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

    // 침묵 — 무기 모듈 자동 공격 일시 정지
    private float m_silenceEndTime = 0f;
    private Coroutine m_silenceVisualCoroutine = null;
    private MeshRenderer[] m_meshRenderers = null;
    private MaterialPropertyBlock m_mpb = null;
    private static readonly int k_baseColorId = Shader.PropertyToID("_BaseColor");

    public void ApplySilence(float duration)
    {
        float endTime = Time.time + duration;
        if (endTime > m_silenceEndTime)
            m_silenceEndTime = endTime;

        // 라스트 어택 시간에 침묵 시간을 더해 침묵 기간만큼 쿨타임 밀림
        SetLastAttackTime(GetLastAttackTime() + duration);

        if (m_silenceVisualCoroutine != null)
            StopCoroutine(m_silenceVisualCoroutine);
        m_silenceVisualCoroutine = StartCoroutine(SilenceVisualCoroutine());
    }

    private IEnumerator SilenceVisualCoroutine()
    {
        SetSilenceTint(true);
        yield return new WaitUntil(() => IsSilenced() == false);
        SetSilenceTint(false);
        m_silenceVisualCoroutine = null;
    }

    private void SetSilenceTint(bool silenced)
    {
        if (m_meshRenderers == null)
            m_meshRenderers = GetComponentsInChildren<MeshRenderer>();
        if (m_mpb == null)
            m_mpb = new MaterialPropertyBlock();

        foreach (MeshRenderer mr in m_meshRenderers)
        {
            if (mr == null) continue;
            if (silenced == true)
            {
                mr.GetPropertyBlock(m_mpb);
                Color silenceColor = DataManager.Instance.m_colorPalette.GetColor("Silence");
                m_mpb.SetColor(k_baseColorId, silenceColor);
                mr.SetPropertyBlock(m_mpb);
            }
            else
            {
                mr.SetPropertyBlock(null);
            }
        }
    }

    public bool IsSilenced()
    {
        return Time.time < m_silenceEndTime;
    }

    public float GetSilenceEndTime()
    {
        return m_silenceEndTime;
    }

    // 공격 딜레이 추가(교란) — 현재 또는 예정된 쿨타임 만료 시점에서 delay만큼 연장
    public void ApplyDisrupt(float delay)
    {
        float lastAttack = GetLastAttackTime();
        float effectiveBase = Mathf.Max(lastAttack, Time.time);
        SetLastAttackTime(effectiveBase + delay);
    }

    // 코루틴 재시작 (Body 교체 등으로 모듈이 재활성화될 때 호출)
    public virtual void RestartCoroutines()
    {
        // 기본 구현 없음 - 각 모듈에서 필요시 override
    }

    // 보상카드 지속버프 배율 — 내 함대(fleet_source_player) 모듈에서만 1이 아님. 공격/쿨다운 등 "사용 시점에 즉시 반영되어야 하는" 값에 매번 곱해서 씀
    // (스폰 시 1회성으로 구워넣지 않는 이유: 버프는 존 런 도중에도 계속 늘어나므로, 이미 스폰된 함선도 다음 공격부터 바로 반영되어야 함)
    public float GetRewardCardBuffMultiplier(ECardEffectType effectType)
    {
        if (m_ownerFleet == null || m_ownerFleet.m_fleetSource != EFleetSource.fleet_source_player) return 1f;
        return ObjectManager.Instance.m_rewardCardSessionState.GetMultiplier(effectType);
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
