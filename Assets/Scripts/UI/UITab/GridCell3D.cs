// 탐사 그리드 셀 3D 오브젝트 — 월드 고정 좌표에 배치되는 실제 씬 오브젝트. 화면(UI) 좌표/카메라 역산에 의존하지 않음(GridCellButton 대체)
// 클릭 판정은 Collider + 전용 Layer로 이뤄지고, CameraController.HandleGalaxyGridSelection이 레이캐스트로 히트를 잡아
// EventManager.Trigger_ExplorationGridCellClicked를 발행하면 UIPanelExplorationGrid가 GetRow()/GetCol()로 구독 처리
using System.Collections;
using TMPro;
using UnityEngine;

public enum EGridCellVisualState
{
    Unvisited, // 안 가본 곳 — 인접하지 않고 클리어도 안 됨, 클릭 불가
    Reachable, // 인접함 — 깜빡임 강조, 클릭 가능
    Current,   // 현재 위치
    Cleared,   // 클리어됨 — 재방문 가능
    Blocked,   // 완전 통행불가(절차적 생성 시 막힌 셀) — 오브젝트 자체를 숨김, 자리만 차지
}

public class GridCell3D : MonoBehaviour
{
    [SerializeField] private Renderer m_renderer;       // 큐브 전체 — 모서리만 불투명, 중심은 투명해지는 페이드 텍스처 + 상태색 틴트
    [SerializeField] private TextMeshPro m_stateLabel;  // World Space 텍스트(Start/Escape/Cleared)
    [SerializeField] private GridCellOrbitGlow m_orbitGlow; // 셀 외곽을 도는 발광 오브젝트 — Current/Reachable에서만 활성화
    [SerializeField] private string m_colorPropertyName = "_BaseColor"; // URP Lit/Unlit 기준. Built-in 셰이더면 "_Color"로 교체
    [SerializeField] private string m_faceAlphaPropertyName = "_FaceAlpha"; // 면 채움 불투명도 — HoloGridCell 셰이더 전용 프로퍼티
    private Vector3 m_labelLocalOffset = new Vector3(0f, 60f, 0f); // 큐브 중심 기준 라벨 오프셋(월드 단위) — 큐브 윗면(m_cellHeight 절반)보다 살짝 위
    private float m_labelLocalScale = 30f; // k_cellWorldSize(800)에 맞춘 값 — 실제 화면 비율 보고 인스펙터에서 추가 조정
    [SerializeField] private float m_cellFillRatio = 0.85f; // 셀 간격(ExplorationGridGenerator.k_cellWorldSize) 대비 큐브가 채우는 비율 — 나머지는 셀 사이 틈
    [SerializeField] private float m_cellHeight = 80f; // 큐브 높이(월드 유닛) — 위아래로 납작한 정도
    [SerializeField] private float m_unclearedFaceAlpha = 0.05f; // 시작점이 아니고 클리어 전인 셀의 면 채움 불투명도
    [SerializeField] private float m_currentOrbitDuration = 2.2f; // Current 상태 외곽 발광이 한 바퀴 도는 데 걸리는 시간(초)
    [SerializeField] private float m_reachableOrbitDuration = 3f; // Reachable 상태 외곽 발광이 한 바퀴 도는 데 걸리는 시간(초)

    private int m_row;
    private int m_col;
    private bool m_isStart;
    private bool m_isEscape;
    private Coroutine m_blinkCoroutine;
    private MaterialPropertyBlock m_propertyBlock;

    public void Initialize(GridCellData cellData)
    {
        m_row = cellData.row;
        m_col = cellData.col;
        m_isStart = cellData.isStart;
        m_isEscape = cellData.isEscape;

        if (m_propertyBlock == null) m_propertyBlock = new MaterialPropertyBlock();

        ApplyScale();

        if (m_orbitGlow != null)
        {
            float orbitHalfExtent = ExplorationGridGenerator.k_cellWorldSize * m_cellFillRatio * 0.5f;
            float orbitHeight = m_cellHeight * 0.5f + 5f; // 큐브 윗면보다 살짝 위에서 돌게
            m_orbitGlow.SetPerimeter(orbitHalfExtent, orbitHeight);
        }

        if (m_stateLabel != null)
        {
            m_stateLabel.transform.localPosition = m_labelLocalOffset;
            m_stateLabel.transform.localScale = Vector3.one * m_labelLocalScale;
        }

        UpdateStateLabel();
    }

    public int GetRow() { return m_row; }
    public int GetCol() { return m_col; }

