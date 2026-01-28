using UnityEngine;
using System.Collections.Generic;

public enum EGaugeBarMode
{
    Body,
    Weapon,
    Engine,
    Hanger,
    All
}

public class GaugeBars : MonoBehaviour
{
    [SerializeField] private GameObject m_multiGaugeBarPrefab;
    [SerializeField] private Canvas m_targetCanvas;
    [HideInInspector] public EGaugeBarMode m_displayMode = EGaugeBarMode.Body;

    private SpaceShip m_spaceShip;
    private Dictionary<ModuleBase, GaugeBar> m_moduleGaugeBars = new Dictionary<ModuleBase, GaugeBar>();
    
    [SerializeField] private Vector3 m_offsetFromTarget = new Vector3(0, 2f, 0);
    [SerializeField] private float m_smoothSpeed = 5f;

    void Awake()
    {
        m_spaceShip = GetComponent<SpaceShip>();
        if (m_targetCanvas == null)
            m_targetCanvas = FindFirstObjectByType<Canvas>();

        EventManager.Subscribe_ModuleReplaced(OnModuleReplaced);
    }

    void Start()
    {
        if (m_spaceShip != null)
            InitializeGaugeBars();
    }

    private void OnModuleReplaced(ModuleBase oldModule, ModuleBase newModule)
    {
        // 기존 모듈 게이지바 제거
        if (oldModule != null && m_moduleGaugeBars.TryGetValue(oldModule, out GaugeBar oldGaugeBar))
        {
            if (oldGaugeBar != null)
                Destroy(oldGaugeBar.gameObject);
            m_moduleGaugeBars.Remove(oldModule);
        }

        // 새 모듈 게이지바 생성 (Body 타입만)
        if (newModule != null && newModule is ModuleBody)
        {
            CreateGaugeBarForModule(newModule);
        }
    }

    private void InitializeGaugeBars()
    {
        ClearAllGaugeBars();
        if (m_spaceShip == null) return;
        switch (m_displayMode)
        {
            case EGaugeBarMode.Body:
                foreach (ModuleBody body in m_spaceShip.m_moduleBodys)
                {
                    if (body != null)
                        CreateGaugeBarForModule(body);
                }
                break;

            // case EGaugeBarMode.Weapon:
            //     foreach (ModuleWeapon weapon in m_spaceShip.m_moduleWeaponList)
            //     {
            //         if (weapon != null)
            //             CreateGaugeBarForModule(weapon);
            //     }
            //     break;

            // case EGaugeBarMode.Engine:
            //     foreach (ModuleEngine engine in m_spaceShip.m_moduleEngineList)
            //     {
            //         if (engine != null)
            //             CreateGaugeBarForModule(engine);
            //     }
            //     break;

            case EGaugeBarMode.All:
                foreach (ModuleBody body in m_spaceShip.m_moduleBodys)
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

    // 게이지바 생성
    private void CreateGaugeBarForModule(ModuleBase module)
    {
        if (m_moduleGaugeBars.ContainsKey(module) == true) return;
        if (m_targetCanvas == null) return;

        GameObject gaugeBarPrefab = Resources.Load<GameObject>("Prefabs/UI/GaugeBar");
        if (gaugeBarPrefab == null) return;

        GameObject gaugeBarObj = Instantiate(gaugeBarPrefab, m_targetCanvas.transform);
        GaugeBar gaugeBar = gaugeBarObj.GetComponent<GaugeBar>();
        if (gaugeBar == null) return;
        Color gaugeColor = GetModuleColor(module);
        gaugeBar.InitializeGaugeBar(module.transform, m_offsetFromTarget, gaugeColor, m_smoothSpeed);
        m_moduleGaugeBars.Add(module, gaugeBar);

    }

    private Color GetModuleColor(ModuleBase module)
    {
        if (module is ModuleBody)
            return new Color(0.2f, 0.8f, 0.2f);
        else if (module is ModuleBeam)
            return new Color(0.8f, 0.2f, 0.2f);
        else if (module is ModuleMissile)
            return new Color(0.8f, 0.3f, 0.2f);
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
            GaugeBar gaugeBar = kvp.Value;

            if (module == null || gaugeBar == null) continue;

            float currentHealth = 0f;
            float maxHealth = 100f;

            if (module is ModuleBody body)
            {
                currentHealth = body.m_health;
                maxHealth = body.m_healthMax;
            }
            else if (module is ModuleBeam beam)
            {
                currentHealth = beam.m_health;
                maxHealth = beam.m_healthMax;
            }
            else if (module is ModuleMissile missile)
            {
                currentHealth = missile.m_health;
                maxHealth = missile.m_healthMax;
            }
            else if (module is ModuleEngine engine)
            {
                currentHealth = engine.m_health;
                maxHealth = engine.m_healthMax;
            }

            gaugeBar.UpdateValue(currentHealth, maxHealth);
        }
    }

    private void LateUpdate()
    {
        foreach (var kvp in m_moduleGaugeBars)
        {
            GaugeBar gaugeBar = kvp.Value;
            if (gaugeBar == null) continue;

            bool isInBounds = gaugeBar.IsInScreenBounds();

            if (isInBounds == true
                && gaugeBar.gameObject.activeSelf == false)
                gaugeBar.gameObject.SetActive(true);
            else if (isInBounds == false
                && gaugeBar.gameObject.activeSelf == true)
                gaugeBar.gameObject.SetActive(false);
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
        EventManager.Unsubscribe_ModuleReplaced(OnModuleReplaced);
        ClearAllGaugeBars();
    }
}
