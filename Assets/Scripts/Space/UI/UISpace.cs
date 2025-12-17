//------------------------------------------------------------------------------
using UnityEngine;

public class UISpace : UIManager
{
    public override void InitializeUIManager()
    {
        const string PANEL_PREFAB_PATH = "Prefabs/UI/Panel";

        // Load all prefabs from the Panel folder
        GameObject[] panelPrefabs = Resources.LoadAll<GameObject>(PANEL_PREFAB_PATH);

        if (panelPrefabs == null || panelPrefabs.Length == 0)
        {
            Debug.LogWarning($"No panel prefabs found in {PANEL_PREFAB_PATH}");
            return;
        }

        foreach (GameObject prefab in panelPrefabs)
        {
            GameObject panelInstance = Instantiate(prefab, transform);
            panelInstance.name = prefab.name; // Remove "(Clone)" suffix
            
            var panelBase = panelInstance.GetComponent<UIPanelBase>();
            if(panelBase != null)
            {
                panelBase.panelName = prefab.name;
                panelBase.InitializeUIPanel();
            }
                
            AddPanel(panelBase);
        }

        InitializePanels();
        ShowDefaultPanel();

        ShowPanel("UIPanelMineral");
    }
}
