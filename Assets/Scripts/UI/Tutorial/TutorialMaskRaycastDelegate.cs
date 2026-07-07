using UnityEngine;
using UnityEngine.UI;

// FullScreenButton이 TutorialMask의 자식이 아니어도(형제 등) 동일한 hole 통과 판정을 받도록 위임
// — TutorialMask 전체를 부모로 두면 dim 꺼짐 등과 무관하게 항상 켜둘 수 없는 문제가 있어 별도 컴포넌트로 분리
public class TutorialMaskRaycastDelegate : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField] private TutorialMask m_mask;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        return m_mask == null || m_mask.IsRaycastLocationValid(screenPoint, eventCamera);
    }
}
