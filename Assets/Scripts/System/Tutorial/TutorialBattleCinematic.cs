using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Tutorial_FirstPlay_Battle / Tutorial_FirstPlay_Complete 전용 전투 연출(웨이브 스폰/탈출함선/기함폭발) 상태 및 로직.
// TutorialManager 본체를 범용 스텝 상태머신으로 남겨두기 위해 이 튜토리얼 전용 상태를 분리한 것 —
// 앞으로 비슷하게 무거운 연출용 튜토리얼이 추가될 때도 TutorialManager에 필드를 쌓지 말고 이런 전용 컨트롤러를 새로 만들 것
public class TutorialBattleCinematic
{
    private readonly TutorialManager m_host;

    private SpaceFleet m_escapeFleet;   // EscapeShipDistanceFromFlagship 조건이 스폰/추적하는 탈출 함대 — 이후 실제 유저 함대의 기함이 됨
    private Coroutine m_escapeFleetMoveCoroutine; // step 전환(StopTutorialCondition)에 끊기지 않고 계속 이동하도록 별도 관리
    private float m_escapeFleetSpeedMultiplier = 1f; // 워프 연출 시 MoveEscapeFleetForward 재시작 없이 속도만 배가
    private WarpEffectShip m_escapeWarpEffect; // Tutorial_FirstPlay_Complete까지 유지했다가 CleanupEscapeFleet()에서 정지
    private const float ESCAPE_SHIP_SPEED = 10f;
    private readonly List<SpaceFleet> m_enemyWaveFleets = new List<SpaceFleet>(); // EnemyWave1/2 조건이 스폰한 적 함대 목록
    private readonly Dictionary<int, SpaceFleet> m_waveIndexOccupancy = new Dictionary<int, SpaceFleet>(); // positionIndex → 그 자리를 차지한 함대 (전멸하면 자리 반납)

    // 웨이브2 무한 보충 스폰의 on/off — 튜토리얼 스텝은 FlagshipHealthBelowPercent 충족 즉시 다음 스텝으로 넘어가지만,
    // 전투 자체(기존 스폰된 함대들의 교전)는 기함이 체력 플로어라 죽지 않은 채 백그라운드에서 계속 진행됨.
    // 스텝 인덱스로 스폰을 끊으면 그 사이(중간 대사 스텝 등) 보충이 끊겨 결국 적 함대가 전멸해버려서,
    // 실제로 전투가 끝나는 시점(SiegfriedFlagshipExplosion 연출 시작)까지 별도 플래그로 관리
    private bool m_endlessWaveActive;
    private static readonly WaitForSeconds k_cameraTransitionWait = new WaitForSeconds(3f); // 카메라가 탈출 함선으로 넘어갈 시간

    public TutorialBattleCinematic(TutorialManager host)
    {
        m_host = host;
    }

