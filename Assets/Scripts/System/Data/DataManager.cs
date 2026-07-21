// 게임 전반의 런타임 데이터(캐릭터, 함대, 계정 상태, 데이터 테이블)를 관리하는 싱글톤
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class DataManager : Singleton<DataManager>
{
    #region Initialization #####################################################################
    protected override void OnInitialize()
    {
        LoadDataTableModule();
        LoadDataTableUpgradeCost();
        LoadDataTableCommanderLevel();
        LoadDataTableConfig();
        LoadDataTableZone();
        LoadDataTableShipPreset();
        LoadDataTablePvpSeason();
        LoadDataTableDailyBonus();
        LoadColorPalette();
    }
    #endregion

    #region Account State Management ############################################################
    public bool m_isGoogleLinked;  // 구글 계정 연동 여부
    #endregion

    #region Commander Info Management ###########################################################
    public Commander m_currentCommander;

    // 서버에서 받은 커맨더 정보 설정 — 로컬 저장 없음
    public void SetCommanderInfo(CommanderInfo commanderInfo)
    {
        if (m_currentCommander == null)
            m_currentCommander = new Commander(commanderInfo);

        m_currentCommander.UpdateCommanderInfo(commanderInfo);
    }

    public void ClearCommanderData()
    {
        m_currentCommander = null;
    }
    #endregion

    #region Fleet Info Management ###############################################################
    public TempFleetInfo m_currentFleetInfo;

    // 초기화 전용 — 로그인/씬 진입 시 최초 1회만 사용
    public void SetFleetData(TempFleetInfo fleetInfo)
    {
        m_currentFleetInfo = fleetInfo;
    }

    public void ClearFleetData()
    {
        m_currentFleetInfo = null;
    }

    public int GetShipCount()
    {
        return m_currentFleetInfo?.ships?.Count ?? 0;
    }
    #endregion

    #region Data Table Config ###############################################################
    public DataTableConfig m_dataTableConfig;

    private void LoadDataTableConfig()
    {
        m_dataTableConfig = ResourceManager.Instance.Load<DataTableConfig>("DataTable/DataTableConfig");
        if (m_dataTableConfig == null)
        {
            Debug.LogError("DataTableConfig is not exist");
        }
        else
        {
            Debug.Log("DataTableConfig loaded successfully");
        }
    }

    public void ApplyGameSettings()
    {
        // 게임 설정 적용 로직
    }

    public string GetGameVersion()
    {
        return m_dataTableConfig?.gameSettings?.version ?? "1.0.0";
    }

    #endregion


    #region Data Table Module ###############################################################
    public DataTableModule m_dataTableModule;

    private void LoadDataTableModule()
    {
        m_dataTableModule = ResourceManager.Instance.Load<DataTableModule>("DataTable/DataTableModule");
        if (m_dataTableModule == null)
            Debug.LogError("DataTableModule is not exist");
    }

    public bool GetModuleLevelUpCost(EModuleSubType subType, int moduleLevel, out int modulePointCost)
    {
        modulePointCost = 0;
        ModuleData moduleData = m_dataTableModule.GetModuleDataFromTable(subType, moduleLevel + 1);
        if (moduleData == null) return false;

        modulePointCost = moduleData.modulePointCost;
        return true;
    }

    // level+1 데이터가 없으면 현재 레벨이 최대
    public int GetMaxModuleLevel(EModuleSubType subType)
    {
        int level = 1;
        while (m_dataTableModule.GetModuleDataFromTable(subType, level + 1) != null)
            level++;
        return level;
    }

    public EModuleSubType GetFirstSubType(EModuleType moduleType)
    {
        if (moduleType == EModuleType.body)    return EModuleSubType.body_t1_m1;
        if (moduleType == EModuleType.beam)    return EModuleSubType.beam_t1_m1;
        if (moduleType == EModuleType.missile) return EModuleSubType.missile_t1_m1;
        if (moduleType == EModuleType.hanger)  return EModuleSubType.hanger_t1_m1;
        return EModuleSubType.none;
    }

    // investedModulePoint → (baselineSubType, baselineLevel) 역산
    // unlock(1) → T1 레벨업 → T2 그레이드업 → T2 레벨업 → ... 순서로 차감
    public bool CalcModulePointBaseline(EModuleType moduleType, int investedModulePoint, out EModuleSubType baselineSubType, out int baselineLevel)
    {
        baselineSubType = EModuleSubType.none;
        baselineLevel   = 0;

        if (investedModulePoint <= 0) return false;

        // 함체는 언락의 대상이 아님, 언락 비용 없음 (서버 calcModulePointBaseline과 동일 처리)
        int unlockCost    = moduleType != EModuleType.body ? m_dataTableConfig.gameSettings.moduleUnlockPrice : 0;
        int remaining     = investedModulePoint - unlockCost;
        EModuleSubType currentSubType = GetFirstSubType(moduleType);

        while (currentSubType != EModuleSubType.none)
        {
            int maxLevel = GetMaxModuleLevel(currentSubType);

            for (int lv = 1; lv < maxLevel; lv++)
            {
                if (GetModuleLevelUpCost(currentSubType, lv, out int cost) == false) break;
                if (remaining < cost)
                {
                    baselineSubType = currentSubType;
                    baselineLevel   = lv;
                    return true;
                }
                remaining -= cost;
            }

            // 최대레벨 도달 → 다음 그레이드
            int nextVal = (int)currentSubType + 100;
            if (System.Enum.IsDefined(typeof(EModuleSubType), nextVal) == false)
            {
                baselineSubType = currentSubType;
                baselineLevel   = maxLevel;
                return true;
            }

            EModuleSubType nextSubType = (EModuleSubType)nextVal;
            int gradeUpCost = GetModuleResearchCost(nextSubType);
            if (remaining < gradeUpCost)
            {
                baselineSubType = currentSubType;
                baselineLevel   = maxLevel;
                return true;
            }
            remaining     -= gradeUpCost;
            currentSubType = nextSubType;
        }

        return false;
    }
    #endregion

    #region Data Table Upgrade Cost ###################################################################
    public DataTableUpgradeCost m_dataTableUpgradeCost;

    private void LoadDataTableUpgradeCost()
    {
        m_dataTableUpgradeCost = ResourceManager.Instance.Load<DataTableUpgradeCost>("DataTable/DataTableUpgradeCost");
        if (m_dataTableUpgradeCost == null)
            Debug.LogError("DataTableUpgradeCost is not exist");
        else
            Debug.Log("DataTableUpgradeCost loaded successfully");
    }

    public int GetModuleResearchCost(EModuleSubType subType)
    {
        if (m_dataTableUpgradeCost == null) return 0;
        return m_dataTableUpgradeCost.GetCost(subType);
    }
    #endregion

    #region Data Table Commander Level ###########################################################
    public DataTableCommanderLevel m_dataTableCommanderLevel;

    private void LoadDataTableCommanderLevel()
    {
        m_dataTableCommanderLevel = ResourceManager.Instance.Load<DataTableCommanderLevel>("DataTable/DataTableCommanderLevel");
        if (m_dataTableCommanderLevel == null)
            Debug.LogError("DataTableCommanderLevel is not exist");
        else
            Debug.Log("DataTableCommanderLevel loaded successfully");
    }
    #endregion

    #region Data Table Zone ###############################################################
    public DataTableZone m_dataTableZone;

    private void LoadDataTableZone()
    {
        m_dataTableZone = ResourceManager.Instance.Load<DataTableZone>("DataTable/DataTableZone");
        if (m_dataTableZone == null)
            Debug.LogError("DataTableZone is not exist");
    }
    #endregion

    #region Data Table Ship Preset ###############################################################
    public DataTableShipPreset m_dataTableShipPreset;

    private void LoadDataTableShipPreset()
    {
        m_dataTableShipPreset = ResourceManager.Instance.Load<DataTableShipPreset>("DataTable/DataTableShipPreset");
        if (m_dataTableShipPreset == null)
            Debug.LogError("DataTableShipPreset is not exist");
    }
    #endregion

    #region Data Table PvpSeason ###############################################################
    public DataTablePvpSeason m_dataTablePvpSeason;

    private void LoadDataTablePvpSeason()
    {
        m_dataTablePvpSeason = ResourceManager.Instance.Load<DataTablePvpSeason>("DataTable/DataTablePvpSeason");
        if (m_dataTablePvpSeason == null)
            Debug.LogError("DataTablePvpSeason is not exist");
    }
    #endregion

    #region Data Table DailyBonus ###############################################################
    public DataTableDailyBonus m_dataTableDailyBonus;

    private void LoadDataTableDailyBonus()
    {
        m_dataTableDailyBonus = ResourceManager.Instance.Load<DataTableDailyBonus>("DataTable/DataTableDailyBonus");
        if (m_dataTableDailyBonus == null)
            Debug.LogError("DataTableDailyBonus is not exist");
    }
    #endregion

    #region ColorPalette ###############################################################
    public ColorPalette m_colorPalette;

    private void LoadColorPalette()
    {
        m_colorPalette = ResourceManager.Instance.Load<ColorPalette>("DataTable/ColorPalette");
        if (m_colorPalette == null)
            Debug.LogError("ColorPalette is not exist");
    }
    #endregion

}