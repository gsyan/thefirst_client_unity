using UnityEngine;
using System.Collections;

public class ProjectileBeam : ProjectileBase
{
    [SerializeField] private LineRenderer m_lineRenderer;
    [SerializeField] private float m_maxBeamLength = 5f;
    [SerializeField] private float m_beamWidth = 0.1f;
    [SerializeField] private float m_beamSpeed = 20f;
    
    [SerializeField] private Material m_beamMaterialPrefab;
    [SerializeField] private Color m_beamColor = Color.cyan;
    [SerializeField] private float m_uvScrollSpeed = 2f;
    private EffectBase m_headEffect;

    
    private Vector3 m_direction;
    private Vector3 m_beamHeadPos;
    private Vector3 m_beamTailPos;
    private float m_toalBeamLength;
    
    private SpaceShip m_currentTarget = null;
    
    private Material m_beamMaterial;
    private float m_uvOffset;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 5f;
    private float m_hitEffectTime;

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData,
                          Color color, ModuleBase sourceModuleBase)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase);
        
        m_beamHeadPos = m_firePointTransform.position;
        m_beamTailPos = m_firePointTransform.position;
        m_maxBeamLength = moduleData.m_projectileLength;
        m_beamWidth = moduleData.m_projectileWidth;
        m_beamSpeed = moduleData.m_projectileSpeed;        
        m_beamColor = color;        
        

        if (m_lineRenderer == null)
            m_lineRenderer = GetComponent<LineRenderer>();

        if (m_lineRenderer == null)
            m_lineRenderer = gameObject.AddComponent<LineRenderer>();

        m_lineRenderer.positionCount = 2;
        m_lineRenderer.startWidth = m_beamWidth;
        m_lineRenderer.endWidth = m_beamWidth/* * 0.8f*/;
        m_lineRenderer.enabled = true;

        if (m_beamMaterialPrefab != null)
        {
            m_beamMaterial = new Material(m_beamMaterialPrefab);
            m_beamMaterial.color = m_beamColor;
            m_lineRenderer.material = m_beamMaterial;
        }
        else if (m_lineRenderer.material != null)
        {
            m_beamMaterial = m_lineRenderer.material;
            m_beamMaterial.color = m_beamColor;
        }

        m_toalBeamLength = 0f;
        m_currentTarget = null;
        m_uvOffset = 0f;
        m_lifeTime = 0f;
        m_hitEffectTime = Time.time;

        if (m_headEffect == null)
        {
            m_headEffect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_BEAM_HEAD);
            m_headEffect.Play();
            m_headEffect.transform.position = m_firePointTransform.position;
        }
        
        if (m_lifeCycleCoroutine != null)
            StopCoroutine(m_lifeCycleCoroutine);

        m_lifeCycleCoroutine = StartCoroutine(BeamLifeCycle());
    }

    private IEnumerator BeamLifeCycle()
    {
        Vector3 saveTargetPosition = m_target.transform.position;

        while (true)
        {
            m_lifeTime += Time.deltaTime;
            if (m_lifeTime >= MAX_LIFE_TIME)
            {
                ReturnToPool();
                yield break;
            }


            float thisFrameBeamMoveLength = m_beamSpeed * Time.deltaTime;
            m_toalBeamLength += thisFrameBeamMoveLength;
            // 모듈 오브젝트 교체시 m_firePointTransform 가 null 이 됨
            if (m_toalBeamLength >= m_maxBeamLength || m_firePointTransform == null)
            {
                m_toalBeamLength = m_maxBeamLength;
                m_beamHeadPos += m_direction * thisFrameBeamMoveLength;
                m_beamTailPos += m_direction * thisFrameBeamMoveLength;
            }
            else
            {
                m_beamTailPos = m_firePointTransform.position;
                if (m_target != null)
                    m_direction = (m_target.transform.position - m_beamTailPos).normalized;
                else
                    m_direction = (saveTargetPosition - m_beamTailPos).normalized;
                m_beamHeadPos = m_beamTailPos + m_direction * m_toalBeamLength;
            }


            float checkDistance = Vector3.Distance(m_beamHeadPos, m_beamTailPos);
            // RaycastHit? validHit = GetValidRaycastHit(m_beamTailPos, m_direction, checkDistance);
            // if (validHit.HasValue)
            // {
            //     RaycastHit hit = validHit.Value;
            RaycastHit hit;            
            if (Physics.Raycast(m_beamTailPos, m_direction, out hit, checkDistance))
            {
                SpaceShip hitTarget = hit.collider.GetComponentInParent<SpaceShip>();
                if (hitTarget != null && m_sourceShip != null)
                {
                    SpaceFleet myFleet = m_sourceShip.GetComponentInParent<SpaceFleet>();
                    SpaceFleet targetFleet = hitTarget.GetComponentInParent<SpaceFleet>();
                    if (myFleet != null && targetFleet != null && myFleet == targetFleet)
                    {
                        ReturnToPool();
                        yield break;
                    }

                    m_beamHeadPos = hit.point;
                    m_currentTarget = hitTarget;

                    if (m_currentTarget != null)
                    {
                        float damageRatio = thisFrameBeamMoveLength / m_maxBeamLength;
                        float frameDamage = m_damage * damageRatio;
                        m_currentTarget.TakeDamage(frameDamage);

                        if (Time.time - m_hitEffectTime >= 1.0f)
                        {
                            ObjectManager.Instance.m_poolManager.GetEffect_Play_AutoReturn(EPoolName.EFFECT_BEAM_HIT, m_beamHeadPos);
                            m_hitEffectTime = Time.time;
                        }
                    }
                }
            }
            else
            {
                m_currentTarget = null;
            }

            m_headEffect.transform.position = m_beamHeadPos;
            m_lineRenderer.SetPosition(0, m_beamHeadPos);
            m_lineRenderer.SetPosition(1, m_beamTailPos);

            if (m_beamMaterial != null)
            {
                m_uvOffset += m_uvScrollSpeed * Time.deltaTime;
                Vector2 offset = new Vector2(m_uvOffset, 0);
                m_beamMaterial.SetTextureOffset("_MainTex", offset);
            }

            float currentHeadTailDistance = Vector3.Distance(m_beamHeadPos, m_beamTailPos);
            if (currentHeadTailDistance < thisFrameBeamMoveLength - 0.01f)
            {
                if (m_currentTarget != null)
                {
                    float damageRatio = currentHeadTailDistance / m_maxBeamLength;
                    float frameDamage = m_damage * damageRatio;
                    m_currentTarget.TakeDamage(frameDamage);
                }

                ReturnToPool();
                yield break;
            }

            yield return null;
        }
    }

    // source 모듈(발사한 무기)의 콜라이더를 제외한 유효한 RaycastHit 반환
    private static readonly RaycastHit[] s_raycastHitBuffer = new RaycastHit[16];
    private RaycastHit? GetValidRaycastHit(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, direction, s_raycastHitBuffer, distance);
        if (hitCount == 0) return null;

        // 거리순 정렬
        System.Array.Sort(s_raycastHitBuffer, 0, hitCount, s_raycastHitComparer);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_raycastHitBuffer[i];

            // source 모듈 자신의 콜라이더인지 확인
            if (m_sourceModuleBase != null)
            {
                // hit된 오브젝트가 source 모듈의 자식인지 확인
                if (hit.collider.transform.IsChildOf(m_sourceModuleBase.transform))
                    continue;
            }

            // 유효한 hit 발견
            return hit;
        }

        return null;
    }

    private static readonly RaycastHitDistanceComparer s_raycastHitComparer = new RaycastHitDistanceComparer();
    private class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private void ReturnToPool()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }

        if (m_headEffect != null)
        {
            m_headEffect.ReturnToPool();
            m_headEffect = null;
        }

        m_lineRenderer.enabled = false;
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_BEAM, this);
    }
}
