// [DEV] 테스트 씬 — bodyPrefab 이름에서 EModuleSubType 파싱 후 InitializeSpaceShip으로 함대 구성
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ETestInputMode
{
    effect,
    fleet,
}

[System.Serializable]
public class TestSceneShipInfo
{
    public GameObject bodyPrefab;
}

public class TestScene : MonoBehaviour
{
    [SerializeField] private ETestInputMode m_mode = ETestInputMode.effect;

    [Header("Fleet")]
    [SerializeField] TestSceneShipInfo[] m_shipInfos;
    [SerializeField] private EFormationType m_spawnFormation = EFormationType.formation_type_linear_horizontal;

    [Header("Effect")]
    [SerializeField] private GameObject[] m_explosionPrefabs; // 1~9,0키 순서
    

    private static readonly Key[] s_keys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
    };

    private void Start()
    {
        RespawnMyFleet();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        HandleModeToggle();

        if (m_mode == ETestInputMode.effect)
            HandleEffectInput();
        else
            HandleFleetInput();
    }

    private void HandleModeToggle()
    {
        if (Keyboard.current[Key.Tab].wasPressedThisFrame == false) return;
        m_mode = m_mode == ETestInputMode.effect ? ETestInputMode.fleet : ETestInputMode.effect;
        Debug.Log($"[TestScene] 모드 전환: {m_mode}");
    }

    // effect 모드 — 숫자키로 폭발 이펙트 스폰
    private void HandleEffectInput()
    {
        for (int i = 0; i < s_keys.Length; i++)
        {
            if (Keyboard.current[s_keys[i]].wasPressedThisFrame == false) continue;
            if (i >= m_explosionPrefabs.Length || m_explosionPrefabs[i] == null) break;
            SpawnEffectAtFlagship(m_explosionPrefabs[i]);
            break;
        }
    }

    // fleet 모드 — F5: 함대 재스폰
    private void HandleFleetInput()
    {
        if (Keyboard.current[Key.F5].wasPressedThisFrame == true)
            RespawnMyFleet();
    }

    // 내 함대 전체 재스폰
    public void RespawnMyFleet()
    {
        if (ObjectManager.Instance.m_myFleet != null)
        {
            Destroy(ObjectManager.Instance.m_myFleet.gameObject);
            ObjectManager.Instance.m_myFleet = null;
        }

        if (m_shipInfos == null || m_shipInfos.Length == 0) return;

        GameObject fleetGo = new GameObject("TestFleet_My");
        SpaceFleet fleet = fleetGo.AddComponent<SpaceFleet>();
        ObjectManager.Instance.m_myFleet = fleet;

        for (int i = 0; i < m_shipInfos.Length; i++)
            SpawnTestShip(fleet, m_shipInfos[i], i);

        fleet.UpdateShipFormation(m_spawnFormation, smooth: false);
        CameraController.Instance.SetTargetOfCameraController(fleet.transform);

        // 기함을 초기 선택 상태로 설정 (줌 범위 적용 및 UI 초기화)
        SpaceShip flagship = fleet.GetFlagship();
        if (flagship != null)
            EventManager.Trigger_SpaceShipSelected(flagship);

        Debug.Log($"[TestScene] 함대 재스폰 완료 — {fleet.m_ships.Count}척, 진형: {m_spawnFormation}");
    }

    // bodyPrefab 이름 → EModuleSubType 파싱 후 ShipInfo 구성 → InitializeSpaceShip 호출
    private void SpawnTestShip(SpaceFleet fleet, TestSceneShipInfo info, int positionIndex)
    {
        if (info.bodyPrefab == null) return;

        if (System.Enum.TryParse(info.bodyPrefab.name, out EModuleSubType subType) == false)
        {
            Debug.LogError($"[TestScene] bodyPrefab 이름 파싱 실패: {info.bodyPrefab.name}");
            return;
        }

        ShipInfo shipInfo = new ShipInfo
        {
            id = positionIndex,
            shipName = $"TestShip_{positionIndex}",
            positionIndex = positionIndex,
            bodies = new List<ModuleBodyInfo>
            {
                new ModuleBodyInfo
                {
                    bodyIndex = 0,
                    moduleType = EModuleType.body,
                    moduleSubType = subType,
                    moduleLevel = 1,
                }
            }
        };

        GameObject shipGo = new GameObject(shipInfo.shipName);
        SpaceShip ship = shipGo.AddComponent<SpaceShip>();
        ship.InitializeSpaceShip(fleet, shipInfo);
        fleet.AddShip(ship, placeInFormation: false);
    }

    // effect 모드용 — 기함 앞에서 이펙트 스폰
    private void SpawnEffectAtFlagship(GameObject prefab)
    {
        if (ObjectManager.Instance == null) return;
        SpaceFleet fleet = ObjectManager.Instance.m_myFleet;
        if (fleet == null) return;

        SpaceShip flagship = fleet.GetFlagship();
        if (flagship == null) return;

        Vector3 spawnPos = flagship.transform.position + flagship.transform.forward * 30f;
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
        StartCoroutine(DestroyAfterOneCycle(go));
    }

    private IEnumerator DestroyAfterOneCycle(GameObject go)
    {
        if (go.TryGetComponent(out ParticleSystem root) == false)
            root = go.GetComponentInChildren<ParticleSystem>();
        if (root == null) { Destroy(go, 5f); yield break; }

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.loop = false;
        }

        yield return new WaitUntil(() => go == null || root.IsAlive(true) == false);
        if (go != null) Destroy(go);
    }
}
