using UnityEngine;
using System.Collections.Generic;

public enum EGaugeDisplayMode
{
    Body,
    Weapon,
    Engine,
    All
}

public class ModuleGaugeDisplay : MonoBehaviour
{
    [SerializeField] private GameObject m_multiGaugeBarPrefab;
    [SerializeField] private Canvas m_targetCanvas;
    [HideInInspector] public EGaugeDisplayMode m_displayMode = EGaugeDisplayMode.Body;

    private SpaceShip m_spaceShip;
    private Dictionary<ModuleBase, MultiGaugeBar> m_moduleGaugeBars = new Dictionary<ModuleBase, MultiGaugeBar>();
    
    void Awake()
    {
        m_spaceShip = GetComponent<SpaceShip>();
        if (m_targetCanvas == null)
            m_targetCanvas = FindFirstObjectByType<Canvas>();
    }

    void Start()
    {
        if (m_spaceShip != null)
            InitializeGaugeBars();
    }

    private void InitializeGaugeBars()
    {
        ClearAllGaugeBars();

        if (m_spaceShip == null) return;

        switch (m_displayMode)
        {
            case EGaugeDisplayMode.Body:
                foreach (ModuleBody body in m_spaceShip.m_moduleBodyList)
                {
                    if (body != null)
                        CreateGaugeBarForModule(body);
                }
                break;

            // case EGaugeDisplayMode.Weapon:
            //     foreach (ModuleWeapon weapon in m_spaceShip.m_moduleWeaponList)
            //     {
            //         if (weapon != null)
            //             CreateGaugeBarForModule(weapon);
            //     }
            //     break;

            // case EGaugeDisplayMode.Engine:
            //     foreach (ModuleEngine engine in m_spaceShip.m_moduleEngineList)
            //     {
            //         if (engine != null)
            //             CreateGaugeBarForModule(engine);
            //     }
            //     break;

            case EGaugeDisplayMode.All:
                foreach (ModuleBody body in m_spaceShip.m_moduleBodyList)
                {
                    if (body != null)
                        CreateGaugeBarForModule(body);
                }
                // foreach (ModuleWeapon weapon in m_spaceShip.m_moduleWeaponList)
                // {
                //     if (weapon != null)
                //         CreateGaugeBarForModule(weapon);
                // }
                // foreach (ModuleEngine engine in m_spaceShip.m_moduleEngineList)
                // {
                //     if (engine != null)
                //         CreateGaugeBarForModule(engine);
                // }
                break;
        }
    }

    public void SetGaugeVisible(bool visible)
    {
        if (visible == false)
            HideAllGaugeBars();
        else
            ShowAllGaugeBars();
    }

    private void CreateGaugeBarForModule(ModuleBase module)
    {
        if (m_moduleGaugeBars.ContainsKey(module) == true) return;
        if (m_targetCanvas == null) return;

        if (m_multiGaugeBarPrefab == null)
            m_multiGaugeBarPrefab = Resources.Load<GameObject>("Prefabs/UI/MultiGaugeBar");
        if (m_multiGaugeBarPrefab == null) return;

        GameObject multiGaugeBarObj = Instantiate(m_multiGaugeBarPrefab, m_targetCanvas.transform);
        MultiGaugeBar multiGaugeBar = multiGaugeBarObj.GetComponent<MultiGaugeBar>();
        if (multiGaugeBar == null) return;
        multiGaugeBar.SetMultiGaugeTarget(module.transform);
        Color gaugeColor = GetModuleColor(module);
        multiGaugeBar.AddGauge(gaugeColor);
        m_moduleGaugeBars[module] = multiGaugeBar;

        if (CameraController.Instance != null && CameraController.Instance.m_currentMode != ECameraControllerMode.Normal)
            multiGaugeBar.gameObject.SetActive(false);
    }

    private Color GetModuleColor(ModuleBase module)
    {
        if (module is ModuleBody)
            return new Color(0.2f, 0.8f, 0.2f);
        else if (module is ModuleWeapon)
            return new Color(0.8f, 0.2f, 0.2f);
        else if (module is ModuleEngine)
            return new Color(0.2f, 0.5f, 0.8f);
        else
            return Color.white;
    }

    void Update()
    {
        UpdateAllGaugeBars();
    }

    private void UpdateAllGaugeBars()
    {
        foreach (var kvp in m_moduleGaugeBars)
        {
            ModuleBase module = kvp.Key;
            MultiGaugeBar multiGaugeBar = kvp.Value;

            if (module == null || multiGaugeBar == null) continue;

            float currentHealth = 0f;
            float maxHealth = 100f;

            if (module is ModuleBody body)
            {
                currentHealth = body.m_health;
                maxHealth = body.m_healthMax;
            }
            else if (module is ModuleWeapon weapon)
            {
                currentHealth = weapon.m_health;
                maxHealth = weapon.m_healthMax;
            }
            else if (module is ModuleEngine engine)
            {
                currentHealth = engine.m_health;
                maxHealth = engine.m_healthMax;
            }

            multiGaugeBar.UpdateGauge(0, currentHealth, maxHealth);
        }
    }

    private void HideAllGaugeBars()
    {
        foreach (var kvp in m_moduleGaugeBars)
        {
            if (kvp.Value != null)
                kvp.Value.gameObject.SetActive(false);
        }
    }

    private void ShowAllGaugeBars()
    {
        foreach (var kvp in m_moduleGaugeBars)
        {
            if (kvp.Value != null)
                kvp.Value.gameObject.SetActive(true);
        }
    }

    private void ClearAllGaugeBars()
    {
        foreach (var kvp in m_moduleGaugeBars)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }
        m_moduleGaugeBars.Clear();
    }

    void OnDestroy()
    {
        ClearAllGaugeBars();
    }
}
