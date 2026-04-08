// 미사일 측면/꼬리 노즐 오브젝트에 부착 — 활성/비활성 및 펄스를 자체적으로 관리
using System.Collections;
using UnityEngine;

public class BurstNozzle : MonoBehaviour
{
    private Coroutine m_pulseCoroutine;
    private bool m_isPulsing;

    // 지속 ON (회전 중)
    public void TurnOn()
    {
        if (m_isPulsing == true) return;
        gameObject.SetActive(true);
    }

    // 지속 OFF — 펄스 중이면 무시
    public void TurnOff()
    {
        if (m_isPulsing == true) return;
        gameObject.SetActive(false);
    }

    // 역추진 펄스 — duration 동안 켜졌다가 스스로 꺼짐
    public void Pulse(float duration)
    {
        if (gameObject.activeSelf == true)
        {
            if (m_pulseCoroutine != null)
                StopCoroutine(m_pulseCoroutine);
        }
        else
            gameObject.SetActive(true);
        
        m_pulseCoroutine = StartCoroutine(PulseCoroutine(duration));
    }

    // 풀 반환 시 상태 초기화
    public void ResetNozzle()
    {
        if (m_pulseCoroutine != null)
        {
            StopCoroutine(m_pulseCoroutine);
            m_pulseCoroutine = null;
        }
        m_isPulsing = false;
        gameObject.SetActive(false);
    }

    private IEnumerator PulseCoroutine(float duration)
    {
        m_isPulsing = true;
        gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        gameObject.SetActive(false);
        m_isPulsing = false;
        m_pulseCoroutine = null;
    }
}
