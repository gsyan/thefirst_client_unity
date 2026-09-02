// 실드 피격 파동 이펙트 — 스케일 확산+페이드아웃 재생 로직은 FORGE3D F3DPulsewave(pulsewave_002)의 동작을 그대로 옮겨온 것.
// 원본 스크립트(OnSpawned/OnDespawned)는 서드파티 자체 풀링 규약을 전제로 만들어져 있어(private, 우리 PoolManager와 다른 훅) 직접 재사용하지 않고,
// 재생 부분만 이 컴포넌트에 복제해 프로젝트 풀링 관례(EPoolName 문자열, ReturnEffect)에 맞춤.
// 이 컴포넌트는 부모(ShieldEffect)에 위치 — 자식으로 pulsewave_002(메시/머티리얼)를 두고, 그 자식의 로컬 회전으로 파동 확산축(원본 로컬 Y축)을
// 이 부모의 forward(Z축)에 맞춰둔다 — PlayAt은 부모의 위치/회전/스케일만 조작, 실제 메시는 자식 트랜스폼 그대로 상속됨
using UnityEngine;

public class EffectShieldHit : MonoBehaviour
{
    // EPoolName 문자열 — enum 직렬화 시 중간 삽입에 의한 인덱스 밀림 방지 (EffectBase와 동일 관례)
    [SerializeField] private string m_poolName;

    [SerializeField] private float m_fadeOutDelay = 0.2f;
    [SerializeField] private float m_fadeOutTime = 5f;
    [SerializeField] private float m_scaleTime = 5f;
    [SerializeField] private Vector3 m_scaleSize = new Vector3(1f, 0.2f, 1f);

    private MeshRenderer m_meshRenderer;
    private static readonly int k_tintColorId = Shader.PropertyToID("_Color");

    private Color m_defaultColor;
    private Color m_color;
    private float m_fadeOutStartTime;
    private bool m_isFadingOut;
    private bool m_isPlaying;

    private void Awake()
    {
        m_meshRenderer = GetComponentInChildren<MeshRenderer>();
        m_defaultColor = m_meshRenderer.material.GetColor(k_tintColorId);
    }

    // hitPoint: 함체 표면 피격 지점(월드), normal: 그 지점의 바깥 방향 — 파동이 실드 표면에 접하도록 배치
    public void PlayAt(Vector3 hitPoint, Vector3 normal)
    {
        transform.position = hitPoint;
        transform.rotation = Quaternion.LookRotation(normal);
        transform.localScale = Vector3.zero;

        m_color = m_defaultColor;
        m_meshRenderer.material.SetColor(k_tintColorId, m_color);

        m_isFadingOut = false;
        m_isPlaying = true;
        m_fadeOutStartTime = Time.time + m_fadeOutDelay;
    }

    private void Update()
    {
        if (m_isPlaying == false) return;

        transform.localScale = Vector3.Lerp(transform.localScale, m_scaleSize, Time.deltaTime * m_scaleTime);

        if (m_isFadingOut == false && Time.time >= m_fadeOutStartTime)
            m_isFadingOut = true;

        if (m_isFadingOut == true)
        {
            m_color = Color.Lerp(m_color, new Color(0f, 0f, 0f, -0.1f), Time.deltaTime * m_fadeOutTime);
            m_meshRenderer.material.SetColor(k_tintColorId, m_color);

            if (m_color.a <= 0f)
            {
                m_isPlaying = false;
                ReturnEffect();
            }
        }
    }

    public void ReturnEffect()
    {
        m_isPlaying = false;

        if (ObjectManager.Instance != null && System.Enum.TryParse(m_poolName, out EPoolName poolName))
            ObjectManager.Instance.m_poolManager.Return(poolName, this);
    }
}
