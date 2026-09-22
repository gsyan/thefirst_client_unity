// 요격체 모듈 — 슬롯/3D 배치 없이 함체(ModuleHull)에 논리적으로만 붙는 컴포넌트(ModuleShield와 동일 패턴).
// 실제로 눈에 보이는 요격체 유닛(InterceptorUnit)은 별도로 스폰/풀링해 함선 전방 원형 궤도에 배치한다.
// 전술 토글(idx=4) ON 상태에서만 빈 자리를 순차 보충하고(interceptorRegenTime초마다 1기, 생성 1기당 tacticInterceptorCost 과금,
// 여유 없으면 토글이 즉시 꺼짐 — SpaceFleet.TryChargeInterceptorTacticCost 참고) 적 미사일을 탐지/배정함.
// 토글 OFF 시에는 신규 생성만 멈출 뿐 이미 떠 있는 유닛은 제거되지 않고 계속 요격 임무를 수행함(despawn/환급 없음) —
// 정리는 존런 종료 시점(SpaceFleet.ClearAllInterceptorUnits)이나 함선 파괴 시에만 일어남.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModuleInterceptor : ModuleBase
{
    private const int k_interceptorTacticBit = 1 << 4;
    private const float SCAN_INTERVAL = 0.2f;
    private const float DETECTION_RADIUS = 30f;

    [SerializeField] private ModuleHull m_parentBody;

    private int m_maxCount;
    private float m_regenTime;     // 요격체 1기가 다시 생성되기까지 걸리는 시간(초)
    private float m_regenProgress;  // 현재 빈 자리를 채우기 위해 누적된 시간(초)
    private bool m_tacticOn;
    private InterceptorUnit[] m_slots = new InterceptorUnit[0];
    private Coroutine m_scanCoroutine;

    public void SetParentBody(ModuleHull parentBody)
    {
        m_parentBody = parentBody;
    }

    // regenTimePoints: 회복시간 강화 투자 포인트 — 성능 표시(ShipStatCalculator)와 같은 공식으로 반영
    public void InitializeModuleInterceptor(string interceptorSubType, int regenTimePoints)
    {
        m_parentBody = GetComponentInParent<ModuleHull>();
        AutoDetectFleetInfo();

        ClearAllSlots();

        if (string.IsNullOrEmpty(interceptorSubType) == true)
        {
            m_maxCount = 0;
            m_regenTime = 0f;
            m_slots = new InterceptorUnit[0];
            return;
        }

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(interceptorSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleInterceptor");
            m_maxCount = 0;
            m_regenTime = 0f;
            m_slots = new InterceptorUnit[0];
            return;
        }

        m_maxCount = moduleData.interceptorCount;
        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        m_regenTime = ShipStatCalculator.ComputeInterceptorRegenTime(moduleData.interceptorRegenTime, regenTimePoints, formula);
        m_slots = new InterceptorUnit[m_maxCount];
        m_regenProgress = 0f;

        bool isPlayerFleet = m_ownerFleet != null && m_ownerFleet.m_fleetSource == EFleetSource.fleet_source_player;
        if (isPlayerFleet == true)
        {
            bool tacticBitOn = (m_ownerFleet.m_fleetInfo.tacticOptions & k_interceptorTacticBit) != 0;
            SetTacticOn(tacticBitOn);
        }
        else
        {
            // 적/시네마틱 함대는 전술 토글 UI가 없어 상시 ON으로 취급(실드와 동일 규칙) — 매초 리필 틱은 플레이어 함대만 받으므로 스폰 시점에 즉시 완전 무장
            m_tacticOn = true;
            StartScanCoroutine();
            ForceFillAllSlots();
        }
    }

    public bool IsEquipped()
    {
        return m_maxCount > 0;
    }

    // ProjectileMissile.CheckCollision이 요격체 명중 시 소속 함대(적/아군) 판정에 사용
    public SpaceFleet GetOwnerFleet()
    {
        return m_ownerFleet;
    }

    public bool HasEmptySlot()
    {
        if (m_slots == null) return false;
        for (int i = 0; i < m_slots.Length; i++)
            if (m_slots[i] == null) return true;
        return false;
    }

    public void SetTacticOn(bool on)
    {
        if (m_tacticOn == on) return;
        m_tacticOn = on;

        if (on == false) return; // 신규 생성만 멈춤 — 이미 떠 있는 유닛은 그대로 유지(요격 임무 계속 수행)

        StartScanCoroutine();
    }

    // 전술 토글(요격체) ON 상태에서 SpaceFleet.ApplyInterceptorRegenTickToAllShips가 주기적으로 호출 — tickSeconds만큼 시간을 누적하고, m_regenTime(초) 도달마다 1기 리필
    // 1기 생성마다 tacticInterceptorCost를 선확인 과금 — 여유가 없으면 그 자리에서 이번 틱의 나머지 생성을 포기(토글은 TryChargeInterceptorTacticCost가 이미 꺼둠)
    public void ApplyRegenTick(float tickSeconds)
    {
        if (m_tacticOn == false || m_slots == null) return;
        if (HasEmptySlot() == false)
        {
            m_regenProgress = 0f; // 자리가 비는 순간부터 회복시간을 새로 셈
            return;
        }

        int tacticInterceptorCost = DataManager.Instance.m_dataTableConfig.gameSettings.tactic.tacticInterceptorCost;

        m_regenProgress += tickSeconds;
        while (m_regenProgress >= m_regenTime)
        {
            int emptyIndex = FindLowestEmptyIndex();
            if (emptyIndex < 0)
            {
                m_regenProgress = 0f;
                break;
            }

            if (m_ownerFleet == null || m_ownerFleet.TryChargeInterceptorTacticCost(tacticInterceptorCost) == false)
            {
                m_regenProgress = 0f;
                break;
            }

            SpawnInterceptorUnitAt(emptyIndex);
            m_regenProgress -= m_regenTime;
        }
    }

    // 즉시효과 보상카드(Instant_InterceptorHeal)용 — 최대 슬롯수 대비 healRatio만큼 가산해서 빈 슬롯을 즉시 채움.
    // 자연 회복(ApplyRegenTick)과 달리 m_tacticOn이 꺼져 있어도 채워짐(다른 Instant_ 카드들과 동일하게 토글 무관 즉시 적용)
    public void HealSlotsByRatio(float healRatio)
    {
        if (healRatio <= 0f || m_slots == null || m_maxCount <= 0) return;

        int currentFilled = 0;
        for (int i = 0; i < m_slots.Length; i++)
            if (m_slots[i] != null) currentFilled++;

        int targetFilled = Mathf.Min(m_maxCount, currentFilled + Mathf.RoundToInt(m_maxCount * healRatio));
        int remainToFill = targetFilled - currentFilled;

        for (int i = 0; i < m_slots.Length && remainToFill > 0; i++)
        {
            if (m_slots[i] != null) continue;
            SpawnInterceptorUnitAt(i);
            remainToFill--;
        }
    }

    // 요격 성공 시 InterceptorUnit이 스스로 호출 — 자리를 비움(리필은 다음 ApplyRegenTick에서 처리)
    public void OnUnitConsumed(int index)
    {
        if (m_slots == null || index < 0 || index >= m_slots.Length) return;
        m_slots[index] = null;
    }

    // 함선이 곧 Destroy될 때도(SpaceFleet.RemoveShip) 미리 호출됨 — 파괴된 뒤엔 요격체가 같은 Destroy() 호출에
    // 자식으로 걸려 풀 반납(SetParent)이 막히므로, 반드시 파괴 전에 불러야 함
    public void ClearAllSlots()
    {
        StopScanCoroutine();
        if (m_slots != null)
        {
            for (int i = 0; i < m_slots.Length; i++)
            {
                if (m_slots[i] != null) m_slots[i].ReturnToPoolImmediate();
                m_slots[i] = null;
            }
        }
        m_regenProgress = 0f;
    }

    private void ForceFillAllSlots()
    {
        for (int i = 0; i < m_slots.Length; i++)
            if (m_slots[i] == null) SpawnInterceptorUnitAt(i);
    }

    private int FindLowestEmptyIndex()
    {
        for (int i = 0; i < m_slots.Length; i++)
            if (m_slots[i] == null) return i;
        return -1;
    }

    private InterceptorUnit FindIdleUnit()
    {
        if (m_slots == null) return null;
        for (int i = 0; i < m_slots.Length; i++)
            if (m_slots[i] != null && m_slots[i].IsIdle() == true) return m_slots[i];
        return null;
    }

    private void SpawnInterceptorUnitAt(int index)
    {
        Transform shipTransform = m_parentBody != null ? m_parentBody.transform : transform;
        Transform orbitCenter = m_parentBody != null && m_parentBody.m_interceptorOrbitCenter != null ? m_parentBody.m_interceptorOrbitCenter : shipTransform;
        float orbitRadius = m_parentBody != null ? m_parentBody.m_interceptorOrbitRadius : 2f;
        InterceptorUnit unit = ObjectManager.Instance.m_poolManager.Get<InterceptorUnit>(EPoolName.PROJECTILE_INTERCEPTOR);
        unit.Initialize(this, index, shipTransform, orbitCenter, orbitRadius, m_maxCount);
        m_slots[index] = unit;
    }

    private void StartScanCoroutine()
    {
        if (m_scanCoroutine != null) return;
        m_scanCoroutine = StartCoroutine(Co_ScanForTargets());
    }

    private void StopScanCoroutine()
    {
        if (m_scanCoroutine == null) return;
        StopCoroutine(m_scanCoroutine);
        m_scanCoroutine = null;
    }

    private IEnumerator Co_ScanForTargets()
    {
        WaitForSeconds wait = new WaitForSeconds(SCAN_INTERVAL);
        while (true)
        {
            yield return wait;
            // m_tacticOn과 무관하게 계속 스캔 — 토글이 꺼져도 이미 떠 있는 유닛은 계속 요격 임무를 수행해야 함(있는 유닛이 없으면 FindIdleUnit이 그냥 null 반환)
            List<ProjectileMissile> threatMissiles = GetThreatMissileList();
            if (threatMissiles == null || threatMissiles.Count == 0) continue;

            Vector3 myPos = transform.position;
            float sqrRadius = DETECTION_RADIUS * DETECTION_RADIUS;

            for (int i = 0; i < threatMissiles.Count; i++)
            {
                ProjectileMissile missile = threatMissiles[i];
                if (missile == null || missile.gameObject.activeInHierarchy == false) continue;
                if (missile.m_claimedBy != null) continue;

                float sqrDist = (missile.transform.position - myPos).sqrMagnitude;
                if (sqrDist > sqrRadius) continue;

                InterceptorUnit idleUnit = FindIdleUnit();
                if (idleUnit == null) break;
                idleUnit.AssignTarget(missile);
            }
        }
    }

    // 내 함대 기준 적 미사일 리스트 — ProjectileMissile.CheckCollision의 아군/적 판정과 동일한 방식(ObjectManager.IsEnemyOfMyTeam)
    private List<ProjectileMissile> GetThreatMissileList()
    {
        if (m_ownerFleet == null) return null;
        bool isPlayerSide = ObjectManager.Instance.IsEnemyOfMyTeam(m_ownerFleet) == false;
        return isPlayerSide ? ObjectManager.Instance.m_enemyMissiles : ObjectManager.Instance.m_friendlyMissiles;
    }

    private void OnDestroy()
    {
        ClearAllSlots();
    }
}
