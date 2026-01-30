using UnityEngine;
using UnityEngine.EventSystems;

// 튜토리얼 대상 UI에 부착하여 클릭 감지
public class TutorialClickHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string m_tutorialTargetId;

    public string TargetId => m_tutorialTargetId;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (TutorialManager.Instance == null) return;
        if (!TutorialManager.Instance.IsPlaying) return;

        TutorialManager.Instance.OnTargetClicked(m_tutorialTargetId);
    }

    // 런타임에서 ID 설정
    public void SetTargetId(string targetId)
    {
        m_tutorialTargetId = targetId;
    }
}
