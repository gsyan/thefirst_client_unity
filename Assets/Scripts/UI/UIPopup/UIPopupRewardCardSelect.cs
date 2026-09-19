// 셀 클리어 보상(탐험 포인트/경험치 안내 + 보상카드 3택1)을 한 화면에서 처리하는 팝업 — 취소 없음
// 카드 클릭은 선택(하이라이트)만 하고, CONFIRM 버튼을 눌러야 실제 확정됨(오클릭 방지)
// 탈출 셀 클리어처럼 카드 후보가 없는 경우, 카드 섹션은 비우고 포인트/경험치 안내만 표시한 채 CONFIRM으로 바로 진행
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupRewardCardSelect : UIPopupBase
{
    [Header("Reward Card Select Popup")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private GameObject m_rewardSummaryContainer; // 탐험 포인트/경험치 안내 행 2개를 담은 부모 — 통째로 표시/숨김
    [SerializeField] private RowLabelValue m_rewardPointRow; // 라벨 좌측/값 우측 정렬(UIPopupConfirm의 REWARD 섹션 행과 동일한 RowLabelValue 재사용)
    [SerializeField] private RowLabelValue m_rewardExpRow;
    [SerializeField] private GameObject m_rewardCardButtonContainer; // 카드 버튼 3개를 담은 오브젝트 — 카드 후보가 없을 때(탈출 셀 등) 통째로 숨김
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private TMP_Text m_confirmButtonText;
    [SerializeField] private Button m_rerollButton; // 다시 뽑기(광고 시청 리롤) — 카드 후보가 없는 셀은 통째로 숨김
    [SerializeField] private TMP_Text m_rerollButtonText;

    private RewardCardButton[] m_cardButtons;
    private List<string> m_candidateCardIds;
    private int m_selectedIndex;
    private System.Action<string> m_onConfirmed;
    private int m_zoneNumber;
    private int m_cellRow;
    private int m_cellCol;
    private int m_rerollRemain; // 오늘 남은 리롤 가능 횟수 — 하루 총량 제한(예: 10회)만 있고 그 외 텀 제한은 없음

    protected override void Awake()
    {
        base.Awake();
        m_cardButtons = m_rewardCardButtonContainer.GetComponentsInChildren<RewardCardButton>(true);
        m_confirmButton.onClick.AddListener(OnConfirmClicked);

        if (m_confirmButtonText != null)
            CommonUtility.SetUILocText(m_confirmButtonText, "UI_Confirm");

        if (m_rerollButton != null)
            m_rerollButton.onClick.AddListener(OnRerollClicked);

        // 광고가 팝업이 열려있는 동안 재로딩 완료되면 리롤 버튼을 다시 켜야 함 — 리롤은 하루 총량 제한만 있어
        // 광고만 준비되면 연속으로 계속 쓸 수 있어야 하기 때문(풀에 보관되는 동안 비활성 상태에서 호출돼도 안전)
        EventManager.Subscribe_RewardedAdReadyChanged(OnRewardedAdReadyChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_RewardedAdReadyChanged(OnRewardedAdReadyChanged);
    }

    private void OnRewardedAdReadyChanged(bool isReady)
    {
        RefreshRerollButtonState();
    }

    public void ShowPopupRewardCardSelect(int explorationPointGained, int expGained, List<string> candidateCardIds, bool isEscapeCell, int zoneNumber, int cellRow, int cellCol, int rerollRemain, System.Action<string> onConfirmed)
    {
        base.ShowPopup();
        m_candidateCardIds = candidateCardIds;
        m_onConfirmed = onConfirmed;
        m_selectedIndex = -1;
        m_zoneNumber = zoneNumber;
        m_cellRow = cellRow;
        m_cellCol = cellCol;
        m_rerollRemain = rerollRemain;

        // 탈출 셀은 "탈출 지점 발견", 카드 후보 없이 포인트만 지급되는 트레저(전투 없는 이벤트 셀에서 탐험 포인트 당첨)는
        // "트레저 보상"으로 구분 — 카드 선택 문구("Choose a Reward Card")는 실제로 고를 카드가 있을 때만 맞는 표현이라
        // 카드 후보가 없으면 결과 안내형 타이틀을 써야 함. isEscapeCell은 호출부가 명시적으로 판정해서 넘김
        bool hasCardCandidates = candidateCardIds != null && candidateCardIds.Count > 0;
        string titleKey;
        if (isEscapeCell == true)
            titleKey = "UIPopupRewardCardSelect_EscapeTitle";
        else if (hasCardCandidates == false)
            titleKey = "UIPopupRewardCardSelect_TreasureTitle";
        else
            titleKey = "UIPopupRewardCardSelect_Title";
        CommonUtility.SetUILocText(m_titleText, titleKey);

        // 재접속 복구로 뜬 경우(포인트/경험치가 이미 반영되어 0으로 전달됨) 어색한 "+0" 문구 대신 요약 자체를 숨김
        bool hasRewardSummary = explorationPointGained > 0 || expGained > 0;
        m_rewardSummaryContainer.SetActive(hasRewardSummary);
        if (hasRewardSummary == true)
        {
            m_rewardPointRow.SetRow("UIPanelExplorationGrid_OwnedPoint", explorationPointGained.ToString(), rawValue: true);
            m_rewardExpRow.SetRow("UIPopupConfirm_ExpLabel", expGained.ToString(), rawValue: true);
        }

        m_rewardCardButtonContainer.SetActive(hasCardCandidates);
        if (hasCardCandidates == true)
        {
            BindCardButtons(candidateCardIds);

            // 생각 없이 CONFIRM만 눌러도 진행되도록 가운데 카드를 기본 선택 상태로 시작
            int defaultIndex = candidateCardIds.Count / 2;
            OnCardClicked(defaultIndex);
        }

        RefreshRerollButtonState();

        RefreshConfirmButtonState();
    }

    private void BindCardButtons(List<string> candidateCardIds)
    {
        DataTableRewardCard table = DataManager.Instance.m_dataTableRewardCard;
        for (int i = 0; i < m_cardButtons.Length; i++)
        {
            if (i >= candidateCardIds.Count)
            {
                m_cardButtons[i].Hide();
                continue;
            }

            int index = i;
            RewardCardData card = table.GetCard(candidateCardIds[index]);
            m_cardButtons[index].SetCard(card, () => OnCardClicked(index));
        }
    }

    private void OnCardClicked(int index)
    {
        m_selectedIndex = index;
        for (int i = 0; i < m_cardButtons.Length; i++)
            m_cardButtons[i].SetSelected(i == index);

        RefreshConfirmButtonState();
    }

    // 카드 후보가 있으면 하나를 선택해야만 CONFIRM 가능, 카드 후보가 없으면(탈출 셀 등) 바로 CONFIRM 가능
    private void RefreshConfirmButtonState()
    {
        bool hasCardCandidates = m_candidateCardIds != null && m_candidateCardIds.Count > 0;
        m_confirmButton.interactable = hasCardCandidates == false || m_selectedIndex >= 0;
    }

    private void OnConfirmClicked()
    {
        bool hasCardCandidates = m_candidateCardIds != null && m_candidateCardIds.Count > 0;
        string selectedCardId = (hasCardCandidates == true && m_selectedIndex >= 0) ? m_candidateCardIds[m_selectedIndex] : null;

        System.Action<string> callback = m_onConfirmed;
        HidePopup();
        callback?.Invoke(selectedCardId);
    }

    // 카드 후보가 없는 셀(탈출/트레저 등)에서만 버튼 자체를 숨김 — 그 외엔 항상 보이고, 남은 횟수/광고 준비 여부는
    // interactable로만 표현(0/n처럼 소진 상태도 그대로 보여줘야 함). VIP는 광고 없이 항상 가능, 비VIP는 광고 준비 시에만 가능
    private void RefreshRerollButtonState()
    {
        if (m_rerollButton == null) return;
        bool hasCardCandidates = m_candidateCardIds != null && m_candidateCardIds.Count > 0;
        m_rerollButton.gameObject.SetActive(hasCardCandidates);
        if (hasCardCandidates == false) return;

        bool hasRerollsLeft = m_rerollRemain > 0;
        bool canReroll = hasRerollsLeft == true && (IAPManager.Instance.IsVipActive() == true || AdManager.Instance.IsRewardedAdReady == true);
        m_rerollButton.interactable = canReroll;

        if (m_rerollButtonText != null)
        {
            int rerollLimit = DataManager.Instance.m_dataTableConfig.gameSettings.exploration.rewardCardRerollLimit;
            m_rerollButtonText.text = $"{LocalizationManager.Instance.Get("UIPopupRewardCardSelect_RerollButton")} {m_rerollRemain}/{rerollLimit}";
        }
    }

    private void OnRerollClicked()
    {
        bool isVip = IAPManager.Instance.IsVipActive();
        if (m_rerollRemain <= 0) return;
        if (isVip == false && AdManager.Instance.IsRewardedAdReady == false) return;

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get(isVip == true ? "UIPopupRewardCardSelect_RerollConfirmVip" : "UIPopupRewardCardSelect_RerollConfirm"),
            confirmText1 = isVip == true ? LocalizationManager.Instance.Get("UI_Confirm") : LocalizationManager.Instance.Get("UI_WatchAD"),
            onConfirm = isVip == true ? RequestReroll : RequestRerollWithAd,
            onCancel = () => { }
        });
    }

    private void RequestReroll()
    {
        RerollRewardCardRequest request = new RerollRewardCardRequest
        {
            zoneNumber = m_zoneNumber,
            cellRow = m_cellRow,
            cellCol = m_cellCol,
        };
        NetworkManager.Instance.RerollRewardCard(request, OnRerollResponse);
    }

    private void RequestRerollWithAd()
    {
        AdManager.Instance.ShowRewardedAd(result =>
        {
            if (result != EAdResult.Rewarded) return;
            RequestReroll();
        });
    }

    private void OnRerollResponse(ApiResponse<RerollRewardCardResponse> response)
    {
        if (response.errorCode == (int)ServerErrorCode.EXPLORATION_REWARD_CARD_REROLL_LIMIT_EXCEEDED)
        {
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message = LocalizationManager.Instance.Get("UIPopupRewardCardSelect_RerollLimitReached"),
                onConfirm = () => { },
            });
            return;
        }

        if (response.errorCode != 0 || response.data == null) return;

        m_candidateCardIds = response.data.rewardCardCandidates;
        m_rerollRemain = response.data.rerollRemain;
        BindCardButtons(m_candidateCardIds);
        OnCardClicked(m_candidateCardIds.Count / 2); // 최초 표시 때와 동일하게 가운데 카드를 기본 선택 상태로
        RefreshRerollButtonState();
    }
}
