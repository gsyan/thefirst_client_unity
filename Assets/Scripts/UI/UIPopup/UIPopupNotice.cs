// 공지 팝업 — 제목 목록(ListView)에서 제목을 누르면 본문(DetailView)으로 전환, 뒤로가기로 목록 복귀
// 공지 버튼으로 열면 서버 /notice/list를 새로 받고, 로그인 시 자동 표시는 NoticeManager가 받은 목록을 그대로 씀. 본문을 열면 읽음 처리
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupNotice : UIPopupBase
{
    [SerializeField] private TMP_Text   m_titleText;
    [SerializeField] private Button     m_closeButton;
    [SerializeField] private Button     m_backgroundButton;

    [Header("목록")]
    [SerializeField] private GameObject m_listView;
    [SerializeField] private ScrollRect m_listScrollView;
    [SerializeField] private Transform  m_rowContainer;
    [SerializeField] private UINoticeRow m_rowTemplate; // 비활성 상태로 둔 행 원본 — 공지 수만큼 복제해서 재사용
    [SerializeField] private TMP_Text   m_emptyText;

    [Header("본문")]
    [SerializeField] private GameObject m_detailView;
    [SerializeField] private TMP_Text   m_detailTitleText;
    [SerializeField] private TMP_Text   m_detailBodyText;
    [SerializeField] private ScrollRect m_detailScrollView;
    [SerializeField] private Button     m_backButton;

    private readonly List<UINoticeRow> m_rows = new List<UINoticeRow>();
    private List<NoticeItem> m_notices;
    private System.Action m_onClose;
    private int m_requestSerial; // 팝업을 닫았다 다시 연 뒤 이전 요청 응답이 늦게 오면 무시하기 위한 번호

    protected override void Awake()
    {
        base.Awake();
        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(OnCloseClicked);
        if (m_backgroundButton != null)
            m_backgroundButton.onClick.AddListener(OnCloseClicked);
        if (m_backButton != null)
            m_backButton.onClick.AddListener(OnBackClicked);
        if (m_rowTemplate != null)
            m_rowTemplate.gameObject.SetActive(false);

        TMP_Text closeButtonText = m_closeButton != null ? m_closeButton.GetComponentInChildren<TMP_Text>() : null;
        if (closeButtonText != null)
            CommonUtility.SetUILocText(closeButtonText, "UI_Close");
        TMP_Text backButtonText = m_backButton != null ? m_backButton.GetComponentInChildren<TMP_Text>() : null;
        if (backButtonText != null)
            CommonUtility.SetUILocText(backButtonText, "UI_Back");
    }

    public void ShowPopupNotice(List<NoticeItem> notices, bool autoOpenSingleUnread, System.Action onClose)
    {
        m_onClose = onClose;
        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UI_Notice");

        m_notices = null;
        HideAllRows();
        if (m_emptyText != null)
            m_emptyText.gameObject.SetActive(false);
        ShowListView();
        base.ShowPopup();

        m_requestSerial++;
        if (notices != null)
        {
            OnNoticesLoaded(notices, autoOpenSingleUnread);
            return;
        }

        int requestSerial = m_requestSerial;
        NetworkManager.Instance.GetNoticeList(response => OnNoticeListResponse(response, requestSerial, autoOpenSingleUnread));
    }

    private void OnNoticeListResponse(ApiResponse<NoticeResponse> response, int requestSerial, bool autoOpenSingleUnread)
    {
        if (requestSerial != m_requestSerial) return;
        if (gameObject.activeInHierarchy == false) return;

        bool isSuccess = response != null && response.errorCode == (int)ServerErrorCode.SUCCESS && response.data != null;
        List<NoticeItem> notices = isSuccess == true ? response.data.notices : null;
        if (isSuccess == true)
            NoticeManager.Instance.ApplyNoticeList(notices);
        OnNoticesLoaded(notices, autoOpenSingleUnread);
    }

    private void OnNoticesLoaded(List<NoticeItem> notices, bool autoOpenSingleUnread)
    {
        m_notices = notices;
        BindRows();

        if (autoOpenSingleUnread == false) return;
        if (NoticeManager.CountUnread(m_notices) != 1) return;

        int unreadIndex = FindFirstUnreadIndex();
        if (unreadIndex >= 0)
            OpenDetail(unreadIndex);
    }

    private int FindFirstUnreadIndex()
    {
        if (m_notices == null) return -1;

        for (int i = 0; i < m_notices.Count; i++)
        {
            if (m_notices[i] != null && m_notices[i].isRead == false)
                return i;
        }
        return -1;
    }

    private void BindRows()
    {
        int noticeCount = m_notices != null ? m_notices.Count : 0;
        EnsureRowCount(noticeCount);

        for (int i = 0; i < m_rows.Count; i++)
        {
            bool isUsed = i < noticeCount;
            m_rows[i].gameObject.SetActive(isUsed);
            if (isUsed == false) continue;

            NoticeItem notice = m_notices[i];
            NoticeText text = NoticeManager.PickText(notice);
            string title = text != null ? text.title : string.Empty;
            bool isRead = notice == null || notice.isRead == true;
            m_rows[i].Bind(i, title, isRead, OnRowClicked);
        }

        if (m_emptyText != null)
        {
            m_emptyText.gameObject.SetActive(noticeCount == 0);
            if (noticeCount == 0)
                CommonUtility.SetUILocText(m_emptyText, "UIPopupNotice_Empty");
        }

        RebuildAndScrollTop(m_rowContainer as RectTransform, m_listScrollView);
    }

    // 행은 필요한 만큼만 늘리고 줄이지 않음 — 다시 열 때는 기존 행을 재사용
    private void EnsureRowCount(int count)
    {
        if (m_rowTemplate == null || m_rowContainer == null) return;

        while (m_rows.Count < count)
        {
            UINoticeRow row = Instantiate(m_rowTemplate, m_rowContainer);
            m_rows.Add(row);
        }
    }

    private void HideAllRows()
    {
        for (int i = 0; i < m_rows.Count; i++)
            m_rows[i].gameObject.SetActive(false);
    }

    private void OnRowClicked(int index)
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        OpenDetail(index);
    }

    private void OpenDetail(int index)
    {
        if (m_notices == null || index < 0 || index >= m_notices.Count) return;

        NoticeItem notice = m_notices[index];
        NoticeText text = NoticeManager.PickText(notice);
        if (text == null) return;

        if (m_detailTitleText != null)
            m_detailTitleText.text = text.title;
        if (m_detailBodyText != null)
            m_detailBodyText.text = text.body;
        ShowDetailView();

        RectTransform bodyParentRect = m_detailBodyText != null ? m_detailBodyText.transform.parent as RectTransform : null;
        RebuildAndScrollTop(bodyParentRect, m_detailScrollView);

        NoticeManager.Instance.MarkRead(notice, m_notices);
        if (index < m_rows.Count)
            m_rows[index].SetRead(true);
    }

    private void OnBackClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        ShowListView();
    }

    private void ShowListView()
    {
        if (m_listView != null) m_listView.SetActive(true);
        if (m_detailView != null) m_detailView.SetActive(false);
    }

    private void ShowDetailView()
    {
        if (m_listView != null) m_listView.SetActive(false);
        if (m_detailView != null) m_detailView.SetActive(true);
    }

    private void RebuildAndScrollTop(RectTransform contentRect, ScrollRect scrollView)
    {
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (scrollView != null)
            scrollView.verticalNormalizedPosition = 1f;
    }

    private void OnCloseClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClose != null)
            m_onClose.Invoke();
    }
}
