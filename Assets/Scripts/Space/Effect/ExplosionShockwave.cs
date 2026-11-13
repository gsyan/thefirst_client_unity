using System.Collections;
using UnityEngine;

// 미사일 범위 피해 충격파 구 — 폭발 이펙트 프리팹에 붙여, 구 지름이 실제 판정 반경(×2)까지 커졌다가 사라지게 함
// 구 메시는 Unity 기본 Sphere(크기 1 = 지름 1) 기준. 콜라이더는 없어야 함(범위 피해 OverlapSphere에 걸리지 않게)
public class ExplosionShockwave : MonoBehaviour
{
    [Tooltip("충격파 구 Transform — 기본 Sphere 메시, ShockwaveSphere 머티리얼")]
    [SerializeField] private Transform m_sphere;
    [Tooltip("충격파 구 Renderer — 투명도(_Alpha) 조절용")]
    [SerializeField] private Renderer m_sphereRenderer;
    [Tooltip("0에서 판정 반경까지 커지는 시간(초)")]
    [SerializeField] private float m_expandDuration = 0.15f;
    [Tooltip("최대 크기 도달 후 사라지는 시간(초) — 이펙트가 풀로 돌아가기 전에 끝나야 함")]
    [SerializeField] private float m_fadeDuration = 0.25f;

    private static readonly int k_alphaPropertyId = Shader.PropertyToID("_Alpha");
    private MaterialPropertyBlock m_propertyBlock;
    private Coroutine m_playCoroutine;

    private void Awake()
    {
        m_propertyBlock = new MaterialPropertyBlock();
        HideSphere();
    }

    private void OnDisable()
    {
        m_playCoroutine = null;
        HideSphere();
    }

    // radius: 범위 피해 판정 반경(월드 유닛). 0 이하면 표시 안 함
    public void Play(float radius)
    {
        if (m_sphere == null) return;
        if (m_playCoroutine != null)
        {
            StopCoroutine(m_playCoroutine);
            m_playCoroutine = null;
        }
        if (radius <= 0f)
        {
            HideSphere();
            return;
        }
        m_playCoroutine = StartCoroutine(Co_Play(radius));
    }

    private IEnumerator Co_Play(float radius)
    {
        // 부모(이펙트 루트) 크기가 폭발 반경에 따라 바뀌므로 그만큼 나눠 월드 지름 = 2 × radius로 맞춤
        float parentScale = m_sphere.parent != null ? m_sphere.parent.lossyScale.x : 1f;
        float safeParentScale = Mathf.Max(parentScale, 0.0001f);
        float targetLocalDiameter = (radius * 2f) / safeParentScale;

        m_sphere.gameObject.SetActive(true);
        SetAlpha(1f);

        float elapsed = 0f;
        while (elapsed < m_expandDuration)
        {
            float t = elapsed / m_expandDuration;
            float easedT = 1f - (1f - t) * (1f - t); // 빠르게 커지다 끝에서 감속
            m_sphere.localScale = Vector3.one * (targetLocalDiameter * easedT);
            elapsed += Time.deltaTime;
            yield return null;
        }
        m_sphere.localScale = Vector3.one * targetLocalDiameter;

        elapsed = 0f;
        while (elapsed < m_fadeDuration)
        {
            float fadeAlpha = 1f - (elapsed / m_fadeDuration);
            SetAlpha(fadeAlpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        HideSphere();
        m_playCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (m_sphereRenderer == null) return;
        m_sphereRenderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetFloat(k_alphaPropertyId, alpha);
        m_sphereRenderer.SetPropertyBlock(m_propertyBlock);
    }

    private void HideSphere()
    {
        if (m_sphere == null) return;
        m_sphere.localScale = Vector3.zero;
        m_sphere.gameObject.SetActive(false);
    }
}
