// 탐사 그리드 셀 3D 오브젝트 — 월드 고정 좌표에 배치되는 실제 씬 오브젝트. 화면(UI) 좌표/카메라 역산에 의존하지 않음(GridCellButton 대체)
// 클릭 판정은 Collider + 전용 Layer로 이뤄지고, CameraController.HandleGalaxyGridSelection이 레이캐스트로 히트를 잡아
// EventManager.Trigger_ExplorationGridCellClicked를 발행하면 UIPanelExplorationGrid가 GetRow()/GetCol()로 구독 처리
using System.Collections;
using TMPro;
using UnityEngine;

public enum EGridCellVisualState
{
    Unvisited, // 안 가본 곳 — 인접하지 않고 클리어도 안 됨, 클릭 불가
    Reachable, // 인접함 — 깜빡임 강조 + 테두리 오빗 강조, 클릭 가능
    Current,   // 현재 위치
    Cleared,   // 클리어됨 — 재방문 가능
    Blocked,   // 완전 통행불가(절차적 생성 시 막힌 셀) — 오브젝트 자체를 숨김, 자리만 차지
}

public class GridCell3D : MonoBehaviour
{
    [SerializeField] private Renderer m_renderer;       // 쿼드 전체 — 모서리만 불투명, 중심은 투명해지는 페이드 텍스처 + 상태색 틴트
    [SerializeField] private string m_colorPropertyName = "_BaseColor"; // URP Lit/Unlit 기준. Built-in 셰이더면 "_Color"로 교체
    [SerializeField] private string m_faceAlphaPropertyName = "_FaceAlpha"; // 면 채움 불투명도 — HoloGridCell 셰이더 전용 프로퍼티
    [SerializeField] private string m_orbitPhasePropertyName = "_OrbitPhase"; // 테두리를 도는 강조 구간의 둘레상 위치(0~1) — HoloGridCell 셰이더 전용
    [SerializeField] private string m_orbitEnabledPropertyName = "_OrbitEnabled"; // 오빗 강조 on/off — HoloGridCell 셰이더 전용
    [SerializeField] private string m_glowEnabledPropertyName = "_GlowEnabled"; // 모서리 밝기 부스트 on/off(Reachable 전용) — HoloGridCell 셰이더 전용
    private Vector3 m_labelLocalOffset = new Vector3(0f, 60f, 0f); // 쿼드 중심 기준 라벨 오프셋(월드 단위) — 쿼드 평면보다 살짝 위
    private float m_labelLocalScale = 30f; // k_cellWorldSize(800)에 맞춘 값 — 실제 화면 비율 보고 인스펙터에서 추가 조정
    [SerializeField] private float m_cellFillRatio = 0.85f; // 셀 간격(ExplorationGridGenerator.k_cellWorldSize) 대비 쿼드가 채우는 비율 — 나머지는 셀 사이 틈
    [SerializeField] private float m_unclearedFaceAlpha = 0.18f; // 시작점이 아니고 클리어 전인 셀의 면 채움 불투명도 — 완전 투명(클리어)과 뚜렷이 구분되는 값
    [SerializeField] private float m_reachableOrbitDuration = 3f; // Reachable 셀 테두리 오빗 강조가 한 바퀴 도는 데 걸리는 시간(초)

    private int m_row;
    private int m_col;
    private bool m_isStart;
    private bool m_isEscape;
    private Coroutine m_blinkCoroutine;
    private Coroutine m_orbitCoroutine;
    private MaterialPropertyBlock m_propertyBlock;

    // Initialize() 호출 전에 풀링/씬 전환 등으로 OnDisable이 먼저 불릴 수 있어(ArgumentNullException 원인),
    // m_propertyBlock은 Initialize가 아니라 Awake에서 항상 준비해둠
    private void Awake()
    {
        m_propertyBlock = new MaterialPropertyBlock();
    }

    public void Initialize(GridCellData cellData)
    {
        m_row = cellData.row;
        m_col = cellData.col;
        m_isStart = cellData.isStart;
        m_isEscape = cellData.isEscape;

        ApplyScale();
    }

    public int GetRow() { return m_row; }
    public int GetCol() { return m_col; }

    // 쿼드 스케일을 셀 간격(k_cellWorldSize)에 맞춰 자동 계산 — 간격이 튜닝돼도 쿼드 크기가 항상 따라가서 둘이 어긋나지 않음
    // 쿼드는 X축 90도 회전으로 바닥에 눕혀놨으므로 로컬 X/Y가 각각 월드 X/Z 크기가 됨(두께 축인 로컬 Z는 1로 고정)
    private void ApplyScale()
    {
        if (m_renderer == null) return;
        float xz = ExplorationGridGenerator.k_cellWorldSize * m_cellFillRatio;
        m_renderer.transform.localScale = new Vector3(xz, xz, 1f);
    }

