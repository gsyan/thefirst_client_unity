//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

// 함선 통계 구조체
[System.Serializable]
public struct SpaceShipStats
{
    public float totalHealth;
    public float totalMovementSpeed;
    public float totalRotationSpeed;
    public float totalCargoCapacity;
    public float totalAttackPower;
    public int totalWeapons;
    public int totalEngines;

    public override string ToString()
    {
        return $"Health: {totalHealth:F1}, Speed: {totalMovementSpeed:F1}, " +
                $"Rotation: {totalRotationSpeed:F1}, Cargo: {totalCargoCapacity:F1}, " +
                $"AttackPower: {totalAttackPower:F1}, " +
                $"Weapons: {totalWeapons}, Engines: {totalEngines}";
    }
}

public class SpaceShip : MonoBehaviour
{
    [SerializeField] public ShipInfo m_shipInfo;
    [SerializeField] public List<ModuleBody> m_moduleBodyList = new List<ModuleBody>();
    [SerializeField] public float m_health;
    [SerializeField] public SpaceShip m_targetShip;
    [SerializeField] public SpaceShipStats m_spaceShipStatsOrg;
    [SerializeField] public SpaceShipStats m_spaceShipStatsCur;

    public SpaceFleet m_myFleet;
    public EShipState m_shipState;

    private ModuleGaugeDisplay m_gaugeDisplay;
    public OutlineScanner m_outlineScanner;

    virtual protected void Start()
    {
        InitializeGaugeDisplay();
    }

    private void InitializeGaugeDisplay()
    {
        m_gaugeDisplay = GetComponent<ModuleGaugeDisplay>();
        if (m_gaugeDisplay == null)
            m_gaugeDisplay = gameObject.AddComponent<ModuleGaugeDisplay>();
    }

    public void InitializeSpaceShip(SpaceFleet fleet, ShipInfo shipInfo)
    {
        m_myFleet = fleet;
        ApplyFleetStateToShip();

        m_shipInfo = shipInfo;
        if (shipInfo.bodies == null || shipInfo.bodies.Length == 0) return;
        foreach (ModuleBodyInfo bodyInfo in shipInfo.bodies)
            InitSpaceShipBody(bodyInfo);

        CalculateTotalHealth();
        m_spaceShipStatsOrg = GetTotalStats();
        m_spaceShipStatsCur = GetTotalStats();

        SetupModuleHighlighting();
        
        // 지금은 바디가 오직 하나...
        m_outlineScanner = m_moduleBodyList[0].GetComponent<OutlineScanner>();
        m_outlineScanner.Check();
        m_outlineScanner.BuildAdjacency();
        
    }

    public int GetAverageModuleLevel()
    {
        if (m_shipInfo.bodies == null || m_shipInfo.bodies.Length == 0) return 0;
        int totalLevel = 0;
        int moduleCount = 0;

        foreach (ModuleBodyInfo body in m_shipInfo.bodies)
        {
            totalLevel += body.moduleLevel;
            moduleCount++;

            if (body.weapons != null)
            {
                foreach (ModuleWeaponInfo weapon in body.weapons)
                {
                    totalLevel += weapon.moduleLevel;
                    moduleCount++;
                }
            }

            if (body.engines != null)
            {
                foreach (ModuleEngineInfo engine in body.engines)
                {
                    totalLevel += engine.moduleLevel;
                    moduleCount++;
                }
            }
        }

        if (moduleCount == 0) return 0;
        return totalLevel / moduleCount;
    }

    private void InitSpaceShipBody(ModuleBodyInfo bodyInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(bodyInfo.ModuleType.ToString(), bodyInfo.ModuleSubType.ToString(), bodyInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitSpaceShipBody: Cannot find module prefab - Level: {bodyInfo.moduleLevel}");
            return;
        }

        GameObject bodyObj = Instantiate(modulePrefab, transform.position, transform.rotation);
        bodyObj.transform.SetParent(transform);

        ModuleBody moduleBody = bodyObj.GetComponent<ModuleBody>();
        if (moduleBody == null)
            moduleBody = bodyObj.AddComponent<ModuleBody>();

        moduleBody.InitializeModuleBody(bodyInfo);
        m_moduleBodyList.Add(moduleBody);
    }

