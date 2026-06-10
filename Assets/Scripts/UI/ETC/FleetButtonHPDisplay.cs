// FleetButton 자식 HP 바 — 함대 탭이 닫힌 상태에서 함선 체력 요약 표시
// ShipSelector 와 동일한 anchorMax.x 방식으로 비율 표현
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FleetButtonHPDisplay : MonoBehaviour
{
    [SerializeField] private float m_lerpDuration = 0.4f;

    // HP 루트(직접 자식)의 두 번째 자식이 fill Image — Awake 에서 자동 수집
    [SerializeField] private Transform m_hpRoot;
    private Transform[] m_hpBarRoots;
    private Image[]     m_hpBarFills;
    private Coroutine[] m_lerpCoroutines;

    private void Awake()
    {
        int count = m_hpRoot.transform.childCount;
        m_hpBarRoots     = new Transform[count];
        m_hpBarFills     = new Image[count];
        m_lerpCoroutines = new Coroutine[count];

        for (int i = 0; i < count; i++)
        {
            Transform root = m_hpRoot.transform.GetChild(i);
            m_hpBarRoots[i] = root;
            // 두 번째 자식(index 1)이 fill Image
            if (root.childCount > 1)
                m_hpBarFills[i] = root.GetChild(1).GetComponent<Image>();
        }

        EventManager.Subscribe_FleetUpdateHP(RefreshHPBars);
        EventManager.Subscribe_FleetShipCountChanged(RefreshHPBars);
    }

    private void OnEnable()
    {
        RefreshHPBars();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_FleetUpdateHP(RefreshHPBars);
        EventManager.Unsubscribe_FleetShipCountChanged(RefreshHPBars);
    }

    private void RefreshHPBars()
    {
        if (m_hpBarRoots == null) return;
        if (ObjectManager.Instance == null) return;

        SpaceFleet fleet = ObjectManager.Instance.m_myFleet;
        int shipCount = fleet != null ? fleet.m_ships.Count : 0;

        for (int i = 0; i < m_hpBarRoots.Length; i++)
        {
            if (m_hpBarRoots[i] == null) continue;

            if (i < shipCount)
            {
                m_hpBarRoots[i].gameObject.SetActive(true);
                if (m_hpBarFills[i] == null) continue;

                SpaceShip ship = fleet.m_ships[i];
                float ratio = 0f;
                if (ship != null)
                {
                    float maxHp = ship.m_spaceShipStatsOrg.health;
                    ratio = maxHp > 0f ? Mathf.Clamp01(ship.m_spaceShipStatsCur.health / maxHp) : 0f;
                }
                SetRatio(i, ratio);
            }
            else
            {
                m_hpBarRoots[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetRatio(int index, float targetRatio)
    {
        if (m_lerpCoroutines[index] != null)
            StopCoroutine(m_lerpCoroutines[index]);

        if (!gameObject.activeInHierarchy)
        {
            ApplyRatio(m_hpBarFills[index], targetRatio);
            return;
        }

        m_lerpCoroutines[index] = StartCoroutine(Co_LerpRatio(index, targetRatio));
    }

    private IEnumerator Co_LerpRatio(int index, float targetRatio)
    {
        Image fill      = m_hpBarFills[index];
        float startRatio = fill.rectTransform.anchorMax.x;
        float elapsed    = 0f;

        while (elapsed < m_lerpDuration)
        {
            elapsed += Time.deltaTime;
            ApplyRatio(fill, Mathf.Lerp(startRatio, targetRatio, Mathf.Clamp01(elapsed / m_lerpDuration)));
            yield return null;
        }

        ApplyRatio(fill, targetRatio);
        m_lerpCoroutines[index] = null;
    }

    private void ApplyRatio(Image fill, float ratio)
    {
        RectTransform rt = fill.rectTransform;
        rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
