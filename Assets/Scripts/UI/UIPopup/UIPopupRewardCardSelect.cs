// 셀 클리어 보상(광물/경험치 안내 + 보상카드 구매) 통합 팝업
// 카드 클릭 = 즉시 구매(별도 확인창 없음) — 구매할수록 그 카드 비용이 올라가고, 구매 직후 후보 3개가 다시 추첨됨
// CONFIRM 버튼은 "닫기" 역할 — 몇 장을 구매했든, 하나도 안 샀든 언제든 눌러서 다음으로 진행 가능(스킵 겸용)
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIPopupRewardCardSelect : UIPopupBase
{
    [Header("Reward Card Select Popup")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private GameObject m_totalMineralContainer; // TotoalMineralContainer — 탈출 셀은 광물이 아니라 경험치가 보상이라 이 컨테이너 자체를 숨김
    [SerializeField] private RowLabelValue m_totalMineralRow; // 우측 상단 — 보유 광물 현재치 상시 표시(구매할 때마다 갱신)
    [FormerlySerializedAs("m_expGainedRow")]
    [FormerlySerializedAs("m_mineralGainedRow")]
    [SerializeField] private RowLabelValue m_rewardAmountRow; // 일반 셀 클리어는 이번에 얻은 광물, 탈출 셀은 그 존의 탈출 보상 경험치(미리보기) — 팝업이 뜰 때 1회만 표시, 이후 구매와 무관하게 갱신 안 함. 0이면 행 자체를 숨김(BuyCardButton으로 연 경우 등)
    [SerializeField] private GameObject m_rewardCardButtonContainer; // 카드 버튼 3개를 담은 오브젝트 — 카드 후보가 없을 때(탈출 셀 등) 통째로 숨김
    [SerializeField] private Button m_confirmButton; // 닫기 버튼 — 이름은 과거(확정) 흐름의 잔재, 역할은 "닫고 다음으로 진행"
    [SerializeField] private TMP_Text m_confirmButtonText;
    [SerializeField] private Button m_rerollButton; // 카드 후보 다시 뽑기 — 카드 후보가 없는 셀은 통째로 숨김
    [SerializeField] private TMP_Text m_rerollButtonText;

    private RewardCardButton[] m_cardButtons;
    private List<string> m_candidateCardIds;
    private int[] m_candidateCosts; // m_candidateCardIds와 같은 인덱스 — 구매 시점의 비용을 그대로 서버에 보고하기 위해 클릭 시점 재계산 대신 바인딩 시점 값을 저장
    private int m_zoneNumber;
    private int m_cellRow;
    private int m_cellCol;
    private int m_rerollCharge; // 현재 리롤 가능 횟수(충전식, 계정 전체 공유) — 0이면 리롤 버튼 클릭 시 광고 충전 흐름으로 전환
    private System.Action m_onClosed;

    private const float k_cardEnterStagger = 0.07f; // 새 카드가 왼쪽부터 순차적으로 피어나는 간격(초)
    private int m_displayedTotalMineral; // 보유 광물 롤링 연출의 시작값 — 마지막으로 화면에 표시한 값
    private float m_cardExitEndTime; // 카드 퇴장 연출이 끝나는 시각(unscaled) — 서버 응답이 먼저 와도 이 시각 전엔 새 카드를 바인딩하지 않음
    private Coroutine m_rebindCoroutine;

    protected override void Awake()
    {
        base.Awake();
        m_cardButtons = m_rewardCardButtonContainer.GetComponentsInChildren<RewardCardButton>(true);
        m_confirmButton.onClick.AddListener(OnCloseClicked);

        if (m_confirmButtonText != null)
            CommonUtility.SetUILocText(m_confirmButtonText, "UI_Close");

        if (m_rerollButton != null)
            m_rerollButton.onClick.AddListener(OnRerollClicked);
    }

    public void ShowPopupRewardCardSelect(int rewardAmount, List<string> candidateCardIds, bool isEscapeCell, int zoneNumber, int cellRow, int cellCol, int rerollRemain, System.Action onClosed)
    {
        base.ShowPopup();
        // 이전에 열렸다 닫힌 팝업의 대기 중 재바인딩이 남아있으면 버림(비활성화로 코루틴 자체는 이미 멈춰있음)
        if (m_rebindCoroutine != null)
        {
            StopCoroutine(m_rebindCoroutine);
            m_rebindCoroutine = null;
        }
        m_cardExitEndTime = 0f;
        m_candidateCardIds = candidateCardIds;
        m_onClosed = onClosed;
        m_zoneNumber = zoneNumber;
        m_cellRow = cellRow;
        m_cellCol = cellCol;
        m_rerollCharge = rerollRemain;

        // 탈출 셀은 "탈출 지점 발견", 카드 후보 없이 광물만 지급되는 트레저(전투 없는 이벤트 셀에서 광물 당첨)는
        // "트레저 보상"으로 구분 — 카드 구매 문구("Buy a Reward Card")는 실제로 살 카드가 있을 때만 맞는 표현이라
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

        // 탈출 셀은 보상이 광물이 아니라 경험치라 보유 광물 표시가 의미 없음 — 컨테이너 자체를 숨김
        if (m_totalMineralContainer != null)
            m_totalMineralContainer.SetActive(isEscapeCell == false);
        if (isEscapeCell == false)
            RefreshTotalMineralDisplay(false);

        // 이번 셀 클리어 보상 안내는 팝업이 뜨는 시점에 1회만 표시 — 카드를 구매해도 바뀌지 않으므로 갱신하지 않음
        // 탈출 셀은 광물이 아니라 경험치 라벨을 씀(값 자체는 호출부가 이미 올바른 의미로 넘겨줌)
        if (m_rewardAmountRow != null)
        {
            m_rewardAmountRow.gameObject.SetActive(rewardAmount > 0);
            if (rewardAmount > 0)
            {
                string rewardLabelKey = isEscapeCell == true ? "UIPopupConfirm_ExpLabel" : "UIPopupRewardCardSelect_MineralGainedLabel";
                m_rewardAmountRow.SetRow(rewardLabelKey, rewardAmount.ToString(), rawValue: true);
            }
        }

        m_rewardCardButtonContainer.SetActive(hasCardCandidates);
        if (hasCardCandidates == true)
        {
            BindCardButtons(candidateCardIds);
            SetCardButtonsBusy(false); // 이전에 응답 대기 중 닫혔던 경우 잠긴 상태가 남아있을 수 있음
        }

        RefreshRerollButtonState();
    }

    private CommanderInfo GetCommanderInfo()
    {
        return DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
    }

    // 보유 광물 상시 표시 — 팝업이 뜰 때는 즉시, 구매 직후엔 이전값→새값 롤링(animate=true)으로 갱신
    private void RefreshTotalMineralDisplay(bool animate)
    {
        CommanderInfo info = GetCommanderInfo();
        int mineralAmount = info != null ? info.mineralAmount : 0;
        if (animate == true)
            m_totalMineralRow.SetValueAnimated(m_displayedTotalMineral, mineralAmount, RebuildTotalMineralLayout);
        else
            m_totalMineralRow.SetRow("UIPanelExplorationGrid_OwnedPoint", CommonUtility.FormatNumber(mineralAmount), rawValue: true);
        m_displayedTotalMineral = mineralAmount;
        RebuildTotalMineralLayout();
    }

    // 자릿수가 바뀌면 이 행의 폭이 바뀌는데, 팝업이 뜬 직후/구매 직후엔 그 변화가 같은 프레임에 반영 안 될 수 있어 강제로 재계산(롤링이 끝난 뒤에도 한 번 더)
    private void RebuildTotalMineralLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_totalMineralRow.transform as RectTransform);
    }

    private void BindCardButtons(List<string> candidateCardIds)
    {
        DataTableRewardCard table = DataManager.Instance.m_dataTableRewardCard;
        RewardCardSessionState sessionState = ObjectManager.Instance.m_rewardCardSessionState;
        m_candidateCosts = new int[m_cardButtons.Length];

        for (int i = 0; i < m_cardButtons.Length; i++)
        {
            if (candidateCardIds == null || i >= candidateCardIds.Count)
            {
                m_cardButtons[i].Hide();
                continue;
            }

            int index = i;
            RewardCardData card = table.GetCard(candidateCardIds[index]);
            int cost = card != null ? sessionState.GetCurrentCost(card) : 0;
            m_candidateCosts[index] = cost;
            m_cardButtons[index].SetCard(card, cost, () => OnCardBuyClicked(index));
            float enterDelay = k_cardEnterStagger * index;
            m_cardButtons[index].PlayEnter(enterDelay);
        }

        RefreshCardAffordability();
    }

    // 현재 카드들 퇴장 연출 시작 — purchasedIndex는 구매한 카드(-1이면 다시 뽑기 등 구매 아님)
    private void PlayCardsExit(int purchasedIndex)
    {
        float maxDuration = 0f;
        for (int i = 0; i < m_cardButtons.Length; i++)
        {
            if (m_candidateCardIds == null || i >= m_candidateCardIds.Count) continue;
            float duration = m_cardButtons[i].PlayExit(i == purchasedIndex);
            maxDuration = Mathf.Max(maxDuration, duration);
        }
        m_cardExitEndTime = Time.unscaledTime + maxDuration;
    }

    // 서버 응답(새 후보 또는 실패 시 기존 후보)을 퇴장 연출이 끝난 뒤에 바인딩 — 응답이 연출보다 빨라도 줄어드는 도중에 내용이 바뀌지 않게
    private void RebindCardsAfterExit(List<string> candidateCardIds)
    {
        // 응답이 오기 전에 팝업이 닫혔으면 코루틴을 시작할 수 없음 — 다음에 열릴 때 ShowPopupRewardCardSelect가 새로 바인딩함
        if (gameObject.activeInHierarchy == false) return;

        if (m_rebindCoroutine != null)
            StopCoroutine(m_rebindCoroutine);
        m_rebindCoroutine = StartCoroutine(Co_RebindCardsAfterExit(candidateCardIds));
    }

    private IEnumerator Co_RebindCardsAfterExit(List<string> candidateCardIds)
    {
        while (Time.unscaledTime < m_cardExitEndTime)
            yield return null;

        m_rebindCoroutine = null;
        m_candidateCardIds = candidateCardIds;
        BindCardButtons(candidateCardIds);
        SetCardButtonsBusy(false);
    }

    // 보유 광물이 바뀔 때마다(구매 직후) 3개 버튼 전부 다시 체크 — 버튼은 항상 눌리게 두고 비용 텍스트 색상만 바꿔서 부족함을 표시(클릭은 막지 않음)
    private void RefreshCardAffordability()
    {
        if (m_candidateCardIds == null) return;

        CommanderInfo info = GetCommanderInfo();
        int mineralAmount = info != null ? info.mineralAmount : 0;
        for (int i = 0; i < m_cardButtons.Length; i++)
        {
            if (i >= m_candidateCardIds.Count) continue;
            m_cardButtons[i].SetAffordable(mineralAmount >= m_candidateCosts[i]);
        }
    }

    // 서버가 새 후보를 내려줄 때까지만 버튼을 잠금(중복 구매 클릭 방지) — 광물 부족과는 별개의 이유라 Button.interactable을 직접 건드림
    private void SetCardButtonsBusy(bool isBusy)
    {
        for (int i = 0; i < m_cardButtons.Length; i++)
            m_cardButtons[i].SetInteractable(isBusy == false);

        if (isBusy == false)
            RefreshCardAffordability();
    }

    private void OnCardBuyClicked(int index)
    {
        if (m_candidateCardIds == null || index >= m_candidateCardIds.Count) return;

        string cardId = m_candidateCardIds[index];
        RewardCardData card = DataManager.Instance.m_dataTableRewardCard.GetCard(cardId);
        if (card == null) return;

        int cost = m_candidateCosts[index];
        CommanderInfo info = GetCommanderInfo();
        int mineralAmount = info != null ? info.mineralAmount : 0;
        if (info == null || mineralAmount < cost)
        {
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message = string.Format(LocalizationManager.Instance.Get("RewardCardButton_InsufficientMineral"), mineralAmount, cost),
                onConfirm = () => { },
            });
            return;
        }

        info.mineralAmount -= cost;
        EventManager.Trigger_MineralAmountChanged(info.mineralAmount);
        RefreshTotalMineralDisplay(true);

        ObjectManager.Instance.m_rewardCardSessionState.IncrementPurchaseCount(cardId);
        ApplyCardEffect(card, cost);

        SetCardButtonsBusy(true); // 서버가 새 후보를 내려줄 때까지 중복 구매 클릭 방지
        PlayCardsExit(index); // 구매한 카드는 빨려들 듯 사라지고 나머지는 페이드아웃 — 새 후보는 응답 후 피어나듯 등장
        PurchaseRewardCardRequest request = new PurchaseRewardCardRequest
        {
            zoneNumber = m_zoneNumber,
            cellRow = m_cellRow,
            cellCol = m_cellCol,
            cardId = cardId,
            mineralSpent = cost,
        };
        NetworkManager.Instance.PurchaseRewardCard(request, OnPurchaseResponse);
    }

    // 지속버프는 세션 상태에 누적하고 즉시 함대에 반영, 즉시효과(회복 등)는 바로 적용 — UIPanelExplorationGrid.ApplySelectedRewardCard와 동일한 분기
    // mineralSpent: 이번 구매에 실제로 지불한 비용 — Buff_MineralFlatBonus는 "지불한 만큼 향후 획득량에 가산"이라 card.value1 대신 이 값을 그대로 적용값으로 씀
    private void ApplyCardEffect(RewardCardData card, int mineralSpent)
    {
        if (card.isPersistent == true)
        {
            float? appliedValue = card.effectType == ECardEffectType.Buff_MineralFlatBonus ? (float?)mineralSpent : null;
            ObjectManager.Instance.m_rewardCardSessionState.ApplyCard(card, appliedValue);
            ObjectManager.Instance.RefreshRewardCardBuffsOnMyFleet();
            return;
        }

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        if (card.effectType == ECardEffectType.Instant_HealthHeal)
            myFleet.HealAllShipsByRatio(card.value1);
        else if (card.effectType == ECardEffectType.Instant_ShieldHeal)
            myFleet.HealShieldAllShipsByRatio(card.value1);
        else if (card.effectType == ECardEffectType.Instant_InterceptorHeal)
            myFleet.HealInterceptorAllShipsByRatio(card.value1);
    }

    private void OnPurchaseResponse(ApiResponse<PurchaseRewardCardResponse> response)
    {
        if (response.errorCode != 0 || response.data == null)
        {
            Debug.LogError($"[UIPopupRewardCardSelect] PurchaseRewardCard 실패: {response.errorCode}");
            // 이미 퇴장 연출로 사라진 기존 카드를 그대로 다시 띄움 — 잠금 해제도 재바인딩 시점에 함께 처리
            RebindCardsAfterExit(m_candidateCardIds);
            return;
        }

        RebindCardsAfterExit(response.data.rewardCardCandidates);
    }

    private void OnCloseClicked()
    {
        CloseRewardCardPopupRequest request = new CloseRewardCardPopupRequest
        {
            zoneNumber = m_zoneNumber,
            cellRow = m_cellRow,
            cellCol = m_cellCol,
        };
        NetworkManager.Instance.CloseRewardCardPopup(request, response =>
        {
            if (response.errorCode != 0)
                Debug.LogError($"[UIPopupRewardCardSelect] CloseRewardCardPopup 실패: {response.errorCode}");
        });

        System.Action onClosed = m_onClosed;
        m_onClosed = null;
        onClosed?.Invoke();
    }

    // 카드 후보가 없는 셀(탈출/트레저 등)에서만 버튼 자체를 숨김 — 그 외엔 항상 보이고 interactable로 소진 상태를 표현(0/n도 그대로 보여줌)
    // 리롤 가능 횟수가 0이어도 버튼은 계속 눌러지게 둠 — 0일 때 클릭하면 광고 시청(충전) 흐름으로 전환되기 때문
    private void RefreshRerollButtonState()
    {
        if (m_rerollButton == null) return;
        bool hasCardCandidates = m_candidateCardIds != null && m_candidateCardIds.Count > 0;
        m_rerollButton.gameObject.SetActive(hasCardCandidates);
        if (hasCardCandidates == false) return;

        m_rerollButton.interactable = true;

        if (m_rerollButtonText != null)
        {
            string label = LocalizationManager.Instance.Get("UIPopupRewardCardSelect_RerollButton");
            // VIP는 충전 횟수 제한이 아예 없음 — 숫자 대신 무제한 표시(VIP가 끝나면 그 시점의 실제 충전량이 다시 보임)
            bool isVip = IAPManager.Instance.IsVipActive();
            if (isVip == true)
            {
                m_rerollButtonText.text = $"{label} ∞";
            }
            else
            {
                int rerollChargeMax = DataManager.Instance.m_dataTableConfig.gameSettings.exploration.rewardCardRerollChargeMax;
                m_rerollButtonText.text = $"{label} {m_rerollCharge}/{rerollChargeMax}";
            }
        }
    }

    private void OnRerollClicked()
    {
        // VIP는 충전 횟수와 무관하게 항상 리롤 가능(광고 흐름 자체를 타지 않음)
        bool isVip = IAPManager.Instance.IsVipActive();
        if (isVip == true || m_rerollCharge > 0)
        {
            RequestReroll();
            return;
        }

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get("UIPopupRewardCardSelect_RerollChargeAd"),
            confirmText1 = LocalizationManager.Instance.Get("UI_WatchAD"),
            onConfirm = RequestRerollChargeRefillWithAd,
            onCancel = () => { },
        });
    }

    private void RequestReroll()
    {
        m_rerollButton.interactable = false; // 응답이 올 때까지 중복 리롤 방지
        SetCardButtonsBusy(true); // 사라지는 중인(곧 교체될) 카드가 구매되지 않도록 잠금
        PlayCardsExit(-1);
        RerollRewardCardRequest request = new RerollRewardCardRequest
        {
            zoneNumber = m_zoneNumber,
            cellRow = m_cellRow,
            cellCol = m_cellCol,
        };
        NetworkManager.Instance.RerollRewardCard(request, OnRerollResponse);
    }

    private void OnRerollResponse(ApiResponse<RerollRewardCardResponse> response)
    {
        // 실패 시엔 퇴장 연출로 사라진 기존 카드를 그대로 다시 띄움
        if (response.errorCode == (int)ServerErrorCode.EXPLORATION_REWARD_CARD_REROLL_LIMIT_EXCEEDED)
        {
            m_rerollCharge = 0;
            RefreshRerollButtonState();
            RebindCardsAfterExit(m_candidateCardIds);
            return;
        }

        if (response.errorCode != 0 || response.data == null)
        {
            RefreshRerollButtonState();
            RebindCardsAfterExit(m_candidateCardIds);
            return;
        }

        m_rerollCharge = response.data.rerollRemain;
        RefreshRerollButtonState();
        RebindCardsAfterExit(response.data.rewardCardCandidates);
    }

    // 충전 횟수가 0인 비VIP만 도달하는 경로 — 광고 시청 성공 시에만 서버 충전 엔드포인트 호출
    private void RequestRerollChargeRefillWithAd()
    {
        AdManager.Instance.ShowRewardedAd(result =>
        {
            if (result != EAdResult.Rewarded) return;

            WatchAdForRewardCardRerollChargeRequest request = new WatchAdForRewardCardRerollChargeRequest();
            NetworkManager.Instance.WatchAdForRewardCardRerollCharge(request, OnRerollChargeRefillResponse);
        });
    }

    private void OnRerollChargeRefillResponse(ApiResponse<WatchAdForRewardCardRerollChargeResponse> response)
    {
        if (response.errorCode != 0 || response.data == null) return;

        m_rerollCharge = response.data.rewardCardRerollCharge;
        RefreshRerollButtonState();
    }
}
