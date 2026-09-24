using System.Collections;
using TMPro;
using UnityEngine;

// 서버 요청 대기 화면 — 입력 차단막과 스피너/메시지를 따로 켜고 끔. 팝업 스택에 쌓지 않고 UIManager.SetNetworkWaitState가 단일 인스턴스로 관리
public class UIPopupNetworkWait : UIPopupBase
{
    [SerializeField] private GameObject m_blockerRoot; // 전체 화면 투명 Raycast 대상 — 켜져 있는 동안 뒤 UI 입력 차단
    [SerializeField] private GameObject m_spinnerRoot; // 스피너 + 메시지 묶음
    [SerializeField] private RectTransform m_spinnerIcon; // 회전시키는 아이콘
    [SerializeField] private TMP_Text m_messageText;

    private const float k_spinDegreesPerSec = 360f;
    private Coroutine m_spinCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (m_messageText != null)
            m_messageText.text = LocalizationManager.Instance.Get("UIPopupMessage_NetworkWaiting");
    }

    // 오브젝트가 꺼지면 코루틴이 멈추므로 참조도 함께 비워 다음 표시에서 다시 시작되게 함
    private void OnDisable()
    {
        m_spinCoroutine = null;
    }

    public void SetState(bool blockInput, bool showSpinner)
    {
        if (m_blockerRoot != null)
            m_blockerRoot.SetActive(blockInput);
        if (m_spinnerRoot != null)
            m_spinnerRoot.SetActive(showSpinner);

        bool needSpin = showSpinner == true && gameObject.activeInHierarchy == true;
        if (needSpin == true && m_spinCoroutine == null)
        {
            m_spinCoroutine = StartCoroutine(Co_Spin());
        }
        else if (needSpin == false && m_spinCoroutine != null)
        {
            StopCoroutine(m_spinCoroutine);
            m_spinCoroutine = null;
        }
    }

    // 전투 배속(timeScale)과 무관하게 일정 속도로 회전
    private IEnumerator Co_Spin()
    {
        while (true)
        {
            if (m_spinnerIcon != null)
                m_spinnerIcon.Rotate(0f, 0f, -k_spinDegreesPerSec * Time.unscaledDeltaTime);
            yield return null;
        }
    }
}
