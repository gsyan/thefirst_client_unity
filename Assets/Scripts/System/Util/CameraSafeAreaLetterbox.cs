//------------------------------------------------------------------------------
// 곡면(엣지) 디스플레이에서 카메라 렌더 영역을 고정 % 마진만큼 깎아
// 바깥쪽은 Clear Flags(Solid Color, 검정)로 남기고 안쪽 사각형만 사용
//------------------------------------------------------------------------------
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSafeAreaLetterbox : MonoBehaviour
{
    [Header("곡면 화면 대비 추가 마진 (0~0.5, 화면 크기 대비 비율)")]
    [SerializeField] private float m_marginLeft;
    [SerializeField] private float m_marginRight;
    [SerializeField] private float m_marginTop;
    [SerializeField] private float m_marginBottom;

    private Camera m_camera;
    private int m_lastScreenWidth;
    private int m_lastScreenHeight;

    private void Awake()
    {
        m_camera = GetComponent<Camera>();
        ApplyViewport();
    }

    private void Update()
    {
        if (m_lastScreenWidth != Screen.width || m_lastScreenHeight != Screen.height)
            ApplyViewport();
    }

    private void ApplyViewport()
    {
        m_lastScreenWidth = Screen.width;
        m_lastScreenHeight = Screen.height;

        float x = m_marginLeft;
        float y = m_marginBottom;
        float width = 1f - m_marginLeft - m_marginRight;
        float height = 1f - m_marginTop - m_marginBottom;

        m_camera.rect = new Rect(x, y, width, height);
    }
}