    // 이 컨트롤러가 담당하는 조건 타입 시작 — TutorialManager.StartTutorialCondition에서 위임됨.
    // ownerStepIndex는 이 스텝을 시작한 시점의 스텝 인덱스(소유권 토큰) — 아래 코루틴들이 나중에 깨어났을 때
    // 이미 다른 스텝으로 넘어가 있으면 RequestNextStep()이 조용히 무시하므로, 코루틴을 일일이 취소하지 않아도 안전함
    public void StartCondition(TutorialStep step, int ownerStepIndex)
    {
        switch (step.conditionType)
        {
            case ETutorialConditionType.EscapeShipDistanceFromFlagship:
            {
                SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
                m_escapeFleet = TutorialCinematicController.SpawnEscapeFleet(siegfriedFleet);
                // 이동은 별도 코루틴으로 분리 — step 전환 시 StopTutorialCondition()에 끊기지 않고 계속 이동
                m_escapeFleetMoveCoroutine = m_host.StartCoroutine(MoveEscapeFleetForward());
                m_host.StartCoroutine(CheckEscapeShipDistance(siegfriedFleet, step.conditionThreshold, ownerStepIndex));
                break;
            }

            case ETutorialConditionType.EnemyWave1:
            {
                // 지크프리트 기함은 전투 데미지로는 실제 파괴되지 않고 10%에서 버티다가 이후 연출(폭발)로만 파괴됨
                SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
                SpaceShip flagship = siegfriedFleet != null ? siegfriedFleet.GetFlagship() : null;
                if (flagship != null)
                    flagship.m_minHealthRatio = 0.1f;

                m_host.StartCoroutine(SpawnEnemyWaveRoutine(new int[] { 4, 2, 1 }, fleetCount: 5, spawnInterval: 5f, ownerStepIndex: ownerStepIndex));
                break;
            }

            case ETutorialConditionType.EnemyWave2:
                // 애초에 전멸이 불가능한 물량 — 스폰 코루틴은 전멸 대기 없이 스폰만 담당하고,
                // 다음 스텝 전환은 별도로 띄운 FlagshipHealthBelowPercent 코루틴이 담당.
                // m_endlessWaveActive는 이 스텝이 끝나도(스텝 전환) 꺼지지 않음 — SiegfriedFlagshipExplosion이 시작될 때 꺼짐
                m_endlessWaveActive = true;
                m_host.StartCoroutine(SpawnEnemyWaveRoutine(new int[] { 9, 6, 5, 4, 3 }, fleetCount: 10, spawnInterval: 5f, waitForFullClear: false, ownerStepIndex: ownerStepIndex));
                m_host.StartCoroutine(CheckFlagshipHealthBelowPercent(0.1f, ownerStepIndex));
                break;

            case ETutorialConditionType.FlagshipHealthBelowPercent:
                // conditionThreshold는 CSV에 비율(0~1)로 직접 기재 — 런타임 변환 없이 그대로 사용
                m_host.StartCoroutine(CheckFlagshipHealthBelowPercent(step.conditionThreshold, ownerStepIndex));
                break;

            case ETutorialConditionType.SiegfriedFlagshipExplosion:
                // 여기서부터 실제로 전투가 끝나는 연출이 시작되므로 웨이브2 보충 스폰을 멈춤
                m_endlessWaveActive = false;
                m_host.StartCoroutine(PlaySiegfriedFlagshipExplosion(step.conditionThreshold, ownerStepIndex));
                break;

            case ETutorialConditionType.CleanupEscapeFleet:
                CleanupEscapeFleet();
                m_host.RequestNextStep(ownerStepIndex);
                break;
        }
    }

    // 스킵 등으로 튜토리얼을 도중에 끝낼 때 남아있는 연출용 함대(탈출선/적 웨이브)를 정리
    public void Cleanup()
    {
        m_endlessWaveActive = false;
        CleanupEscapeFleet();

        foreach (SpaceFleet fleet in m_enemyWaveFleets)
        {
            if (fleet != null)
                TutorialCinematicController.DespawnCinematicFleet(fleet);
        }
        m_enemyWaveFleets.Clear();
        m_waveIndexOccupancy.Clear();

        // 워프 가속 배율은 CleanupEscapeFleet()이 건드리지 않으므로 다음 세션에 새어 들어가지 않게 여기서 리셋
        m_escapeFleetSpeedMultiplier = 1f;
    }

    // maxCount 범위 안에서 비어있는(한 번도 안 쓰였거나, 배정된 함대가 전멸한) 가장 낮은 인덱스를 반환 — 자리가 없으면 -1.
    // 전멸한 함대는 빈 껍데기 오브젝트로 씬에 계속 남지 않도록 자리를 반납하는 시점에 바로 Despawn
    private int GetLowestFreeWaveIndex(int maxCount)
    {
        for (int i = 0; i < maxCount; i++)
        {
            if (m_waveIndexOccupancy.TryGetValue(i, out SpaceFleet fleet) == false)
                return i;

            if (fleet == null)
                return i;

            if (fleet.IsFleetAlive() == false)
            {
                TutorialCinematicController.DespawnCinematicFleet(fleet);
                m_waveIndexOccupancy.Remove(i);
                return i;
            }
        }
        return -1;
    }

