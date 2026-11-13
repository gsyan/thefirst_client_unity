// 탐사 성공 이펙트 — 파티클 1세트(색은 광물량 단계별로 코드가 지정) + 선택적 월드 공간 텍스트("+광물량", 위로 떠오르며 페이드)
// 풀 규약은 EffectShieldHit과 동일(EPoolName 문자열, 자기 타입으로 반환) — EffectBase를 상속하면 반환 시 풀 타입이 어긋남
using System.Collections;
using TMPro;
using UnityEngine;

public class EffectScout : MonoBehaviour
{
    // EPoolName 문자열 — enum 직렬화 시 중간 삽입에 의한 인덱스 밀림 방지 (EffectBase와 동일 관례)
    [SerializeField] private string m_poolName;

    [SerializeField] private TMP_Text m_amountText;        // 월드 공간 TextMeshPro(3D) — 비워두면 텍스트 없이 파티클만 재생
    [SerializeField] private float m_lifetime = 1.2f;      // 이펙트 전체 재생 시간(초) — 이 시간이 지나면 풀로 반환
    [SerializeField] private float m_textRiseDistance = 0.6f; // 텍스트가 재생 동안 위로 떠오르는 월드 거리

    private ParticleSystem[] m_particleSystems;
    private Vector3 m_textStartLocalPosition;
    private Coroutine m_playCoroutine;

    private void Awake()
    {
        m_particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        if (m_amountText != null)
            m_textStartLocalPosition = m_amountText.transform.localPosition;
    }

    // worldPosition: 탐사 판정 위치, color: 광물량 단계 색(성공) 또는 실패 색(파티클 시작색 + 텍스트 색)
    // amount: 표시할 광물량 — 0 이하(탐사 실패)면 숫자 텍스트 없이 파티클만 재생
    public void PlayAt(Vector3 worldPosition, Color color, int amount)
    {
        bool showText = amount > 0;
        transform.position = worldPosition;

        for (int i = 0; i < m_particleSystems.Length; i++)
        {
            ParticleSystem ps = m_particleSystems[i];
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            ps.Clear();
            ps.Play();
        }

        if (m_amountText != null)
        {
            m_amountText.gameObject.SetActive(showText);
            if (showText == true)
            {
                m_amountText.text = "+" + amount;
                m_amountText.color = color;
                m_amountText.transform.localPosition = m_textStartLocalPosition;
            }
        }

        if (m_playCoroutine != null) StopCoroutine(m_playCoroutine);
        m_playCoroutine = StartCoroutine(Co_Play(color, showText));
    }

    // 텍스트를 카메라 쪽으로 향하게 하며 위로 띄우고, 후반부에 페이드아웃 — 파티클만 있는 프리팹이면 대기만 함
    private IEnumerator Co_Play(Color baseColor, bool showText)
    {
        Camera cam = Camera.main;
        float elapsed = 0f;
        while (elapsed < m_lifetime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_lifetime);

            if (m_amountText != null && showText == true)
            {
                m_amountText.transform.localPosition = m_textStartLocalPosition + Vector3.up * (m_textRiseDistance * t);
                if (cam != null)
                    m_amountText.transform.rotation = cam.transform.rotation;

                float alpha = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);
                m_amountText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
            yield return null;
        }

        ReturnEffect();
    }

    public void ReturnEffect()
    {
        if (m_playCoroutine != null)
        {
            StopCoroutine(m_playCoroutine);
            m_playCoroutine = null;
        }

        for (int i = 0; i < m_particleSystems.Length; i++)
            m_particleSystems[i].Stop();

        if (m_amountText != null)
            m_amountText.gameObject.SetActive(false);

        if (ObjectManager.Instance != null && System.Enum.TryParse(m_poolName, out EPoolName poolName))
            ObjectManager.Instance.m_poolManager.Return(poolName, this);
    }
}