    // 큐브 스케일을 셀 간격(k_cellWorldSize)에 맞춰 자동 계산 — 간격이 튜닝돼도 큐브 크기가 항상 따라가서 둘이 어긋나지 않음
    private void ApplyScale()
    {
        if (m_renderer == null) return;
        float xz = ExplorationGridGenerator.k_cellWorldSize * m_cellFillRatio;
        m_renderer.transform.localScale = new Vector3(xz, m_cellHeight, xz);
    }

    // 갤럭시뷰 카메라가 정착한 뒤 스폰 시 1회만 호출 — 그리드가 열려있는 동안 카메라가 고정이라 매 프레임 갱신 불필요
    // 셀 위치 기준 LookAt이 아니라 카메라의 회전각(피치/요)을 모든 셀에 동일하게 복사 — 셀마다 카메라를 향하는 방향이 미묘하게 달라
    // 텍스트가 각자 다르게 틀어져 보이는 것을 방지(그리드 전체가 한 몸처럼 같은 각도로만 기울어짐)
    public void FaceCameraOnce(Camera cam)
    {
        if (m_stateLabel == null || cam == null) return;
        Vector3 camEuler = cam.transform.rotation.eulerAngles;
        m_stateLabel.transform.rotation = Quaternion.Euler(camEuler.x, camEuler.y, 0f);
    }

    // start/escape가 clear보다 우선 표시 — 셀 하나에 여러 상태가 겹쳐도 위치(시작/탈출)가 더 중요한 정보이기 때문
    private void UpdateStateLabel(bool isCleared = false)
    {
        if (m_stateLabel == null) return;

        if (m_isStart == true)
            m_stateLabel.text = "Start";
        else if (m_isEscape == true)
            m_stateLabel.text = "Escape";
        else if (isCleared == true)
            m_stateLabel.text = "Cleared";
        else
            m_stateLabel.text = string.Empty;
    }

    public void SetVisualState(EGridCellVisualState state)
    {
        StopBlink();

        // 완전 통행불가 셀은 자리만 차지 — 보이지도, 눌리지도 않음
        if (state == EGridCellVisualState.Blocked)
        {
            gameObject.SetActive(false);
            return;
        }
        if (gameObject.activeSelf == false) gameObject.SetActive(true);

        bool isCleared = state == EGridCellVisualState.Cleared;

        Color fillColor;
        switch (state)
        {
            case EGridCellVisualState.Current:
                fillColor = CommonUtility.PaletteColor("Selected");
                break;
            case EGridCellVisualState.Reachable:
                fillColor = CommonUtility.PaletteColor("Unlocked");
                break;
            case EGridCellVisualState.Cleared:
                // 시작점은 Cleared 색(General.Dark1)이 배경과 거의 구분 안 돼 모서리까지 안 보이는 문제 방지 — Unvisited와 같은 톤 유지
                fillColor = m_isStart == true ? CommonUtility.PaletteColor("Zone.Locked") : CommonUtility.PaletteColor("General.Dark1");
                break;
            default: // Unvisited
                fillColor = CommonUtility.PaletteColor("Zone.Locked");
                break;
        }

        // 시작점은 처음부터 투명, 클리어 전이면 반투명 채움, 클리어되면 다시 투명
        float faceAlpha = (m_isStart == true || isCleared == true) ? 0f : m_unclearedFaceAlpha;

        ApplyColor(fillColor, faceAlpha);
        UpdateStateLabel(isCleared);

        // 외곽 발광은 항상 그 셀의 모서리 색과 동일하게 — 별도 색으로 튀지 않게
        if (state == EGridCellVisualState.Reachable)
        {
            m_blinkCoroutine = StartCoroutine(BlinkRoutine(faceAlpha));
            if (m_orbitGlow != null) m_orbitGlow.StartOrbit(fillColor, m_reachableOrbitDuration);
        }
        else if (state == EGridCellVisualState.Current)
        {
            if (m_orbitGlow != null) m_orbitGlow.StartOrbit(fillColor, m_currentOrbitDuration);
        }
        else
        {
            if (m_orbitGlow != null) m_orbitGlow.StopOrbit();
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

    private IEnumerator BlinkRoutine(float faceAlpha)
    {
        Color baseColor = CommonUtility.PaletteColor("Unlocked");
        while (true)
        {
            float blinkPhase = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            ApplyColor(Color.Lerp(baseColor, Color.white, blinkPhase), faceAlpha);
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

    private void OnDisable()
    {
        StopBlink();
        if (m_orbitGlow != null) m_orbitGlow.StopOrbit();
    }
}
