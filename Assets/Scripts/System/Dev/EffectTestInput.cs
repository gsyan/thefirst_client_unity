// [DEV] 숫자키 1~5로 폭발 이펙트 테스트 — 적 기함 위치에서 생성, 한 사이클 후 자동 소멸
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class EffectTestInput : MonoBehaviour
{
    [SerializeField] private GameObject[] m_explosionPrefabs; // 1~5키 순서로 할당

    private static readonly Key[] s_keys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
    };

    private void Update()
    {
        if (Keyboard.current == null) return;
        for (int i = 0; i < s_keys.Length; i++)
        {
            if (Keyboard.current[s_keys[i]].wasPressedThisFrame == false) continue;
            if (i >= m_explosionPrefabs.Length || m_explosionPrefabs[i] == null) break;

            SpawnAtEnemyFlagship(m_explosionPrefabs[i]);
            break;
        }
    }

    private void SpawnAtEnemyFlagship(GameObject prefab)
    {
        if (ObjectManager.Instance == null) return;
        SpaceFleet fleet = ObjectManager.Instance.m_myFleet;
        if (fleet == null) return;

        SpaceShip flagship = fleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == 0);
        if (flagship == null && fleet.m_ships.Count > 0)
            flagship = fleet.m_ships[0];
        if (flagship == null) return;

        Vector3 spawnPos = flagship.transform.position + flagship.transform.forward * 30f;
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
        StartCoroutine(DestroyAfterOneCycle(go));
    }

    // 루프 비활성화 후 자연 소멸까지 대기 → Destroy
    private IEnumerator DestroyAfterOneCycle(GameObject go)
    {
        if (go.TryGetComponent(out ParticleSystem root) == false)
            root = go.GetComponentInChildren<ParticleSystem>();
        if (root == null) { Destroy(go, 5f); yield break; }

        // 모든 자식 포함 루프 off → 한 번만 재생
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.loop = false;
        }

        yield return new WaitUntil(() => go == null || root.IsAlive(true) == false);
        if (go != null) Destroy(go);
    }
}
