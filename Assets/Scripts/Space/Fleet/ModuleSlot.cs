// 모듈 슬롯 위치/타입 정보 및 해당 슬롯 선택 시 카메라 목표값 보유
using UnityEngine;

public class ModuleSlot : MonoBehaviour
{
    public ModuleSlotInfo m_moduleSlotInfo = new ModuleSlotInfo();

    // 이 슬롯 선택 시 카메라가 이동할 목표 회전/줌 값 (인스펙터에서 직접 편집 가능)
    public float m_cameraRotationY = 0f;
    public float m_cameraRotationX = 0f;
    public float m_cameraZoom = 800f;
}
