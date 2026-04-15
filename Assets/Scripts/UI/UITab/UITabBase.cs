using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabBase : MonoBehaviour
{
    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystemParent;

    [SerializeField] private Button m_closeButton;
    [SerializeField] private TMP_Text m_textResult;
    private Coroutine m_textResultCoroutine;

    // UIPanelSpace 초기화 시 m_tabSystemParent 설정 후 호출
    public void InitializeCloseButton()
    {
        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(() => m_tabSystemParent?.SwitchToTab(-1));
    }

    virtual public void InitializeUITab()
    {

    }

    virtual public void OnTabActivated()
    {
        ResetResultMessage();
    }

    virtual public void OnTabDeactivated()
    {
        
    }


    // m_textResult에 텍스트를 표시하고 n초 후 자동으로 사라지게 합니다.
    protected void ShowResultMessage(string message, float displayDuration = 3f)
    {
        if (m_textResult == null) return;

        // 이전 코루틴이 실행 중이면 중지
        if (m_textResultCoroutine != null)
            StopCoroutine(m_textResultCoroutine);

        // 메시지 표시 및 자동 사라지기 코루틴 시작
        m_textResultCoroutine = StartCoroutine(ShowResultMessageCoroutine(message, displayDuration));
    }

    protected IEnumerator ShowResultMessageCoroutine(string message, float displayDuration)
    {
        // 메시지 표시
        m_textResult.text = message;

        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(displayDuration);

        ResetResultMessage();
    }

    private void ResetResultMessage()
    {
        // 메시지 제거
        m_textResult.text = "";
        m_textResultCoroutine = null;
    }
}

