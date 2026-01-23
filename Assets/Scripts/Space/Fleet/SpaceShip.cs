//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

// 능력치 프로파일 구조체 (함선/함대의 전투 및 작전 능력)
[System.Serializable]
public struct CapabilityProfile
{
    // 기존 능력치 (하위 호환성 유지 - deprecated)
    public int totalWeapons;
    public int totalEngines;

    // 세부 전투 능력치
    public float attackDps;        // 초당 공격력 (DPS)
    public float hp;               // 체력
    public float engineSpeed;      // 엔진 속도 (이동+회전 통합)
    public float cargoCapacity;    // 화물 용량

    // 육각형 차트용 포괄적 능력치
    public float firepower;        // 화력
    public float survivability;    // 생존력
    public float mobility;         // 기동력
    public float logistics;        // 군수
    public float sustainment;      // 지속력
    public float detection;        // 탐지력

    public override string ToString()
    {
        return $"Weapons: {totalWeapons}, Engines: {totalEngines}\n" +
                $"AttackDPS: {attackDps:F1}, HP: {hp:F1}, " +
                $"EngineSpeed: {engineSpeed:F1}, Cargo: {cargoCapacity:F1}, " +                
                $"Firepower: {firepower:F1}, Survivability: {survivability:F1}, " +
                $"Mobility: {mobility:F1}, Logistics: {logistics:F1}";
    }
}

public class SpaceShip : MonoBehaviour
{
    [SerializeField] public ShipInfo m_shipInfo;
    [SerializeField] public List<ModuleBody> m_moduleBodys = new List<ModuleBody>();
    [SerializeField] public SpaceShip m_targetShip;
    [SerializeField] public CapabilityProfile m_spaceShipStatsOrg;
    [SerializeField] public CapabilityProfile m_spaceShipStatsCur;

    public SpaceFleet m_myFleet;
    public EShipState m_shipState;
    [HideInInspector] public Outline m_shipOutline;

    private GaugeBars m_gaugeBars;
    public AirCraftPathGrid m_airCraftPathGrid;

    virtual protected void Start()
    {
        InitializeGaugeDisplay();
    }

    private void InitializeGaugeDisplay()
    {
        m_gaugeBars = GetComponent<GaugeBars>();
        if (m_gaugeBars == null)
            m_gaugeBars = gameObject.AddComponent<GaugeBars>();
    }

    public void InitializeSpaceShip(SpaceFleet fleet, ShipInfo shipInfo)
    {
        m_myFleet = fleet;
        ApplyFleetStateToShip();

        m_shipInfo = shipInfo;
        if (shipInfo.bodies == null || shipInfo.bodies.Count == 0) return;
        foreach (ModuleBodyInfo bodyInfo in shipInfo.bodies)
            InitSpaceShipBody(bodyInfo, null);

        m_spaceShipStatsOrg = GetShipCapabilityProfile(true);
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);

        SetupSelectedModuleVisualing();
        
        // AirCraftPathGrid, 지금은 바디가 오직 하나...
        m_airCraftPathGrid = m_moduleBodys[0].GetComponent<AirCraftPathGrid>();
        
