// 요격체 모듈 — 슬롯/3D 배치 없이 함체(ModuleHull)에 논리적으로만 붙는 컴포넌트(ModuleShield와 동일 패턴).
// 실제로 눈에 보이는 요격체 유닛(InterceptorUnit)은 별도로 스폰/풀링해 함선 전방 원형 궤도에 배치한다.
// 전술 토글(idx=4) ON 상태에서만 빈 자리를 interceptorRegenRate로 순차 보충하고 적 미사일을 탐지/배정함 — UIPanelBattle.Co_DrainTacticPower 참고.
// 토글 OFF 시 궤도에 떠 있는 유닛은 즉시 전부 제거되고, 다시 ON 하면 빈 상태에서부터 순차적으로 채워짐.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModuleInterceptor : ModuleBase
{
    private const int k_interceptorTacticBit = 1 << 4;
    private const float SCAN_INTERVAL = 0.2f;
    private const float DETECTION_RADIUS = 30f;
    private const float ORBIT_RADIUS = 6f;
    private const float ORBIT_FORWARD_OFFSET = 1f;

    [SerializeField] private ModuleHull m_parentBody;

    private int m_maxCount;
    private float m_regenRate;
    private float m_regenProgress;
    private bool m_tacticOn;
    private InterceptorUnit[] m_slots = new InterceptorUnit[0];
    private Coroutine m_scanCoroutine;

    public void SetParentBody(ModuleHull parentBody)
    {
        m_parentBody = parentBody;
    }

    public void InitializeModuleInterceptor(string interceptorSubType)
    {
        m_parentBody = GetComponentInParent<ModuleHull>();
        AutoDetectFleetInfo();

        ClearAllSlots();

        if (string.IsNullOrEmpty(interceptorSubType) == true)
        {
            m_maxCount = 0;
            m_regenRate = 0f;
            m_slots = new InterceptorUnit[0];
            return;
        }

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(interceptorSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleInterceptor");
            m_maxCount = 0;
            m_regenRate = 0f;
            m_slots = new InterceptorUnit[0];
            return;
        }

        m_maxCount = moduleData.interceptorCount;
        m_regenRate = moduleData.interceptorRegenRate;
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

        if (on == false)
        {
            ClearAllSlots();
            return;
        }

        StartScanCoroutine();
    }

    // 전술 토글(요격체) ON 상태에서 SpaceFleet.ApplyInterceptorRegenTickToAllShips가 1초 간격으로 호출 — m_regenRate는 초당 진행도, 1.0 도달마다 1기 리필
    public void ApplyRegenTick()
    {
        if (m_tacticOn == false || m_slots == null) return;
        if (HasEmptySlot() == false) return;

        m_regenProgress += m_regenRate;
        while (m_regenProgress >= 1f)
        {
            int emptyIndex = FindLowestEmptyIndex();
            if (emptyIndex < 0)
            {
                m_regenProgress = 0f;
                break;
            }
            SpawnInterceptorUnitAt(emptyIndex);
            m_regenProgress -= 1f;
        }
    }

    // 요격 성공 시 InterceptorUnit이 스스로 호출 — 자리를 비움(리필은 다음 ApplyRegenTick에서 처리)
    public void OnUnitConsumed(int index)
    {
        if (m_slots == null || index < 0 || index >= m_slots.Length) return;
        m_slots[index] = null;
    }

    private void ClearAllSlots()
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
        InterceptorUnit unit = ObjectManager.Instance.m_poolManager.Get<InterceptorUnit>(EPoolName.PROJECTILE_INTERCEPTOR);
        unit.Initialize(this, index, shipTransform, ORBIT_RADIUS, ORBIT_FORWARD_OFFSET, m_maxCount);
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
            if (m_tacticOn == false) continue;

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