    // fleetCount개의 적 함대를 spawnInterval 간격으로 순차 스폰한 뒤, waitForFullClear가 true면 전부 전멸할 때까지 대기 후 NextStep
    // waitForFullClear가 false면 스폰만 담당하고 끝남 — 애초에 전멸 불가능한 물량용, 다음 스텝 전환은 별도 조건이 담당
    // positionIndex 자리가 꽉 차 있으면(그레이드 그룹의 프리셋 수보다 fleetCount가 많은 경우) 빈 자리가 날 때까지 대기 후 스폰
    private IEnumerator SpawnEnemyWaveRoutine(int[] shipGradeLevels, int fleetCount, float spawnInterval, int ownerStepIndex, bool waitForFullClear = true)
    {
        m_enemyWaveFleets.Clear();
        m_waveIndexOccupancy.Clear();

        // TODO(3단계): 삭제된 DataTableZone.GetFleetPositionCount를 대체할 신규 탐사 그리드 기준 자리 수 산출로 교체 필요
        // 스폰 지점은 5개(부채꼴 정면 1 + 좌우 2씩)로 고정 — 웨이브2도 이 5자리를 계속 재사용(전멸한 자리만 리스폰)하는 방식으로 무한 보충
        const int k_tempMaxPositions = 5;
        int maxPositions = k_tempMaxPositions;

        // 스폰 기준 방향을 이 인카운터 시작 시점에 한 번만 고정 — 전투 중 내 함대가 조금씩 회전해도 이후 스폰되는
        // 함대들이 같은 기준으로 부채꼴 자리를 유지하게 함(매 스폰마다 실시간으로 다시 읽으면 자리가 서서히 겹침)
        SpaceFleet siegfriedFleetForBasis = ObjectManager.Instance.GetMyFleet();
        Vector3 waveBasePos = siegfriedFleetForBasis != null ? siegfriedFleetForBasis.transform.position : Vector3.zero;
        Vector3 waveForward = siegfriedFleetForBasis != null ? siegfriedFleetForBasis.transform.forward : Vector3.forward;
        Vector3 waveUp = siegfriedFleetForBasis != null ? siegfriedFleetForBasis.transform.up : Vector3.up;

        for (int i = 0; i < fleetCount; i++)
        {
            int positionIndex = GetLowestFreeWaveIndex(maxPositions);
            while (m_host.IsPlaying && positionIndex < 0)
            {
                // 자리 대기 중 중단 조건 — waitForFullClear가 true(웨이브1)면 스텝 전환 즉시 중단, false(웨이브2)면
                // 스텝이 바뀌어도(중간 대사 스텝 등) 계속 스폰하고 m_endlessWaveActive가 꺼질 때만 중단
                if (waitForFullClear == true && m_host.GetCurrentStepIndex() != ownerStepIndex) yield break;
                if (waitForFullClear == false && m_endlessWaveActive == false) yield break;
                yield return null;
                positionIndex = GetLowestFreeWaveIndex(maxPositions);
            }
            if (!m_host.IsPlaying) yield break;
            if (waitForFullClear == true && m_host.GetCurrentStepIndex() != ownerStepIndex) yield break;
            if (waitForFullClear == false && m_endlessWaveActive == false) yield break;

            SpaceFleet fleet = TutorialCinematicController.SpawnEnemyWaveFleet(shipGradeLevels, positionIndex, waveBasePos, waveForward, waveUp);
            if (fleet != null)
            {
                m_enemyWaveFleets.Add(fleet);
                m_waveIndexOccupancy[positionIndex] = fleet;
            }

            if (i < fleetCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        if (waitForFullClear == false)
        {
            // "애초에 전멸이 불가능한 물량" 설계 의도 — 초기 fleetCount만 스폰하고 끝내버리면 유저가 그걸 다 잡았을 때
            // 물량이 고갈돼버림. 격파로 자리가 빌 때마다(GetLowestFreeWaveIndex) 계속 리스폰 — 스텝이 넘어가도(FlagshipHealthBelowPercent
            // 충족 후 다음 대사 스텝 등) 전투 자체는 백그라운드에서 계속되므로, m_endlessWaveActive가 꺼질 때(SiegfriedFlagshipExplosion 시작)까지 유지
            while (m_host.IsPlaying && m_endlessWaveActive == true)
            {
                int positionIndex = GetLowestFreeWaveIndex(maxPositions);
                if (positionIndex < 0)
                {
                    yield return null;
                    continue;
                }

                SpaceFleet fleet = TutorialCinematicController.SpawnEnemyWaveFleet(shipGradeLevels, positionIndex, waveBasePos, waveForward, waveUp);
                if (fleet != null)
                {
                    m_enemyWaveFleets.Add(fleet);
                    m_waveIndexOccupancy[positionIndex] = fleet;
                }

                yield return new WaitForSeconds(spawnInterval);
            }
            yield break;
        }

        while (m_host.IsPlaying)
        {
            if (m_host.GetCurrentStepIndex() != ownerStepIndex) yield break;

            bool anyAlive = false;
            foreach (SpaceFleet fleet in m_enemyWaveFleets)
            {
                if (fleet != null && fleet.IsFleetAlive())
                {
                    anyAlive = true;
                    break;
                }
            }

            if (anyAlive == false)
            {
                m_host.RequestNextStep(ownerStepIndex);
                yield break;
            }

            yield return null;
        }
    }

    // 내 함대(지크프리트) 기함 체력 비율이 ratioThreshold(0~1) 이하로 떨어질 때까지 대기
    private IEnumerator CheckFlagshipHealthBelowPercent(float ratioThreshold, int ownerStepIndex)
    {
        while (m_host.IsPlaying)
        {
            if (m_host.GetCurrentStepIndex() != ownerStepIndex) yield break;

            SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
            SpaceShip flagship = siegfriedFleet != null ? siegfriedFleet.GetFlagship() : null;
            if (flagship != null && flagship.IsAlive() == true)
            {
                float healthRatio = flagship.GetHealthRatio();
                if (healthRatio <= ratioThreshold)
                {
                    m_host.RequestNextStep(ownerStepIndex);
                    yield break;
                }
            }

            yield return null;
        }
    }

    // 카메라를 탈출 함선으로 전환하고, 지크프리트 기함을 연출로 파괴한 뒤 waitSeconds초 대기 후 다음 스텝
    private IEnumerator PlaySiegfriedFlagshipExplosion(float waitSeconds, int ownerStepIndex)
    {
        SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
        SpaceShip flagship = siegfriedFleet != null ? siegfriedFleet.GetFlagship() : null;

        if (m_escapeFleet != null && CameraController.Instance != null)
        {
            CameraController.Instance.SetTargetOfCameraController(m_escapeFleet.transform);

            // 탈출선 정면 기준 좌로 10도, 위로 10도 꺾인 각도 + 그 함선의 최대 줌
            float escapeYaw = m_escapeFleet.transform.eulerAngles.y;
            CameraController.Instance.SetTargetRotation(escapeYaw + 180f + 30f, +30f);

            SpaceShip escapeShip = m_escapeFleet.GetFlagship();
            if (escapeShip != null && escapeShip.m_moduleHulls.Count > 0 && escapeShip.m_moduleHulls[0] != null)
            {
                CameraController.Instance.ApplyZoomRangeFromShip(escapeShip);
                CameraController.Instance.SetTargetZoom(escapeShip.m_moduleHulls[0].m_cameraMaxZoom);
            }
        }

        // 카메라가 탈출 함선으로 넘어갈 시간을 벌어준 뒤 기함 폭발
        yield return k_cameraTransitionWait;

        if (flagship != null)
            flagship.DestroyForCinematic();

        // 워프 배속(SpaceFleet.SpawnApproachSpeedMult, 기존 워프 진입과 동일한 배율)으로 가속 + 워프 이펙트
        // 탈출선은 화면 밖으로 사라지는 연출용일 뿐 실제 함대로 승격하지 않음 — Tutorial_FirstPlay_Complete까지 그대로 날아가다가
        // CleanupEscapeFleet()(StartNormalPlay 직전)에서 한 번에 정리됨
        SpaceShip escapeFlagship = m_escapeFleet != null ? m_escapeFleet.GetFlagship() : null;
        if (escapeFlagship != null)
        {
            m_escapeFleetSpeedMultiplier = m_escapeFleet.SpawnApproachSpeedMult;

            if (escapeFlagship.TryGetComponent(out m_escapeWarpEffect) == false)
            {
                m_escapeWarpEffect = escapeFlagship.gameObject.AddComponent<WarpEffectShip>();
                m_escapeWarpEffect.InitializeWarpEffect();
            }
            m_escapeWarpEffect.StartFleetWarpIn();

            // 카메라 lerp 추적 속도로는 워프 가속을 못 따라가 화면 밖으로 벗어나므로, 이 구간만 위치를 매 프레임 그대로 스냅
            if (CameraController.Instance != null)
                CameraController.Instance.SetInstantFollow(true);
        }

        yield return new WaitForSeconds(waitSeconds);

        // 튜토리얼 전투를 위해 존재했던 적 함대/발사체/함재기 전부 정리 (탈출선/워프 이펙트/이동은 유지)
        ObjectManager.Instance.RemoveAllEnemyFleets();
        ObjectManager.Instance.CleanupAllProjectiles();
        ObjectManager.Instance.DestroyAllAircraft();

        // 지크프리트 잔존 함대만 여기서 제거 — 탈출선은 Tutorial_FirstPlay_Complete가 끝날 때까지 유지
        ObjectManager.Instance.DestroyTutorialFleet(siegfriedFleet);

        m_host.RequestNextStep(ownerStepIndex);
    }

    // Tutorial_FirstPlay_Complete까지 끝난 뒤(StartNormalPlay 직전) 탈출선/워프이펙트/이동 코루틴을 한 번에 정리
    public void CleanupEscapeFleet()
    {
        if (m_escapeWarpEffect != null)
        {
            m_escapeWarpEffect.StopWarp();
            m_escapeWarpEffect = null;
        }

        if (CameraController.Instance != null)
            CameraController.Instance.SetInstantFollow(false);

        if (m_escapeFleetMoveCoroutine != null)
        {
            m_host.StopCoroutine(m_escapeFleetMoveCoroutine);
            m_escapeFleetMoveCoroutine = null;
        }

        if (m_escapeFleet != null)
        {
            TutorialCinematicController.DespawnCinematicFleet(m_escapeFleet);
            m_escapeFleet = null;
        }
    }

    // 탈출 함선을 계속 기존 방향/속도로 전진시킴 — step 전환과 무관하게 별도 코루틴으로 유지
    private IEnumerator MoveEscapeFleetForward()
    {
        while (m_host.IsPlaying && m_escapeFleet != null)
        {
            m_escapeFleet.transform.position += m_escapeFleet.transform.forward * (ESCAPE_SHIP_SPEED * m_escapeFleetSpeedMultiplier * Time.deltaTime);
            yield return null;
        }
    }

    // 기함과의 거리가 threshold 이상 벌어질 때까지 대기 (이동 자체는 MoveEscapeFleetForward가 담당)
    private IEnumerator CheckEscapeShipDistance(SpaceFleet siegfriedFleet, float threshold, int ownerStepIndex)
    {
        while (m_host.IsPlaying)
        {
            if (m_host.GetCurrentStepIndex() != ownerStepIndex) yield break;

            if (m_escapeFleet == null || siegfriedFleet == null)
            {
                yield return null;
                continue;
            }

            SpaceShip flagship = siegfriedFleet.GetFlagship();
            if (flagship != null)
            {
                float sqrDistance = (m_escapeFleet.transform.position - flagship.transform.position).sqrMagnitude;
                if (sqrDistance >= threshold * threshold)
                {
                    m_host.RequestNextStep(ownerStepIndex);
                    yield break;
                }
            }

            yield return null;
        }
    }
}
