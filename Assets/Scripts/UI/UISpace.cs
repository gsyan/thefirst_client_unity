//------------------------------------------------------------------------------
using System.Collections;
using UnityEngine;

public class UISpace : UIManager
{
    protected override void Awake()
    {
        base.Awake();
        StartCoroutine(CheckVipDailyBonusRoutine());
    }

    private IEnumerator CheckVipDailyBonusRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (IAPManager.Instance == null || IAPManager.Instance.IsVipActive() == false) yield break;

        IAPManager.Instance.TryClaimDailyMineral(result =>
        {
            if (result == null || result.available == false) return;

            var character = DataManager.Instance.m_currentCharacter;
            if (character != null)
                character.UpdateMineral(result.mineralRemain);

            var loc = LocalizationManager.Instance;
            ShowConfirmPopup(new ConfirmPopupConfig
            {
                title        = loc.Get("VipDailyBonus_Title"),
                message      = loc.Get("VipDailyBonus_Desc", result.grantedMineral),
                confirmText1 = loc.Get("Simple_Confirm"),
                onConfirm    = null,
            });
        });
    }

    public override void InitializeUIManager()
    {
        base.InitializeUIManager();

        const string PANEL_GAME_PREFAB_PATH = "Prefabs/UI/Panel_Game";

        // Load all prefabs from the Panel folder
        GameObject[] panelPrefabs = Resources.LoadAll<GameObject>(PANEL_GAME_PREFAB_PATH);

        if (panelPrefabs == null || panelPrefabs.Length == 0)
        {
            Debug.LogWarning($"No panel prefabs found in {PANEL_GAME_PREFAB_PATH}");
            return;
        }

        foreach (GameObject prefab in panelPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[UISpace] null prefab — skip");
                continue;
            }
            Debug.Log($"[UISpace] Instantiate: {prefab.name}");

            // 일반 UI는 GeneralContainer에 생성
            GameObject panelInstance = Instantiate(prefab, m_generalContainer);
            panelInstance.name = prefab.name;

            var panelBase = panelInstance.GetComponent<UIPanelBase>();
            if(panelBase != null)
            {
                panelBase.panelName = prefab.name;
                panelBase.InitializeUIPanel();
            }

            AddPanel(panelBase);
        }

        InitializePanels();
    }
}
