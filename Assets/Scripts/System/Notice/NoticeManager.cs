using System.Collections.Generic;

// 운영툴에서 등록한 공지사항 — 로그인 시 안 읽은 것을 순서대로 팝업으로 보여줌(ObjectManager.StartNormalPlay에서 1회 호출)
public class NoticeManager : MonoSingleton<NoticeManager>
{
    public void CheckAndShowNotices()
    {
        NetworkManager.Instance.GetNotice(response =>
        {
            if (response == null || response.errorCode != (int)ServerErrorCode.SUCCESS || response.data == null) return;
            if (response.data.notices == null || response.data.notices.Count == 0) return;

            EnqueueNotices(response.data.notices);
        });
    }

    // 서버 응답 자체가 "본 것으로 처리"까지 끝낸 상태라 여기서는 순서대로 띄우기만 하면 됨.
    // UIManager의 확인팝업 큐(ShowConfirmPopup)가 순차 표시를 이미 처리하므로 별도 큐 구현 불필요
    private void EnqueueNotices(List<NoticeItem> notices)
    {
        for (int i = 0; i < notices.Count; i++)
        {
            NoticeItem notice = notices[i];
            ConfirmPopupConfig config = new ConfirmPopupConfig
            {
                message = "<b>" + notice.title + "</b>\n\n",
                detailText = notice.body,
            }; // onCancel을 안 넣으면 취소 버튼이 자동으로 숨겨짐(확인만 있는 팝업)
            UIManager.Instance.ShowConfirmPopup(config);
        }
    }
}
