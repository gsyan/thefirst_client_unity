//------------------------------------------------------------------------------
using UnityEngine;

public class UISpace : UIManager
{
    public override void InitializeUIManager()
    {
        base.InitializeUIManager();

        const string PANEL_GAME_PREFAB_PATH = "Prefabs/UI/Panel_Game";

        // Load all prefabs from the Panel folder
        GameObject[] panelPrefabs = ResourceManager.Instance.LoadAll<GameObject>(PANEL_GAME_PREFAB_PATH);

        if (panelPrefabs == null || panelPrefabs.Length == 0)
        {
            Debug.LogWarning($"No panel prefabs found in {PANEL_GAME_PREFAB_PATH}");
            return;
        }

        foreach (GameObject prefab in panelPrefabs)
        {
            string prefabName = prefab.name; // Instantiate 도중 native object 소멸 대비

            // 일반 UI는 GeneralContainer에 생성
            GameObject panelInstance = Instantiate(prefab, m_generalContainer);
            panelInstance.name = prefabName;

            var panelBase = panelInstance.GetComponent<UIPanelBase>();
            if(panelBase != null)
            {
                panelBase.panelName = prefabName;
                panelBase.InitializeUIPanel();
            }

            AddPanel(panelBase);
        }

        InitializePanels();
    }
}
