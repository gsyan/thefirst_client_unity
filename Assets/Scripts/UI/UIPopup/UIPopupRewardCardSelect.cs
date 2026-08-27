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
    [SerializeField] private TMP_Text m_rewardSummaryText; // "탐험 포인트 / 경험치" 안내 — 줄마다 라벨\t값 형태, 좌/우 정렬은 TMP 탭 스톱(Tab Size) 설정에 의존
    [SerializeField] private GameObject m_rewardCardButtonContainer; // 카드 버튼 3개를 담은 오브젝트 — 카드 후보가 없을 때(탈출 셀 등) 통째로 숨김
    [SerializeField] private Button m_confirmButton;

    private RewardCardButton[] m_cardButtons;
    private List<string> m_candidateCardIds;
    private int m_selectedIndex;
    private System.Action<string> m_onConfirmed;

    protected override void Awake()
    {
        base.Awake();
        m_cardButtons = m_rewardCardButtonContainer.GetComponentsInChildren<RewardCardButton>(true);
        m_confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupRewardCardSelect(int explorationPointGained, int expGained, List<string> candidateCardIds, System.Action<string> onConfirmed)
    {
        base.ShowPopup();
        m_candidateCardIds = candidateCardIds;
        m_onConfirmed = onConfirmed;
        m_selectedIndex = -1;

        // 카드 후보가 없는 경우(탈출 셀)는 일반 셀 클리어와 다른 타이틀로 구분 — "탈출 지점 발견"
        bool hasCardCandidates = candidateCardIds != null && candidateCardIds.Count > 0;
        string titleKey = hasCardCandidates == true ? "UIPopupRewardCardSelect_Title" : "UIPopupRewardCardSelect_EscapeTitle";
        CommonUtility.SetUILocText(m_titleText, titleKey);

        // 재접속 복구로 뜬 경우(포인트/경험치가 이미 반영되어 0으로 전달됨) 어색한 "+0" 문구 대신 요약 자체를 숨김
        bool hasRewardSummary = explorationPointGained > 0 || expGained > 0;
        m_rewardSummaryText.gameObject.SetActive(hasRewardSummary);
        if (hasRewardSummary == true)
        {
            string pointLabel = LocalizationManager.Instance.Get("UIPanelExplorationGrid_OwnedPoint");
            string expLabel = LocalizationManager.Instance.Get("UIPopupConfirm_ExpLabel");
            m_rewardSummaryText.text = $"{pointLabel}\t{explorationPointGained}\n{expLabel}\t{expGained}";
        }

        m_rewardCardButtonContainer.SetActive(hasCardCandidates);
        if (hasCardCandidates == true)
            BindCardButtons(candidateCardIds);

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
}
