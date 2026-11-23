// 이벤트(Treasure) 셀 무료 보상 선택 팝업 — 광물/체력회복/전투자원충전 중 반드시 1개를 골라야 닫힘(닫기 버튼 없음)
// 카드 모양은 보상카드 버튼(RewardCardButton)을 재사용하고 비용 자리에 "무료"를 표시
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIPopupTreasureSelect : UIPopupBase
{
    [Header("Treasure Select Popup")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private GameObject m_rewardCardButtonContainer; // 카드 버튼 3개를 담은 오브젝트

    private const float k_cardEnterStagger = 0.07f; // UIPopupRewardCardSelect와 같은 순차 등장 간격(초)
    private const string k_mineralIconName = "minerals";
    private const string k_healthHealIconName = "auto-repair";
    private const string k_resourceChargeIconName = "lightning-spanner";

    private RewardCardButton[] m_cardButtons;
    private List<TreasureChoiceInfo> m_choices;
    private int m_zoneNumber;
    private System.Action<SelectTreasureRewardResponse> m_onSelected;
    private bool m_isRequesting; // 선택 요청을 보낸 뒤 응답 전까지 중복 클릭 방지
    private float m_cardExitEndTime;

    protected override void Awake()
    {
        base.Awake();
        m_cardButtons = m_rewardCardButtonContainer.GetComponentsInChildren<RewardCardButton>(true);
    }

    public void ShowPopupTreasureSelect(List<TreasureChoiceInfo> choices, int zoneNumber, System.Action<SelectTreasureRewardResponse> onSelected)
    {
        base.ShowPopup();
        m_choices = choices;
        m_zoneNumber = zoneNumber;
        m_onSelected = onSelected;
        m_isRequesting = false;
        m_cardExitEndTime = 0f;

        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UIPopupTreasureSelect_Title");

        BindCardButtons();
    }

    private void BindCardButtons()
    {
        string freeText = LocalizationManager.Instance.Get("UIPopupTreasureSelect_Free");
        for (int i = 0; i < m_cardButtons.Length; i++)
        {
            if (m_choices == null || i >= m_choices.Count)
            {
                m_cardButtons[i].Hide();
                continue;
            }

            int index = i;
            TreasureChoiceInfo choice = m_choices[index];
            string nameText = LocalizationManager.Instance.Get(GetNameKey(choice.rewardType));
            string descText = BuildDescText(choice);
            string iconName = GetIconName(choice.rewardType);
            m_cardButtons[index].SetCustom(iconName, nameText, descText, freeText, () => OnChoiceClicked(index));
            m_cardButtons[index].SetInteractable(true);
            float enterDelay = k_cardEnterStagger * index;
            m_cardButtons[index].PlayEnter(enterDelay);
        }
    }

    private string GetNameKey(ETreasureRewardType rewardType)
    {
        if (rewardType == ETreasureRewardType.Mineral) return "UIPopupTreasureSelect_Mineral_Name";
        if (rewardType == ETreasureRewardType.ShipHealthHeal) return "UIPopupTreasureSelect_HealthHeal_Name";
        return "UIPopupTreasureSelect_ResourceCharge_Name";
    }

    private string GetIconName(ETreasureRewardType rewardType)
    {
        if (rewardType == ETreasureRewardType.Mineral) return k_mineralIconName;
        if (rewardType == ETreasureRewardType.ShipHealthHeal) return k_healthHealIconName;
        return k_resourceChargeIconName;
    }

    // 광물은 지급 액수, 체력/전투자원은 비율(%)을 설명에 표시
    private string BuildDescText(TreasureChoiceInfo choice)
    {
        if (choice.rewardType == ETreasureRewardType.Mineral)
        {
            // string 하나만 넘기면 Get(key, table) 오버로드로 묶여 테이블명으로 해석되므로 object로 넘김
            object mineralText = CommonUtility.FormatNumber(choice.mineralAmount);
            return LocalizationManager.Instance.Get("UIPopupTreasureSelect_Mineral_Desc", mineralText);
        }

        int percent = Mathf.RoundToInt(choice.ratio * 100f);
        string descKey = choice.rewardType == ETreasureRewardType.ShipHealthHeal ? "UIPopupTreasureSelect_HealthHeal_Desc" : "UIPopupTreasureSelect_ResourceCharge_Desc";
        return LocalizationManager.Instance.Get(descKey, percent);
    }

    private void OnChoiceClicked(int index)
    {
        if (m_isRequesting == true) return;
        if (m_choices == null || index >= m_choices.Count) return;

        m_isRequesting = true;
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        SetCardButtonsInteractable(false);
        PlayCardsExit(index);

        SelectTreasureRewardRequest request = new SelectTreasureRewardRequest
        {
            zoneNumber = m_zoneNumber,
            rewardType = m_choices[index].rewardType,
        };
        NetworkManager.Instance.SelectTreasureReward(request, OnSelectResponse);
    }

    private void OnSelectResponse(ApiResponse<SelectTreasureRewardResponse> response)
    {
        if (response.errorCode != 0 || response.data == null)
        {
            // 실패하면 다시 고를 수 있게 카드를 되살림 — 반드시 하나를 골라야 하므로 팝업은 닫지 않음
            m_isRequesting = false;
            NetworkManager.Instance.ShowRequestFailedPopup(response.errorCode);
            if (gameObject.activeInHierarchy == true)
                BindCardButtons();
            return;
        }

        if (gameObject.activeInHierarchy == false)
        {
            FinishSelect(response.data);
            return;
        }
        StartCoroutine(Co_FinishAfterExit(response.data));
    }

    // 고른 카드가 빨려드는 연출이 끝난 뒤 닫힘 — 응답이 연출보다 빨라도 도중에 사라지지 않게
    private IEnumerator Co_FinishAfterExit(SelectTreasureRewardResponse data)
    {
        while (Time.unscaledTime < m_cardExitEndTime)
            yield return null;

        FinishSelect(data);
    }

    private void FinishSelect(SelectTreasureRewardResponse data)
    {
        System.Action<SelectTreasureRewardResponse> onSelected = m_onSelected;
        m_onSelected = null;
        if (onSelected != null)
            onSelected.Invoke(data);
    }

    private void PlayCardsExit(int selectedIndex)
    {
        float maxDuration = 0f;
        for (int i = 0; i < m_cardButtons.Length; i++)
        {
            if (m_choices == null || i >= m_choices.Count) continue;
            float duration = m_cardButtons[i].PlayExit(i == selectedIndex);
            maxDuration = Mathf.Max(maxDuration, duration);
        }
        m_cardExitEndTime = Time.unscaledTime + maxDuration;
    }

    private void SetCardButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < m_cardButtons.Length; i++)
            m_cardButtons[i].SetInteractable(interactable);
    }
}
