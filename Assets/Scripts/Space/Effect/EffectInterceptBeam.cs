// 요격 이펙트 — 요격체 위치에서 미사일 위치까지 뻗는 빔(LineRenderer) + 끝점 임팩트 파티클. PlayBeam이 시작/끝점을 받아 빔의 길이와 방향을 맞춘 뒤 재생한다.
// 빔 라인은 k_beamDuration 동안만 보이고, 파티클은 EffectBase의 재생 시간이 끝나면 풀로 반환됨.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EffectInterceptBeam : EffectBase
{
    private const string k_impactChildName = "plasma_beam_flare_green"; // 빔 끝점에 놓이는 임팩트 파티클
    private const float k_beamDuration = 0.25f;
    private const float k_textureScale = 0.05f; // 빔 길이 1당 텍스처 타일링 비율
    private const float k_uvAnimateSpeed = 6f;
    private const float k_uvAnimateAmplitude = 0.05f;
    private static readonly int s_baseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int s_offsetId = Shader.PropertyToID("_Offset");

    private LineRenderer m_lineRenderer;
    private Transform m_impact;
    private Material m_beamMaterial;
    private float m_baseWidthMultiplier;
    private Coroutine m_beamCoroutine;

    protected override void Awake()
    {
        base.Awake();
        m_lineRenderer = GetComponent<LineRenderer>();
        m_impact = transform.Find(k_impactChildName);
        m_beamMaterial = m_lineRenderer.material;
        m_baseWidthMultiplier = m_lineRenderer.widthMultiplier;
        m_lineRenderer.enabled = false;
    }

    private void OnDisable()
    {
        m_lineRenderer.enabled = false;
        m_beamCoroutine = null;
    }

    private void OnDestroy()
    {
        if (m_beamMaterial != null) Destroy(m_beamMaterial);
    }

    // startPosition: 요격체 위치, endPosition: 미사일 위치
    public void PlayBeam(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 beamVector = endPosition - startPosition;
        float distance = beamVector.magnitude;

        transform.position = startPosition;
        if (distance > 0.001f)
            transform.rotation = Quaternion.LookRotation(beamVector / distance);

        // 라인/임팩트 좌표는 로컬이라 루트 스케일만큼 나눠야 월드 기준 distance가 됨
        float localLength = distance / transform.lossyScale.z;
        m_lineRenderer.SetPosition(0, Vector3.zero);
        m_lineRenderer.SetPosition(1, new Vector3(0f, 0f, localLength));
        m_beamMaterial.SetTextureScale(s_baseMapId, new Vector2(distance * k_textureScale, 1f));
        if (m_impact != null)
            m_impact.localPosition = new Vector3(0f, 0f, localLength);

        PlayEffect();

        if (m_beamCoroutine != null) StopCoroutine(m_beamCoroutine);
        m_beamCoroutine = StartCoroutine(Co_ShowBeam());
    }

    private IEnumerator Co_ShowBeam()
    {
        m_lineRenderer.enabled = true;
        float elapsed = 0f;
        while (elapsed < k_beamDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / k_beamDuration);
            m_lineRenderer.widthMultiplier = m_baseWidthMultiplier * fade;

            float offsetX = 0.5f + Mathf.Sin(elapsed * k_uvAnimateSpeed) * k_uvAnimateAmplitude;
            m_beamMaterial.SetVector(s_offsetId, new Vector4(offsetX, 0f, 0f, 0f));
            yield return null;
        }
        m_lineRenderer.enabled = false;
        m_beamCoroutine = null;
    }
}
