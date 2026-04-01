// 배경 데코 아이템 — Billboard(항상 카메라 정면) + 원점 기준 공전 + 자전 연출
using UnityEngine;
using System.Collections;

public class SpaceDecorItem : MonoBehaviour
{
    private float m_rotationSpeed;
    private Camera m_camera;
    private float m_orbitRadius;
    private float m_orbitPeriod;
    private float m_orbitAngle;

    public void Initialize(float rotationSpeed, Camera cam, float orbitRadius, float orbitPeriod)
    {
        m_rotationSpeed = rotationSpeed;
        m_camera = cam;
        m_orbitRadius = orbitRadius;
        m_orbitPeriod = orbitPeriod;

        // 기준일 2024-01-10, 시작 방향 (0,0,1) = angle π/2
        System.DateTime epoch = new System.DateTime(2024, 1, 10, 0, 0, 0, System.DateTimeKind.Utc);
        double elapsedSeconds = (System.DateTime.UtcNow - epoch).TotalSeconds;
        float angularSpeed = (orbitPeriod > 0f) ? (2f * Mathf.PI / orbitPeriod) : 0f;
        m_orbitAngle = Mathf.PI / 2f + (float)(elapsedSeconds * angularSpeed);

        ApplyOrbitPosition();

        if (m_orbitPeriod > 0f)
            StartCoroutine(OrbitCoroutine());
    }

    private IEnumerator OrbitCoroutine()
    {
        float angularSpeed = 2f * Mathf.PI / m_orbitPeriod;
        while (true)
        {
            m_orbitAngle += angularSpeed * Time.deltaTime;
            ApplyOrbitPosition();
            yield return null;
        }
    }

    private void ApplyOrbitPosition()
    {
        transform.position = new Vector3(
            m_orbitRadius * Mathf.Cos(m_orbitAngle),
            0f,
            m_orbitRadius * Mathf.Sin(m_orbitAngle));
    }

    private void LateUpdate()
    {
        if (m_camera == null) return;
        // sprite forward(+Z)가 카메라를 향하도록 회전
        transform.rotation = Quaternion.LookRotation(m_camera.transform.position - transform.position, Vector3.up);
        // local Z축 자전 (화면상 회전처럼 보임)
        transform.Rotate(0f, 0f, m_rotationSpeed * Time.deltaTime, Space.Self);
    }
}
