using UnityEngine;
using System.Collections.Generic;

public enum EGaugeBarMode
{
    Body,
    Weapon,
    Engine,
    Hangar,
    All
}

public class GaugeBars : MonoBehaviour
{
    private Transform m_gaugeBarContainer;
    [HideInInspector] public EGaugeBarMode m_displayMode = EGaugeBarMode.Body;

    private SpaceShip m_spaceShip;
    private Dictionary<ModuleBase, GaugeBar> m_moduleGaugeBars = new Dictionary<ModuleBase, GaugeBar>();

    [SerializeField] private Vector3 m_offsetFromTarget = new Vector3(0, 0f, 0);
    [SerializeField] private float m_smoothSpeed = 5f;
    private bool m_hideForGalaxy = false;

    void Awake()
    {
        m_spaceShip = GetComponent<SpaceShip>();
        if (m_gaugeBarContainer == null && UIManager.Instance != null)
            m_gaugeBarContainer = UIManager.Instance.GetGaugeBarContainer();
    }

    void OnEnable()
    {
        EventManager.Subscribe_ModuleReplaced(OnModuleReplaced);
        EventManager.Subscribe_FleetViewRestored(OnFleetViewRestored);
    }

    void OnDisable()
    {
        EventManager.Unsubscribe_ModuleReplaced(OnModuleReplaced);
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestored);
    }

    private void OnFleetViewRestored()
    {
        m_hideForGalaxy = false;
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

        // 새 모듈 게이지바 생성 (이 함선 소속 Body 타입만) — 실드는 같은 게이지바 안의 별도 줄이라 여기서 함께 붙는 것으로 처리됨(UpdateAllGaugeBars)
        if (newModule != null && newModule is ModuleHull && m_spaceShip != null && m_spaceShip.m_moduleHulls.Contains(newModule as ModuleHull))
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
                foreach (ModuleHull body in m_spaceShip.m_moduleHulls)
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
                foreach (ModuleHull body in m_spaceShip.m_moduleHulls)
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
        if (m_gaugeBarContainer == null) return;

        GameObject gaugeBarPrefab = ResourceManager.Instance.Load<GameObject>("Prefabs/UI/GaugeBar");
        if (gaugeBarPrefab == null) return;

        GameObject gaugeBarObj = Instantiate(gaugeBarPrefab, m_gaugeBarContainer);
        GaugeBar gaugeBar = gaugeBarObj.GetComponent<GaugeBar>();
        if (gaugeBar == null) return;
        Color gaugeColor = GetModuleColor(module);
        gaugeBar.InitializeGaugeBar(module.transform, m_offsetFromTarget, gaugeColor, m_smoothSpeed);
        m_moduleGaugeBars.Add(module, gaugeBar);

    }

    private Color GetModuleColor(ModuleBase module)
    {
        if (module is ModuleHull)
            return new Color(0.2f, 0.8f, 0.2f);
        else if (module is ModuleBeam)
            return new Color(0.8f, 0.2f, 0.2f);
        else if (module is ModuleMissile)
            return new Color(0.8f, 0.3f, 0.2f);
        else if (module is ModuleHangar)
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

            if (module is ModuleHull body)
            {
                currentHealth = body.m_health;
                maxHealth = body.m_healthMax;

                // 실드는 같은 게이지바 안의 별도 줄로 표시 — 미장착이면 호출하지 않아 GaugeBar가 그 줄을 계속 숨김
                if (body.m_shield != null && body.m_shield.IsEquipped() == true)
                    gaugeBar.UpdateShieldValue(body.m_shield.GetGauge(), body.m_shield.GetGaugeMax());
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
            else if (module is ModuleHangar hangar)
            {
                currentHealth = hangar.m_health;
                maxHealth = hangar.m_healthMax;
            }

            gaugeBar.UpdateValue(currentHealth, maxHealth);
        }
    }

    private void LateUpdate()
    {
        bool isGalaxyView = CameraController.Instance != null && CameraController.Instance.IsGalaxyView;
        if (isGalaxyView == true)
            m_hideForGalaxy = true;

        foreach (var kvp in m_moduleGaugeBars)
        {
            ModuleBase module = kvp.Key;
            GaugeBar gaugeBar = kvp.Value;
            if (module == null || gaugeBar == null) continue;

            bool shouldShow = false;
            if (m_hideForGalaxy == false)
            {
                bool isInBounds = gaugeBar.IsInScreenBounds();
                bool isFullHealth = IsModuleAtFullHealth(module);
                shouldShow = isInBounds == true && isFullHealth == false;
            }

            if (shouldShow == true && gaugeBar.gameObject.activeSelf == false)
                gaugeBar.gameObject.SetActive(true);
            else if (shouldShow == false && gaugeBar.gameObject.activeSelf == true)
                gaugeBar.gameObject.SetActive(false);
        }
    }

    // 체력/실드 둘 다 만땅이어야 숨김 — 실드가 조금이라도 닳으면 함체 체력이 만땅이어도 게이지바를 계속 보여줘야 함
    private bool IsModuleAtFullHealth(ModuleBase module)
    {
        if (module is ModuleHull body)
        {
            bool healthFull = body.m_health >= body.m_healthMax;
            bool shieldFull = body.m_shield == null || body.m_shield.IsEquipped() == false || body.m_shield.GetGauge() >= body.m_shield.GetGaugeMax();
            return healthFull == true && shieldFull == true;
        }
        else if (module is ModuleBeam beam)
            return beam.m_health >= beam.m_healthMax;
        else if (module is ModuleMissile missile)
            return missile.m_health >= missile.m_healthMax;
        else if (module is ModuleHangar hangar)
            return hangar.m_health >= hangar.m_healthMax;
        return true;
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
