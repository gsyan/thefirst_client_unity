// 전술 토글 버튼 6종(0=빔, 1=미사일, 2=함재기, 3=수리, 4=실드, 5=요격체) 한 줄 — 전투 화면(UIBattleView)과 함대관리(UIPanelFleet)가 공용으로 사용
// 상태의 단일 소스는 SpaceFleet.m_fleetInfo.tacticOptions — 클릭은 SpaceFleet.TryToggleTactic, 표시는 TacticOptionsChanged 이벤트로 갱신
// 자식 Button은 순서대로 idx에 대응하고, 각 버튼 자식의 "UsingImage" 활성화로 on/off를 표현
// 전술 자원 소모 표시: m_floatingTextPrefab이 지정된 행(전투 화면)만 동작 — 1초마다 버튼별 소모 합계를 "-N" 텍스트로 버튼 위에서 떠올려 보여줌
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITacticsToggleRow : MonoBehaviour
{
    [Header("전술 자원 소모 표시 (비워두면 기능 꺼짐)")]
    [SerializeField] private TMP_Text m_floatingTextPrefab; // "-N" 표시용 TMP 텍스트 프리팹
    [SerializeField] private RectTransform m_floatingRoot;  // 떠오르는 텍스트의 부모 — 비워두면 이 행 오브젝트
    [SerializeField] private float m_floatDistance = 60f;   // 떠오르는 거리(캔버스 단위)
    [SerializeField] private float m_floatDuration = 0.8f;  // 떠올라 사라지기까지 시간(초, 게임 배속과 무관한 실시간)

    private const int k_tacticCount = 6;
    private const float k_spendTickSeconds = 1f; // 소모 합계를 묶어 표시하는 간격(게임 시간)
    private static readonly WaitForSeconds k_spendTickWait = new WaitForSeconds(k_spendTickSeconds);

    private Button[] m_buttons;
    private RectTransform[] m_buttonRects;
    private GameObject[] m_usingImages;
    private bool m_initialized;

    private readonly int[] m_pendingSpend = new int[k_tacticCount];
    private readonly Stack<TMP_Text> m_floatingPool = new Stack<TMP_Text>();
    private readonly List<TMP_Text> m_activeFloaters = new List<TMP_Text>();
    private readonly Vector3[] m_cornerBuffer = new Vector3[4];
    private Coroutine m_spendTickCoroutine;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        if (m_floatingTextPrefab == null) return;

        EnsureInitialized();
        ClearPendingSpend();
        if (m_spendTickCoroutine == null)
            m_spendTickCoroutine = StartCoroutine(Co_SpendTick());
    }

    private void OnDisable()
    {
        m_spendTickCoroutine = null; // 비활성화되면 코루틴은 자동 중단됨
        ReturnAllActiveFloaters();
    }

    private void OnDestroy()
    {
        if (m_initialized == true)
        {
            EventManager.Unsubscribe_TacticOptionsChanged(OnTacticOptionsChanged);
            EventManager.Unsubscribe_TacticCostSpent(OnTacticCostSpent);
        }
    }

    // 비활성 계층에서 생성된 직후 Refresh가 먼저 불릴 수 있어 지연 초기화를 보장
    private void EnsureInitialized()
    {
        if (m_initialized == true) return;
        m_initialized = true;

        EventManager.Subscribe_TacticOptionsChanged(OnTacticOptionsChanged);
        EventManager.Subscribe_TacticCostSpent(OnTacticCostSpent);

        m_buttons = GetComponentsInChildren<Button>(true);
        m_buttonRects = new RectTransform[m_buttons.Length];
        m_usingImages = new GameObject[m_buttons.Length];
        for (int i = 0; i < m_buttons.Length; i++)
        {
            int idx = i;
            m_buttons[idx].onClick.AddListener(() => OnButtonClicked(idx));
            m_buttonRects[idx] = m_buttons[idx].GetComponent<RectTransform>();

            Transform usingImage = m_buttons[idx].transform.Find("UsingImage");
            m_usingImages[idx] = usingImage != null ? usingImage.gameObject : null;
        }
    }

    private void OnButtonClicked(int idx)
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        myFleet.TryToggleTactic(idx);
    }

    private void OnTacticOptionsChanged(int options)
    {
        for (int i = 0; i < m_usingImages.Length; i++)
        {
            if (m_usingImages[i] == null) continue;
            m_usingImages[i].SetActive((options & (1 << i)) != 0);
        }
    }

    // 패널이 뜰 때마다 호출 — 이벤트는 옵션이 실제로 바뀔 때만 발행되므로 현재 함대 상태로 직접 동기화
    // 해당 모듈이 없어도 토글 버튼은 항상 눌리게 둠(나중에 모듈을 갖추기 전에도 켜/꺼 둘 수 있어야 함) — 모듈이 없으면 실제 효과만 안 날 뿐
    public void Refresh()
    {
        EnsureInitialized();

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        OnTacticOptionsChanged(myFleet.m_fleetInfo.tacticOptions);
    }

    // ── 전술 자원 소모 표시 ─────────────────────────────────

    // 지불 이벤트는 빔/미사일 발사마다 잦게 오므로 바로 표시하지 않고 버튼별로 합산만 해두고, Co_SpendTick이 1초마다 묶어서 표시
    private void OnTacticCostSpent(int tacticIndex, int amount)
    {
        if (m_floatingTextPrefab == null) return;
        if (isActiveAndEnabled == false) return;
        if (tacticIndex < 0 || tacticIndex >= k_tacticCount) return;

        m_pendingSpend[tacticIndex] += amount;
    }

    private void ClearPendingSpend()
    {
        for (int i = 0; i < k_tacticCount; i++)
            m_pendingSpend[i] = 0;
    }

    private IEnumerator Co_SpendTick()
    {
        while (true)
        {
            yield return k_spendTickWait;

            for (int i = 0; i < k_tacticCount; i++)
            {
                int amount = m_pendingSpend[i];
                if (amount <= 0) continue;

                m_pendingSpend[i] = 0;
                SpawnFloatingText(i, amount);
            }
        }
    }

    private void SpawnFloatingText(int tacticIndex, int amount)
    {
        if (tacticIndex >= m_buttonRects.Length) return;

        TMP_Text text = GetFloatingText();
        RectTransform textRect = text.rectTransform;

        // 버튼 윗변 중앙에서 시작 — 버튼이 레이아웃 그룹 안에 있어 월드 좌표로 위치를 잡음
        m_buttonRects[tacticIndex].GetWorldCorners(m_cornerBuffer);
        Vector3 topCenter = (m_cornerBuffer[1] + m_cornerBuffer[2]) * 0.5f;
        textRect.position = topCenter;

        text.text = "-" + amount;
        text.color = CommonUtility.PaletteColor("Text.Warning");
        text.gameObject.SetActive(true);
        textRect.SetAsLastSibling();

        m_activeFloaters.Add(text);
        StartCoroutine(Co_Float(text));
    }

    // 게임 배속(Time.timeScale)에 맞춰 빨라지면 읽을 수 없으므로 실시간(unscaledDeltaTime)으로 진행
    private IEnumerator Co_Float(TMP_Text text)
    {
        RectTransform textRect = text.rectTransform;
        Vector3 startLocalPosition = textRect.localPosition;
        Color baseColor = text.color;

        float elapsed = 0f;
        while (elapsed < m_floatDuration)
        {
            float progress = elapsed / m_floatDuration;
            textRect.localPosition = startLocalPosition + Vector3.up * (m_floatDistance * progress);

            Color fadedColor = baseColor;
            fadedColor.a = 1f - progress;
            text.color = fadedColor;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ReturnFloatingText(text);
    }

    private TMP_Text GetFloatingText()
    {
        if (m_floatingPool.Count > 0)
            return m_floatingPool.Pop();

        RectTransform parent = m_floatingRoot != null ? m_floatingRoot : transform as RectTransform;
        TMP_Text text = Instantiate(m_floatingTextPrefab, parent);
        text.raycastTarget = false;

        // 부모가 레이아웃 그룹이어도 위치가 밀리지 않게 레이아웃 계산에서 제외
        LayoutElement layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = text.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        return text;
    }

    private void ReturnFloatingText(TMP_Text text)
    {
        m_activeFloaters.Remove(text);
        text.gameObject.SetActive(false);
        m_floatingPool.Push(text);
    }

    // 행이 비활성화되면 진행 중이던 코루틴이 멈추므로 떠 있던 텍스트를 풀로 되돌림(부모가 이 행 밖일 때 화면에 남는 것 방지)
    private void ReturnAllActiveFloaters()
    {
        for (int i = m_activeFloaters.Count - 1; i >= 0; i--)
        {
            TMP_Text text = m_activeFloaters[i];
            text.gameObject.SetActive(false);
            m_floatingPool.Push(text);
        }
        m_activeFloaters.Clear();
    }
}
