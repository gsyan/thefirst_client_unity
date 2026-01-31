//------------------------------------------------------------------------------
using UnityEngine;

public class UISpace : UIManager
{
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
        // 패널 표시는 튜토리얼 흐름에서 처리
        // - 자원 튜토리얼: preActionPanelName = "UIPanelMineral"
        // - 함대버튼 튜토리얼: preActionPanelName = "MainPanel" (또는 ShowMainPanel 호출)
    }
}