    // 전체 함선 체력 계산
    private void CalculateTotalHealth()
    {
        float totalHealth = 0f;
        foreach (ModuleBody body in m_moduleBodyList)
        {
            if (body != null)
            {
                totalHealth += body.m_health;
            }
        }
        m_health = totalHealth;
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
        
        foreach (ModuleBody body in m_moduleBodyList)
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

            foreach (ModuleBody body in m_moduleBodyList)
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
        RecalculateHealth();

        if (m_health <= 0.0f)
        {
            Debug.Log($"{gameObject.name} SpaceShip destroyed!");
            OnSpaceShipDestroyed();
        }
    }

    // 함선이 살아있는지 확인
    public bool IsAlive()
    {
        return m_health > 0 && HasAliveBodies();
    }

    // 살아있는 바디가 있는지 확인
    private bool HasAliveBodies()
    {
        foreach (ModuleBody body in m_moduleBodyList)
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
        foreach (ModuleBody body in m_moduleBodyList)
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

    public void RecalculateHealth()
    {
        float totalHealth = 0f;
        foreach (ModuleBody body in m_moduleBodyList)
        {
            if (body != null)
                totalHealth += Mathf.Max(0f, body.m_health);
        }
        m_health = totalHealth;
        m_spaceShipStatsCur = GetTotalStats();
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
                DeveloperConsole.ExecuteCommandStatic("AddMoney 100");
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
        foreach (ModuleBody body in m_moduleBodyList)
        {
            if (body != null && body.m_moduleBodyInfo.bodyIndex == bodyIndex)
            {
                return body;
            }
        }
        return null;
    }

    public SpaceShipStats GetTotalStats()
    {
        SpaceShipStats stats = new SpaceShipStats();

        foreach (ModuleBody body in m_moduleBodyList)
        {
            if (body != null && body.m_health > 0)
            {
                stats.totalHealth += body.m_health;
                stats.totalMovementSpeed += body.GetTotalMovementSpeed();
                stats.totalRotationSpeed += body.GetTotalRotationSpeed();
                stats.totalCargoCapacity += body.GetTotalCargoCapacity();

                foreach (ModuleWeapon weapon in body.m_weapons)
                {
                    if (weapon != null && weapon.m_health > 0)
                        stats.totalAttackPower += weapon.m_attackPower;
                }

                stats.totalWeapons += body.m_weapons.Count;
                stats.totalEngines += body.m_engines.Count;
            }
        }

        return stats;
    }


    #region Display migration ============================================================

    [Header("Visualization")]
    public Material highlightMaterial;
    public Color selectedColor = Color.yellow;
    public Color hoverColor = Color.cyan;

    // Private fields
    private List<ModuleHighlight> m_moduleHighlights = new List<ModuleHighlight>();
    private ModuleBase m_currentSelectedModule = null;

    private void SetupModuleHighlighting()
    {
        // Setup highlighting for parts bodies
        foreach (ModuleBody body in m_moduleBodyList)
        {
            if (body != null)
            {
                SetupModuleHighlight(body);

                // Setup highlighting for weapons
                foreach (ModuleWeapon weapon in body.m_weapons)
                {
                    if (weapon != null)
                        SetupModuleHighlight(weapon);
                }

                // Setup highlighting for engines
                foreach (ModuleEngine engine in body.m_engines)
                {
                    if (engine != null)
                        SetupModuleHighlight(engine);
                }
            }
        }
    }

    private void SetupModuleHighlight(ModuleBase partsBase)
    {
        // Add highlighting component
        ModuleHighlight highlight = partsBase.gameObject.AddComponent<ModuleHighlight>();
        highlight.InitializeModuleHighlight(this, partsBase);
        m_moduleHighlights.Add(highlight);
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

    public void OnClicked(Vector3 hitPoint, Collider hitCollider = null)
    {
        ModuleBase clickedParts = null;
        if (hitCollider != null)
        {
            clickedParts = hitCollider.GetComponent<ModuleBase>();
            if (clickedParts == null)
                clickedParts = hitCollider.GetComponentInParent<ModuleBase>();
        }

        if (clickedParts != null)
            SelectParts(clickedParts);        
    }

    public void SelectParts(ModuleBase partsBase)
    {
        if (m_myFleet == null) return;
        //if (m_myFleet.m_enableModuleSelection == false) return;
        
        m_myFleet.ClearAllSelections();
            

        m_currentSelectedModule = partsBase;
        UpdateHighlighting();

        if (m_myFleet != null)
            m_myFleet.OnModuleClicked(this, partsBase);
    }

    public void ClearSelection()
    {
        m_currentSelectedModule = null;
        UpdateHighlighting();
    }

    private void UpdateHighlighting()
    {
        foreach (var highlight in m_moduleHighlights)
        {
            if (highlight != null)
            {
                bool isSelected = (highlight.ModuleBase == m_currentSelectedModule);
                highlight.SetHighlighted(isSelected);
            }
        }
    }

    public void OnModuleHover(ModuleBase partsBase, bool isHovering)
    {
        //if (m_myFleet.m_enableModuleSelection == false) return;
        // Find the highlight component for this parts
        var highlight = m_moduleHighlights.Find(h => h != null && h.ModuleBase == partsBase);
        if (highlight != null)
            highlight.SetHovered(isHovering);
    }

    #endregion


    #region Autonomous Formation Movement
    private Vector3 m_targetPosition;
    private bool m_isMovingToFormation = false;
    private float m_detectionDistanceLimit = 6f;

    public Vector3 CalculateShipPosition(EFormationType formationType, float spacing = 0f)
    {
        var gameSettings = DataManager.Instance?.m_dataTableConfig?.gameSettings;
        int positionIndex = (int)m_shipInfo.positionIndex;

        switch (formationType)
        {
            case EFormationType.LinearHorizontal:
                float linearHSpacing = spacing > 0f ? spacing : (gameSettings?.linearFormationSpacing ?? 2f);
                return CalculateLinearHorizontalPosition(positionIndex, linearHSpacing);
            case EFormationType.LinearVertical:
                float linearVSpacing = spacing > 0f ? spacing : (gameSettings?.linearFormationSpacing ?? 2f);
                return CalculateLinearVerticalPosition(positionIndex, linearVSpacing);
            case EFormationType.LinearDepth:
                float linearDSpacing = spacing > 0f ? spacing : (gameSettings?.linearFormationSpacing ?? 2f);
                return CalculateLinearDepthPosition(positionIndex, linearDSpacing);
            case EFormationType.Grid:
                float gridSpacing = spacing > 0f ? spacing : (gameSettings?.gridFormationSpacing ?? 5f);
                return CalculateGridPosition(positionIndex, gridSpacing);
            case EFormationType.Circle:
                float circleSpacing = spacing > 0f ? spacing : gameSettings?.circleFormationSpacing ?? 8f;
                return CalculateCirclePosition(positionIndex, circleSpacing);
            case EFormationType.Cross:
                float crossSpacing = spacing > 0f ? spacing : (gameSettings?.diamondFormationSpacing ?? 6f);
                return CalculateCrossPosition(positionIndex, crossSpacing);
            case EFormationType.X:
                float xSpacing = spacing > 0f ? spacing : (gameSettings?.wedgeFormationSpacing ?? 7f);
                return CalculateXPosition(positionIndex, xSpacing);
            default:
                float defaultSpacing = spacing > 0f ? spacing : (gameSettings?.linearFormationSpacing ?? 2f);
                return CalculateLinearHorizontalPosition(positionIndex, defaultSpacing);
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

            float moveSpeed = m_spaceShipStatsCur.totalMovementSpeed;
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