    // isCleared는 state와 별도로 받는다 — Current/Reachable이 우선 표시되는 셀이라도 실제로는 이미 클리어된 상태일 수 있어서
    // (예: 클리어한 직후에도 그 자리에 서 있으면 상태는 Current), 면 투명도는 항상 실제 클리어 여부로만 결정해야 함
    // escapedZoneBefore는 셀이 아니라 존 단위 정보라 호출부(UIPanelExplorationGrid)에서 계산해 넘겨줌
    public void SetVisualState(EGridCellVisualState state, bool isCleared, bool escapedZoneBefore)
    {
        StopBlink();

        // 완전 통행불가 셀은 자리만 차지 — 보이지도, 눌리지도 않음
        if (state == EGridCellVisualState.Blocked)
        {
            gameObject.SetActive(false);
            return;
        }
        if (gameObject.activeSelf == false) gameObject.SetActive(true);

        Color fillColor;
        if (state == EGridCellVisualState.Current)
        {
            fillColor = CommonUtility.PaletteColor("Cell.Current");
        }
        else if (m_isStart == true)
        {
            // 시작점은 다른 상태색보다 우선 — 항상 고정된 Cell.Start 톤(단, 현재 위치면 위에서 이미 Cell.Current로 처리됨)
            fillColor = CommonUtility.PaletteColor("Cell.Start");
        }
        else if (m_isEscape == true && escapedZoneBefore == true)
        {
            // 예전에 한 번이라도 탈출 완료한 존이면 탈출 셀을 알아볼 수 있게 별도 톤으로 표시
            fillColor = CommonUtility.PaletteColor("Cell.Escape");
        }
        else
        {
            switch (state)
            {
                case EGridCellVisualState.Reachable:
                    fillColor = CommonUtility.PaletteColor("Cell.Reachable");
                    break;
                default: // Unvisited, Cleared — 클리어는 어차피 완전 투명(faceAlpha=0)이라 별도 강조색 없이 동일한 중립 톤 유지
                    fillColor = CommonUtility.PaletteColor("Cell.Normal");
                    break;
            }
        }

        // 투명도 규칙: 완전 투명 = 이번 런에서 수색/전투 완료(Cleared), 반투명 = 아직 안 가본 셀(미클리어) — 시작점은 예외로 항상 투명
        float faceAlpha = (m_isStart == true || isCleared == true) ? 0f : m_unclearedFaceAlpha;

        ApplyColor(fillColor, faceAlpha);

        // 테두리 발광(블링크) + 오빗 강조는 "지금 이동 가능한 셀(Reachable)"에만 — Current는 고정 강조색만 유지
        if (state == EGridCellVisualState.Reachable)
        {
            m_blinkCoroutine = StartCoroutine(BlinkRoutine(fillColor, faceAlpha));
            StartOrbitPhase();
        }
        else
        {
            StopOrbitPhase();
        }
    }

    private void ApplyColor(Color color, float faceAlpha)
    {
        if (m_renderer == null) return;
        m_renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetColor(m_colorPropertyName, color);
        m_propertyBlock.SetFloat(m_faceAlphaPropertyName, faceAlpha);
        m_renderer.SetPropertyBlock(m_propertyBlock);
    }

    // 흰색으로 섞지 않고 자기 색조(hue)를 유지한 채 밝기만 사인파로 오르내림 — Start/Escape처럼 고유색이 있는 셀도 그 색 그대로 블링크
    private IEnumerator BlinkRoutine(Color baseColor, float faceAlpha)
    {
        while (true)
        {
            float blinkPhase = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f; // 0~1
            float brightness = 1f + blinkPhase; // 1배~2배
            Color pulseColor = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
            ApplyColor(pulseColor, faceAlpha);
            yield return null;
        }
    }

    private void StopBlink()
    {
        if (m_blinkCoroutine != null)
        {
            StopCoroutine(m_blinkCoroutine);
            m_blinkCoroutine = null;
        }
    }

    // 쿼드 테두리를 국부적으로 두껍게 만드는 구간(_OrbitPhase)을 0→1로 계속 순환시켜 테두리를 도는 것처럼 보이게 함
    // 모서리 밝기 부스트(_GlowEnabled)도 같이 켠다 — 둘 다 "지금 이동 가능한 셀(Reachable)" 전용 강조라 묶어서 관리
    private void StartOrbitPhase()
    {
        if (m_renderer == null) return;

        m_renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetFloat(m_orbitEnabledPropertyName, 1f);
        m_propertyBlock.SetFloat(m_glowEnabledPropertyName, 1f);
        m_renderer.SetPropertyBlock(m_propertyBlock);

        if (m_orbitCoroutine == null)
            m_orbitCoroutine = StartCoroutine(OrbitPhaseRoutine());
    }

    private void StopOrbitPhase()
    {
        if (m_orbitCoroutine != null)
        {
            StopCoroutine(m_orbitCoroutine);
            m_orbitCoroutine = null;
        }

        if (m_renderer == null) return;
        m_renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetFloat(m_orbitEnabledPropertyName, 0f);
        m_propertyBlock.SetFloat(m_glowEnabledPropertyName, 0f);
        m_renderer.SetPropertyBlock(m_propertyBlock);
    }

    private IEnumerator OrbitPhaseRoutine()
    {
        while (true)
        {
            float orbitPhase = (Time.time % m_reachableOrbitDuration) / m_reachableOrbitDuration;
            m_renderer.GetPropertyBlock(m_propertyBlock);
            m_propertyBlock.SetFloat(m_orbitPhasePropertyName, orbitPhase);
            m_renderer.SetPropertyBlock(m_propertyBlock);
            yield return null;
        }
    }

    private void OnDisable()
    {
        StopBlink();
        StopOrbitPhase();
    }
}
