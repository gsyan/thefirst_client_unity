using System.Collections.Generic;

// 운영툴에서 등록한 공지사항 — 로그인 시 안 읽은 공지가 있으면 공지 팝업을 자동으로 엶(ObjectManager.StartNormalPlay에서 1회 호출)
// 읽음 = 현재 버전 본문을 열어본 것(서버 기록). 안 읽은 공지 유무는 공지 버튼 레드닷으로 알림
public class NoticeManager : MonoSingleton<NoticeManager>
{
    private const string FALLBACK_LOCALE_CODE = "en";

    private bool m_hasUnread;

    public bool HasUnread()
    {
        return m_hasUnread;
    }

    // 안 읽은 공지가 1개면 그 본문을, 2개 이상이면 목록을 바로 보여줌
    public void CheckAndShowNotices()
    {
        NetworkManager.Instance.GetNoticeList(response =>
        {
            if (response == null || response.errorCode != (int)ServerErrorCode.SUCCESS || response.data == null) return;

            List<NoticeItem> notices = response.data.notices;
            int unreadCount = CountUnread(notices);
            SetHasUnread(unreadCount > 0);
            if (unreadCount == 0) return;

            UIManager.Instance.ShowNoticePopup(notices, true);
        });
    }

    // 공지 팝업이 목록을 새로 받았을 때 레드닷 동기화
    public void ApplyNoticeList(List<NoticeItem> notices)
    {
        SetHasUnread(CountUnread(notices) > 0);
    }

    // 본문을 열었을 때 — 로컬 상태를 먼저 읽음으로 바꿔 레드닷을 즉시 끄고, 서버 응답의 안 읽은 공지 유무로 다시 맞춤
    public void MarkRead(NoticeItem notice, List<NoticeItem> notices)
    {
        if (notice == null || notice.isRead == true) return;

        notice.isRead = true;
        SetHasUnread(CountUnread(notices) > 0);

        NoticeReadRequest request = new NoticeReadRequest { noticeId = notice.noticeId, version = notice.version };
        NetworkManager.Instance.MarkNoticeRead(request, response =>
        {
            if (response == null || response.errorCode != (int)ServerErrorCode.SUCCESS || response.data == null) return;
            SetHasUnread(response.data.hasUnread);
        });
    }

    public static int CountUnread(List<NoticeItem> notices)
    {
        if (notices == null) return 0;

        int unreadCount = 0;
        for (int i = 0; i < notices.Count; i++)
        {
            NoticeItem notice = notices[i];
            if (notice != null && notice.isRead == false)
                unreadCount++;
        }
        return unreadCount;
    }

    private void SetHasUnread(bool hasUnread)
    {
        m_hasUnread = hasUnread;
        EventManager.Trigger_NoticeUnreadChanged(hasUnread);
    }

    // 현재 언어 본문 → 없으면 영어 → 그것도 없으면 첫 번째 본문
    public static NoticeText PickText(NoticeItem notice)
    {
        if (notice == null || notice.texts == null || notice.texts.Count == 0) return null;

        string currentLocaleCode = LocalizationManager.Instance.GetCurrentLocaleCode();
        NoticeText currentLocaleText = FindText(notice.texts, currentLocaleCode);
        if (currentLocaleText != null) return currentLocaleText;

        NoticeText fallbackText = FindText(notice.texts, FALLBACK_LOCALE_CODE);
        if (fallbackText != null) return fallbackText;

        return notice.texts[0];
    }

    private static NoticeText FindText(List<NoticeText> texts, string localeCode)
    {
        if (string.IsNullOrEmpty(localeCode) == true) return null;

        for (int i = 0; i < texts.Count; i++)
        {
            NoticeText text = texts[i];
            if (text != null && text.locale == localeCode) return text;
        }
        return null;
    }
}