        // Outline 미리 설정
        m_shipOutline = gameObject.AddComponent<Outline>();
        m_shipOutline.OutlineMode = Outline.Mode.OutlineAll;
        m_shipOutline.OutlineColor = Color.cyan;
        m_shipOutline.OutlineWidth = 5f;
        m_shipOutline.enabled = false; // 기본은 꺼둠

    }

    public int GetAverageModuleLevel()
    {
        if (m_shipInfo.bodies == null || m_shipInfo.bodies.Count == 0) return 0;
        int totalLevel = 0;
        int moduleCount = 0;

        foreach (ModuleBodyInfo body in m_shipInfo.bodies)
        {
            totalLevel += body.moduleLevel;
            moduleCount++;

            if (body.engines != null)
            {
                foreach (ModuleInfo engine in body.engines)
                {
                    totalLevel += engine.moduleLevel;
                    moduleCount++;
                }
            }

            if (body.beams != null)
            {
                foreach (ModuleInfo beam in body.beams)
                {
                    totalLevel += beam.moduleLevel;
                    moduleCount++;
                }
            }

            if (body.missiles != null)
            {
                foreach (ModuleInfo missile in body.missiles)
                {
                    totalLevel += missile.moduleLevel;
                    moduleCount++;
                }
            }

            if (body.hangers != null)
            {
                foreach (ModuleInfo hanger in body.hangers)
                {
                    totalLevel += hanger.moduleLevel;
                    moduleCount++;
                }
            }
        }

        if (moduleCount == 0) return 0;
        return totalLevel / moduleCount;
    }

   // Body 초기화 (기존 모듈 재사용 가능)
    private ModuleBody InitSpaceShipBody(ModuleBodyInfo bodyInfo, List<ModuleBase> savedModules)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(bodyInfo.moduleType.ToString(), bodyInfo.moduleSubType.ToString(), bodyInfo.moduleLevel);
        if (modulePrefab == null) return null;

        GameObject bodyObj = Instantiate(modulePrefab, transform.position, transform.rotation);
        bodyObj.transform.SetParent(transform);

        ModuleBody moduleBody = bodyObj.GetComponent<ModuleBody>();
        if (moduleBody == null)
            moduleBody = bodyObj.AddComponent<ModuleBody>();

        moduleBody.InitializeModuleBody(bodyInfo, savedModules);
        m_moduleBodys.Add(moduleBody);
        moduleBody.ApplyShipStateToModule(); // 모듈 변경시를 위해 필요
        return moduleBody;
    }

    private ModuleBody m_currentTargetBody;
    private Coroutine m_findTargetModuleBodyCoroutine;

    public void ApplyFleetStateToShip()
    {
        switch (m_myFleet.m_fleetState)
        {
            case EFleetState.None:
                m_shipState = EShipState.None;
                StopAutoCombat();
                break;
            case EFleetState.Move:
                m_shipState = EShipState.Move;
                StopAutoCombat();
                break;
            case EFleetState.Battle:
                m_shipState = EShipState.Battle;
                if (m_findTargetModuleBodyCoroutine == null)
                    m_findTargetModuleBodyCoroutine = StartCoroutine(FindTargetModuleBody());
                break;
            default:
                m_shipState = EShipState.None;
                StopAutoCombat();
                break;
        }
        
        foreach (ModuleBody body in m_moduleBodys)
            body.ApplyShipStateToModule();
    }



    public void StopAutoCombat()
    {
        if (m_findTargetModuleBodyCoroutine != null)
        {
            StopCoroutine(m_findTargetModuleBodyCoroutine);
            m_findTargetModuleBodyCoroutine = null;
        }
    }

    private IEnumerator FindTargetModuleBody()
    {
        while (true)
        {
            if (m_currentTargetBody == null || m_currentTargetBody.m_health <= 0)
            {
                if (m_targetShip == null || m_targetShip.IsAlive() == false)
                {
                    if (m_myFleet != null && m_myFleet.m_isEnemyFleet == true)
                    {
                        if (ObjectManager.Instance.m_myFleet != null)
                            m_targetShip = ObjectManager.Instance.m_myFleet.GetRandomAliveShip();
                    }
                    else
                    {
                        m_targetShip = ObjectManager.Instance.GetEnemy();
                    }
                }

                if (m_targetShip != null)
                    m_currentTargetBody = m_targetShip.GetRandomAliveBody();

                if (m_currentTargetBody == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }
            }

            foreach (ModuleBody body in m_moduleBodys)
            {
                if (body != null && body.m_health > 0)
                    body.SetTarget(m_currentTargetBody);
            }

            yield return null;
        }
    }

    virtual public void TakeDamage(float attackPower)
    {
        // 살아있는 바디 중 하나에 랜덤으로 데미지 분산 (또는 첫 번째 바디에)
        ModuleBody targetBody = GetRandomAliveBody();
        if (targetBody != null)
        {
            targetBody.TakeDamage(attackPower);
        }

        // 전체 함선 체력 재계산
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);

        if (m_spaceShipStatsCur.hp <= 0.0f)
            OnSpaceShipDestroyed();
    }

    // 함선이 살아있는지 확인
    public bool IsAlive()
    {
        return m_spaceShipStatsCur.hp > 0 && HasAliveBodies();
    }

    // 살아있는 바디가 있는지 확인
    private bool HasAliveBodies()
    {
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_health > 0)
            {
                return true;
            }
        }
        return false;
    }

    // 살아있는 바디 중 랜덤 선택
    private ModuleBody GetRandomAliveBody()
    {
        List<ModuleBody> aliveBodies = new List<ModuleBody>();
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_health > 0)
            {
                aliveBodies.Add(body);
            }
        }

        if (aliveBodies.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, aliveBodies.Count);
            return aliveBodies[randomIndex];
        }

        return null;
    }

    public void UpdateShipStatCur()
    {
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);
    }

    public void UpdateShipStats()
    {
        m_spaceShipStatsOrg = GetShipCapabilityProfile(true);
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);
    }

    // ModuleBody에서 호출되는 파괴 체크 메서드
    public void CheckForDestruction()
    {
        if (IsAlive() == true) return;

        // SpaceFleet에서 자신을 제거
        SpaceFleet parentFleet = GetComponentInParent<SpaceFleet>();
        if (parentFleet != null)
        {
            parentFleet.RemoveShip(this);
            if (parentFleet.m_isEnemyFleet == true)
            {
                // 적 파괴시 보상
                DeveloperConsole.ExecuteCommandStatic("AddMineral 50");
            }
        }


        OnSpaceShipDestroyed();
        Destroy(gameObject);


    }

    // 함선 파괴 시 호출
    virtual protected void OnSpaceShipDestroyed()
    {
        // 코루틴 중지
        StopAllCoroutines();

        // 필요한 정리 작업
        // 예: 폭발 이펙트, 점수 추가, 리스폰 등

        // 게임 오브젝트 비활성화 또는 파괴
        // gameObject.SetActive(false);
    }

    // 인덱스로 바디 찾기
    public ModuleBody FindModuleBodyByIndex(int bodyIndex)
    {
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_moduleBodyInfo.bodyIndex == bodyIndex)
            {
                return body;
            }
        }
        return null;
    }

    // bodyIndex, moduleTypePacked, slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(int bodyIndex, EModuleType moduleType, int slotIndex)
    {
        ModuleBody body = FindModuleBodyByIndex(bodyIndex);
        if (body == null) return null;

        if (moduleType == EModuleType.Body)
            return body;

        return body.FindModule(moduleType, slotIndex);
    }

    // 함선의 능력치 프로파일 계산
    // bByInfo = true: Info 기반 계산 (최대 스펙)
    // bByInfo = false: 실제 상태 기반 계산 (현재 체력/상태 반영)
    public CapabilityProfile GetShipCapabilityProfile(bool bByInfo = true)
    {
        if (bByInfo == true) return CommonUtility.GetShipCapabilityProfile(m_shipInfo);

        CapabilityProfile stats = new CapabilityProfile();
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_health > 0)
            {
                CapabilityProfile bodyStats = body.GetModuleCapabilityProfile(false);
                stats.attackDps += bodyStats.attackDps;
                stats.hp += bodyStats.hp;
                stats.engineSpeed += bodyStats.engineSpeed;
                stats.cargoCapacity += bodyStats.cargoCapacity;               
                stats.totalWeapons += bodyStats.totalWeapons;
                stats.totalEngines += bodyStats.totalEngines;
            }
        }

        // 육각형 능력치 자동 계산
        stats.firepower = stats.attackDps;
        stats.survivability = stats.hp;
        stats.mobility = stats.engineSpeed;
        stats.logistics = stats.cargoCapacity;
        stats.sustainment = 0; // 향후 확장
        stats.detection = 0;   // 향후 확장

        return stats;
    }


    #region Display migration ============================================================
    // Private fields
    private List<SelectedModuleVisual> m_selectedModuleVisuals = new List<SelectedModuleVisual>();
    private ModuleBase m_selectedModule = null;

    private void SetupSelectedModuleVisualing()
    {
        // Setup SelectedModuleVisual for parts bodies
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null)
            {
                SetupSelectedModuleVisual(body);

                // Setup SelectedModuleVisual for all modules in slots
                foreach (ModuleSlot slot in body.m_moduleSlots)
                {
                    if (slot != null && slot.transform.childCount > 0)
                    {
                        ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                        if (module != null)
                            SetupSelectedModuleVisual(module);
                    }
                }
            }
        }
    }

    private void SetupSelectedModuleVisual(ModuleBase moduleBase)
    {
        // Add SelectedModuleVisual component
        SelectedModuleVisual selectedModuleVisual = moduleBase.gameObject.AddComponent<SelectedModuleVisual>();
        selectedModuleVisual.InitializeSelectedModuleVisual(this, moduleBase);
        m_selectedModuleVisuals.Add(selectedModuleVisual);
    }

    

    private Bounds CalculatePartsBounds(ModuleBase partsBase)
    {
        Bounds bounds = new Bounds(partsBase.transform.position, Vector3.one);

        // Include all child renderers
        Renderer[] renderers = partsBase.GetComponentsInChildren<Renderer>();
        bool hasRenderers = false;

        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled)
            {
                if (!hasRenderers)
                {
                    bounds = renderer.bounds;
                    hasRenderers = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        // Ensure minimum size for interaction
        if (!hasRenderers || bounds.size.magnitude < 1f)
        {
            bounds.center = partsBase.transform.position;
            bounds.size = Vector3.one * 1.5f;
        }

        return bounds;
    }

    public void SetSelectedModule(SpaceShip ship, ModuleBase module)
    {
        if (m_myFleet == null) return;
        if (this != ship) return;
        m_myFleet.ClearAllSelectedModule();
        m_selectedModule = module;
        UpdateSelectedModuleVisual();
    }

    public void ClearSelectedModule()
    {
        m_selectedModule = null;
        UpdateSelectedModuleVisual();
    }

    private void UpdateSelectedModuleVisual()
    {
        foreach (var selectedModuleVisual in m_selectedModuleVisuals)
        {
            if (selectedModuleVisual != null)
            {
                bool isSelected = (selectedModuleVisual.ModuleBase == m_selectedModule);
                selectedModuleVisual.SetSelected(isSelected);
            }
        }
    }
    #endregion


    #region Autonomous Formation Movement
    private Vector3 m_targetPosition;
    private bool m_isMovingToFormation = false;
    private float m_detectionDistanceLimit = 6f;

    private float CalculateShipSize()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 1f; // 기본값

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        // 가장 큰 축을 기준으로 크기 결정
        return Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
    }

    public Vector3 CalculateShipPosition(EFormationType formationType, float spacing = 0f)
    {
        var gameSettings = DataManager.Instance?.m_dataTableConfig?.gameSettings;
        int positionIndex = (int)m_shipInfo.positionIndex;

        // 함선 크기를 계산하여 spacing에 반영
        float shipSize = CalculateShipSize();
        float sizeAdjustment = shipSize * 1.0f; // 함선 크기의 절반을 추가 간격으로 사용

        switch (formationType)
        {
            case EFormationType.LinearHorizontal:
                return CalculateLinearHorizontalPosition(positionIndex, spacing + sizeAdjustment);
            case EFormationType.LinearVertical:
                return CalculateLinearVerticalPosition(positionIndex, spacing + sizeAdjustment);
            case EFormationType.LinearDepth:
                return CalculateLinearDepthPosition(positionIndex, spacing + sizeAdjustment);
            case EFormationType.Grid:
                return CalculateGridPosition(positionIndex, spacing + sizeAdjustment);
            case EFormationType.Circle:
                return CalculateCirclePosition(positionIndex, spacing + sizeAdjustment);
            case EFormationType.Cross:
                return CalculateCrossPosition(positionIndex, spacing + sizeAdjustment);
            case EFormationType.X:
                return CalculateXPosition(positionIndex, spacing + sizeAdjustment);
            default:
                return CalculateLinearHorizontalPosition(positionIndex, spacing + sizeAdjustment);
        }
    }

    private static Vector3 CalculateLinearHorizontalPosition(int positionIndex, float spacing)
    {
        if (positionIndex == 0)
            return new Vector3(0, 0, 0);

        int side = (positionIndex % 2 == 1) ? -1 : 1;
        int distance = (positionIndex + 1) / 2;
        float xOffset = side * distance * spacing;

        return new Vector3(xOffset, 0, 0);
    }

    private static Vector3 CalculateLinearVerticalPosition(int positionIndex, float spacing)
    {
        if (positionIndex == 0)
            return new Vector3(0, 0, 0);

        int side = (positionIndex % 2 == 1) ? -1 : 1;
        int distance = (positionIndex + 1) / 2;
        float yOffset = side * distance * spacing;

        return new Vector3(0, yOffset, 0);
    }

    private Vector3 CalculateLinearDepthPosition(int positionIndex, float spacing)
    {
        if (m_myFleet == null)
            return new Vector3(0, 0, positionIndex * spacing);

        List<SpaceShip> sortedShips = new List<SpaceShip>(m_myFleet.m_ships);
        sortedShips.Sort((a, b) => a.m_shipInfo.positionIndex.CompareTo(b.m_shipInfo.positionIndex));

        float accumulatedZ = 0f;

        foreach (SpaceShip ship in sortedShips)
        {
            if (ship == null) continue;
            if (ship.m_shipInfo.positionIndex >= positionIndex) break;

            Bounds shipBounds = ship.CalculateShipBounds();
            accumulatedZ -= shipBounds.size.z + spacing;
        }

        return new Vector3(0, 0, accumulatedZ);
    }

    private static Vector3 CalculateGridPosition(int positionIndex, float spacing, int shipsPerRow = 3)
    {
        int row = positionIndex / shipsPerRow;
        int col = positionIndex % shipsPerRow;

        return new Vector3(
            col * spacing - (shipsPerRow - 1) * spacing * 0.5f,
            row * spacing - row * spacing * 0.5f,
            0
        );
    }

    private static Vector3 CalculateCirclePosition(int positionIndex, float spacing)
    {
        float radius = spacing * 1f;
        float angle = positionIndex * (360f / 8f);
        float radians = angle * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius,
            0
        );
    }

    private static Vector3 CalculateCrossPosition(int positionIndex, float spacing)
    {
        switch (positionIndex)
        {
            case 0: return new Vector3(0, 0, 0);
            case 1: return new Vector3(-spacing, 0, 0);
            case 2: return new Vector3(spacing, 0, 0);
            case 3: return new Vector3(0, spacing, 0);
            case 4: return new Vector3(0, -spacing, 0);
            case 5: return new Vector3(-spacing * 2f, 0, 0);
            case 6: return new Vector3(spacing * 2f, 0, 0);
            case 7: return new Vector3(0, spacing * 2f, 0);
            case 8: return new Vector3(0, -spacing * 2f, 0);
            default:
                int extraIndex = positionIndex - 9;
                int arm = extraIndex / 2;
                bool isHorizontal = (arm % 2) == 0;
                bool isPositive = (extraIndex % 2) == 0;
                float distance = (arm / 2 + 3) * spacing;
                if (isHorizontal)
                    return new Vector3(isPositive ? distance : -distance, 0, 0);
                else
                    return new Vector3(0, isPositive ? distance : -distance, 0);
        }
    }

    private static Vector3 CalculateXPosition(int positionIndex, float spacing)
    {
        switch (positionIndex)
        {
            case 0: return new Vector3(0, 0, 0);
            case 1: return new Vector3(-spacing, spacing, 0);
            case 2: return new Vector3(spacing, spacing, 0);
            case 3: return new Vector3(-spacing, -spacing, 0);
            case 4: return new Vector3(spacing, -spacing, 0);
            case 5: return new Vector3(-spacing * 2f, spacing * 2f, 0);
            case 6: return new Vector3(spacing * 2f, spacing * 2f, 0);
            case 7: return new Vector3(-spacing * 2f, -spacing * 2f, 0);
            case 8: return new Vector3(spacing * 2f, -spacing * 2f, 0);
            default:
                int extraIndex = positionIndex - 9;
                int layer = (extraIndex / 4) + 3;
                int corner = extraIndex % 4;
                float distance = layer * spacing;
                switch (corner)
                {
                    case 0: return new Vector3(-distance, distance, 0);
                    case 1: return new Vector3(distance, distance, 0);
                    case 2: return new Vector3(-distance, -distance, 0);
                    case 3: return new Vector3(distance, -distance, 0);
                    default: return new Vector3(0, 0, 0);
                }
        }
    }

    public void MoveToFormationPosition(EFormationType formationType)
    {
        m_targetPosition = CalculateShipPosition(formationType);
        if (m_isMovingToFormation == false)
        {
            m_isMovingToFormation = true;
            StartCoroutine(AutonomousFormationMovement());
        }
    }

    private IEnumerator AutonomousFormationMovement()
    {
        while (m_isMovingToFormation == true)
        {
            Vector3 currentPos = transform.localPosition;
            float distanceToTarget = Vector3.Distance(currentPos, m_targetPosition);
            if (distanceToTarget < 0.1f)
            {
                transform.localPosition = m_targetPosition;
                m_isMovingToFormation = false;
                yield break;
            }

            Vector3 directionToTarget = (m_targetPosition - currentPos).normalized;
            Vector3 directionToAvoidance = CalculateDirectionToAvoidance(currentPos, directionToTarget);
            //Vector3 finalDirection = (directionToTarget + avoidanceVector).normalized;
             Vector3 finalDirection;
             if (directionToAvoidance.magnitude > 0.1f)
                 finalDirection = directionToAvoidance.normalized;
             else
                 finalDirection = directionToTarget;

            float moveSpeed = m_spaceShipStatsCur.engineSpeed;
            Vector3 newPosition = currentPos + finalDirection * moveSpeed * Time.deltaTime;
            transform.localPosition = newPosition;

            yield return null;
        }
    }

    private Vector3 CalculateDirectionToAvoidance(Vector3 currentPos, Vector3 moveDirection)
    {
        Vector3 directionToAvoidance = Vector3.zero;

        Bounds shipBounds = CalculateShipBounds();
        Vector3 shipSize = shipBounds.size;
        float maxDimension = Mathf.Max(shipSize.x, shipSize.y, shipSize.z);
        float castRadius = maxDimension * 0.5f;

        float distanceToTarget = Vector3.Distance(currentPos, m_targetPosition);
        float checkDistance = Mathf.Min(distanceToTarget, m_detectionDistanceLimit);

        //int layerMask = ~(1 << DisplayFleet.LAYER_DISPLAY_FLEET);

        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position
            , castRadius
            , moveDirection
            , checkDistance
            //, layerMask
        );

        // HashSet for fast O(1) duplicate check with Contains()
        HashSet<SpaceShip> obstacleShips = new HashSet<SpaceShip>();
        // List for sequential storage and iteration only
        List<RaycastHit> obstacleHits = new List<RaycastHit>();

        foreach (RaycastHit hit in hits)
        {
            SpaceShip otherShip = hit.collider.GetComponentInParent<SpaceShip>();
            if (otherShip != null && otherShip != this)
            {
                if (obstacleShips.Contains(otherShip)) continue;
                obstacleShips.Add(otherShip);
            }
            else if (otherShip == null)
            {
                obstacleHits.Add(hit);
            }
        }

        foreach (SpaceShip otherShip in obstacleShips)
        {
            //Debug.DrawLine(transform.position, otherShip.transform.position, Color.red, 0.1f);

            Vector3 toOther = otherShip.transform.position - transform.position;
            float distance = toOther.magnitude;
            float weight = 1f / (distance + 0.1f);

            if (otherShip.m_myFleet == m_myFleet)
            {
                Vector3 toOtherDir = (otherShip.transform.localPosition - currentPos).normalized;
                Vector3 lateralDir = Vector3.Cross(toOtherDir, m_myFleet.transform.forward).normalized;

                directionToAvoidance += lateralDir * weight * 3f;

                // float collisionDistance = castRadius * 1.2f;
                // if (distance < collisionDistance)
                // {
                //     Vector3 awayFromOther = -toOtherDir;
                //     avoidance += awayFromOther * weight * 4f;
                // }
                Vector3 awayFromOther = -toOtherDir;
                directionToAvoidance += awayFromOther * weight * 1f;

            }
            else
            {
                Vector3 awayFromEnemy = currentPos - otherShip.transform.localPosition;
                directionToAvoidance += awayFromEnemy.normalized * weight * 10f;
                //Debug.DrawLine(transform.position, otherShip.transform.position, Color.magenta, 0.1f);
            }
        }

        foreach (RaycastHit hit in obstacleHits)
        {
            Vector3 awayFromObstacle = currentPos - hit.collider.transform.position;
            float weight = 1f / (hit.distance + 0.1f);
            directionToAvoidance += awayFromObstacle.normalized * weight * 1.5f;
        }

        //if (avoidance.magnitude > 0.1f)
        //Debug.DrawRay(transform.position, avoidance.normalized * 2f, Color.blue, 0.1f);

        return directionToAvoidance;
    }

    public Bounds CalculateShipBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);

        if (renderers.Length == 0)
            bounds.size = Vector3.one * 2f;

        return bounds;
    }

    // 모듈 교체 후 ModuleVisual 갱신 (효율적으로)
    public void RefreshSelectedModuleVisuals()
    {
        // 1. 파괴된 모듈의 selectedModuleVisual 만 리스트에서 제거
        m_selectedModuleVisuals.RemoveAll(h => h == null || h.ModuleBase == null);

        // 2. Body 모듈 확인 및 추가
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body == null) continue;

            // Body에 SelectedModuleVisual가 없으면 추가 (새로 생성된 Body)
            if (body.GetComponent<SelectedModuleVisual>() == null)
                SetupSelectedModuleVisual(body);

            // 3. 각 슬롯의 모듈 확인
            foreach (ModuleSlot slot in body.m_moduleSlots)
            {
                if (slot == null || slot.transform.childCount == 0) continue;

                ModuleBase module = slot.GetComponentInChildren<ModuleBase>();

                // 이미 selectedModuleVisual 가 있는지 확인 (SelectedModuleVisual 컴포넌트로 체크)
                if (module != null && module.GetComponent<SelectedModuleVisual>() == null)
                {
                    // 새로 생성된 모듈이므로 selectedModuleVisual 추가
                    SetupSelectedModuleVisual(module);
                }
            }
        }
    }

    // module unlock (외부 호출용 - 모듈 해금 UI에서 사용)
    public void UnlockModule(int bodyIndex, EModuleType moduleType, EModuleSubType moduleSubType, int slotIndex)
    {
        ModuleBody body = FindModuleBodyByIndex(bodyIndex);
        if (body == null)
        {
            Debug.LogError($"Body not found: shipId={m_shipInfo.id}, bodyIndex={bodyIndex}");
            return;
        }

        int moduleLevel = 1; // 해금 시 기본 레벨 1

        // 슬롯에서 placeholder를 실제 모듈로 교체
        bool success = body.ReplaceModuleInSlot(moduleType, moduleSubType, moduleLevel, slotIndex);
        if (!success)
        {
            Debug.LogError($"Failed to unlock module: moduleType={moduleType}, slotIndex={slotIndex}");
            return;
        }

        // 함선 스탯 업데이트
        UpdateShipStats();

        // Outline 갱신 (새로 생성된 모듈들을 포함하도록)
        if (m_shipOutline != null)
            m_shipOutline.RefreshOutline();

        // 모듈 selectedModuleVisual 갱신 (새로 생성된 모듈들을 포함하도록)
        RefreshSelectedModuleVisuals();

        Debug.Log($"Module unlocked: Ship={m_shipInfo.id}, Body={bodyIndex}, Slot={slotIndex}, Type={moduleType}");
    }

    // module 교체 (외부 호출용 - 모듈 교체 UI에서 사용)
    public void ChangeModule(int bodyIndex, EModuleType moduleTypeCurrent, EModuleType moduleTypeNew, EModuleSubType moduleSubTypeNew, int slotIndex)
    {
        ModuleBase oldModule = FindModule(bodyIndex, moduleTypeCurrent, slotIndex);
        if (oldModule == null)
        {
            Debug.LogError($"Old module not found: shipId={m_shipInfo.id}, bodyIndex={bodyIndex}, moduleTypeCurrent={moduleTypeCurrent}, slotIndex={slotIndex}");
            return;
        }
        int moduleLevel = oldModule.GetModuleLevel();

        if (moduleTypeNew == EModuleType.Body)
        {
            // Body 교체 처리
            ChangeModuleBody(bodyIndex, moduleTypeNew, moduleSubTypeNew, moduleLevel);
        }
        else
        {
            // 일반 모듈 교체
            ModuleBody body = FindModuleBodyByIndex(bodyIndex);
            if (body == null) return;
            bool success = body.ReplaceModuleInSlot(moduleTypeNew, moduleSubTypeNew, moduleLevel, slotIndex);
            if (success == false)
            {
                Debug.LogError($"Failed to replace module: moduleTypeNew={moduleTypeNew}");
                return;
            }
        }

        // Outline 갱신 (새로 생성된 모듈들을 포함하도록)
        if (m_shipOutline != null)
            m_shipOutline.RefreshOutline();

        // 모듈 selectedModuleVisual 갱신 (새로 생성된 모듈들을 포함하도록)
        RefreshSelectedModuleVisuals();
    }
    private void ChangeModuleBody(int bodyIndex, EModuleType moduleTypeNew, EModuleSubType moduleSubTypeNew, int moduleLevel)
    {
        ModuleBody oldBody = FindModuleBodyByIndex(bodyIndex);
        if (oldBody == null) return;
        
        ModuleBodyInfo newBodyInfo = new ModuleBodyInfo
        {
            moduleType = moduleTypeNew,
            moduleSubType = moduleSubTypeNew,
            moduleLevel = moduleLevel,
            bodyIndex = bodyIndex,
            engines = oldBody.m_moduleBodyInfo.engines,   // 새 배열 생성,
            beams = oldBody.m_moduleBodyInfo.beams,
            missiles = oldBody.m_moduleBodyInfo.missiles,
            hangers = oldBody.m_moduleBodyInfo.hangers
        };

        ReplaceBodyWhilePreservingModules(oldBody, newBodyInfo);
    }
    // Body 교체 시 기존 모듈을 보존하는 메서드
    private void ReplaceBodyWhilePreservingModules(ModuleBody oldBody, ModuleBodyInfo newBodyInfo)
    {
        // 1. 기존 body의 모든 모듈 수집 (ModulePlaceholder 제외)
        List<ModuleBase> savedModules = new List<ModuleBase>();
        foreach (var slot in oldBody.m_moduleSlots)
        {
            ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
            if (module != null && (module is ModulePlaceholder) == false)
            {
                // 슬롯에서 모듈 분리 (파괴 방지)
                module.transform.SetParent(null);
                module.gameObject.SetActive(false); // 임시로 비활성화
                savedModules.Add(module);
            }
        }

        // 2. 삭제 전 이벤트 발행 (oldBody 아직 유효)
        EventManager.TriggerModuleReplaced(oldBody, null);

        // 3. 기존 body 제거
        m_moduleBodys.Remove(oldBody);
        DestroyImmediate(oldBody.gameObject);

        // 4. 새 body 생성 (저장된 모듈 재배치)
        ModuleBody newBody = InitSpaceShipBody(newBodyInfo, savedModules);

        // 5. 새 body 이벤트 발행
        if (newBody != null)
            EventManager.TriggerModuleReplaced(null, newBody);
    }

    private void OnDrawGizmos()
    {
        Bounds shipBounds = CalculateShipBounds();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(shipBounds.center, shipBounds.size);

        if (m_targetPosition == Vector3.zero) return;

        Vector3 shipSize = shipBounds.size;
        float maxDimension = Mathf.Max(shipSize.x, shipSize.y, shipSize.z);
        float castRadius = maxDimension * 0.5f;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(m_targetPosition, castRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, m_targetPosition);
    }
    #endregion
}
