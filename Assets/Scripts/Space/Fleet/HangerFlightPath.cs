// 함체 프리팹 HangerSlot 자식에 배치 — 함재기 사출/귀환 경로 웨이포인트 정의
// LaunchPath/ReturnPath 각 자식 Transform 순서 = 웨이포인트 순서
using UnityEngine;

public class HangerFlightPath : MonoBehaviour
{
    [SerializeField] private Transform m_launchPath;
    [SerializeField] private Transform m_returnPath;

    // 사출 경로 컨테이너 (자식 = WP, 매 프레임 현재 월드 좌표로 읽어야 함)
    public Transform LaunchPath => m_launchPath;

    // 귀환 경로 컨테이너 (자식 = WP, 매 프레임 현재 월드 좌표로 읽어야 함)
    public Transform ReturnPath => m_returnPath;
}
