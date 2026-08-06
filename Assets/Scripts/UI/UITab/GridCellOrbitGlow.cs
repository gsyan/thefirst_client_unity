// 셀 외곽(사각 테두리)을 따라 계속 도는 발광 오브젝트 — Current/Reachable 셀 위치를 강조하는 연출용
using System.Collections;
using UnityEngine;

public class GridCellOrbitGlow : MonoBehaviour
{
    [SerializeField] private Renderer m_renderer;
    [SerializeField] private string m_colorPropertyName = "_BaseColor";

    private MaterialPropertyBlock m_propertyBlock;
    private Coroutine m_orbitCoroutine;
    private readonly Vector3[] m_pathCorners = new Vector3[4];

    // 셀 크기가 바뀌어도 항상 실제 테두리를 따라가도록 Initialize 시점에 전달받음
    public void SetPerimeter(float halfExtent, float height)
    {
        m_pathCorners[0] = new Vector3(-halfExtent, height, -halfExtent);
        m_pathCorners[1] = new Vector3(halfExtent, height, -halfExtent);
        m_pathCorners[2] = new Vector3(halfExtent, height, halfExtent);
        m_pathCorners[3] = new Vector3(-halfExtent, height, halfExtent);
    }

    public void StartOrbit(Color color, float loopDuration)
    {
        StopOrbit();

        if (m_propertyBlock == null) m_propertyBlock = new MaterialPropertyBlock();
        ApplyColor(color);

        gameObject.SetActive(true);
        m_orbitCoroutine = StartCoroutine(OrbitRoutine(loopDuration));
    }

    public void StopOrbit()
    {
        if (m_orbitCoroutine != null)
        {
            StopCoroutine(m_orbitCoroutine);
            m_orbitCoroutine = null;
        }
        gameObject.SetActive(false);
    }

    private IEnumerator OrbitRoutine(float loopDuration)
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float loopPhase = (elapsed % loopDuration) / loopDuration; // 0~1
            float segmentPhase = loopPhase * 4f;
            int segmentIndex = Mathf.FloorToInt(segmentPhase);
            float segmentT = segmentPhase - segmentIndex;

            Vector3 fromCorner = m_pathCorners[segmentIndex % 4];
            Vector3 toCorner = m_pathCorners[(segmentIndex + 1) % 4];
            transform.localPosition = Vector3.Lerp(fromCorner, toCorner, segmentT);

            yield return null;
        }
    }

    private void ApplyColor(Color color)
    {
        if (m_renderer == null) return;
        m_renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetColor(m_colorPropertyName, color);
        m_renderer.SetPropertyBlock(m_propertyBlock);
    }

    private void OnDisable()
    {
        if (m_orbitCoroutine != null)
        {
            StopCoroutine(m_orbitCoroutine);
            m_orbitCoroutine = null;
        }
    }
}
