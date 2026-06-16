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
        LoadDataTableModuleResearch();
        LoadDataTableTechLevel();
        LoadDataTableConfig();
        LoadDataTableZone();
        LoadDataTablePvpSeason();
        LoadDataTableDailyBonus();
        LoadColorPalette();
    }
    #endregion

    #region Account State Management ############################################################
    public bool m_isGoogleLinked;  // 구글 계정 연동 여부
    #endregion

    #region Character Info Management ###########################################################
    public Character m_currentCharacter;

    // 서버에서 받은 캐릭터 정보 설정 — 로컬 저장 없음
    public void SetCharacterInfo(CharacterInfo characterInfo)
    {
        if (m_currentCharacter == null)
            m_currentCharacter = new Character(characterInfo);

        m_currentCharacter.UpdateCharacterInfo(characterInfo);
    }

    public void ClearCharacterData()
    {
        m_currentCharacter = null;
    }
    #endregion

    #region Fleet Info Management ###############################################################
    public FleetInfo m_currentFleetInfo;

    // 초기화 전용 — 로그인/씬 진입 시 최초 1회만 사용
    public void SetFleetData(FleetInfo fleetInfo)
    {
        m_currentFleetInfo = fleetInfo;
    }

    // 서버에서 전체 함선 목록을 받을 때 통째 교체 (초기화 또는 전체 갱신)
    public void ApplyFleetShips(List<ShipInfo> ships)
    {
        if (m_currentFleetInfo == null) return;
        m_currentFleetInfo.ships = ships;
    }

    // 함선 1척 추가 — SpaceShip.m_shipInfo 참조 보존을 위해 서버 응답 ShipInfo를 리스트에 직접 추가
    public void AddFleetShip(ShipInfo ship)
    {
        if (m_currentFleetInfo == null || m_currentFleetInfo.ships == null) return;
        m_currentFleetInfo.ships.Add(ship);
    }

    // 함선 1척 제거
    public void RemoveFleetShip(long shipId)
    {
        if (m_currentFleetInfo == null || m_currentFleetInfo.ships == null) return;
        for (int i = m_currentFleetInfo.ships.Count - 1; i >= 0; i--)
        {
            if (m_currentFleetInfo.ships[i].id == shipId)
            {
                m_currentFleetInfo.ships.RemoveAt(i);
                break;
            }
        }
    }

    // 진형 변경 시
    public void ApplyFleetFormation(EFormationType formation)
    {
        if (m_currentFleetInfo == null) return;
        m_currentFleetInfo.formation = formation;
    }

    // 전술 옵션 변경 시
    public void ApplyFleetTacticOptions(int tacticOptions)
    {
        if (m_currentFleetInfo == null) return;
        m_currentFleetInfo.tacticOptions = tacticOptions;
    }

    public void ClearFleetData()
    {
        m_currentFleetInfo = null;
    }

    public ShipInfo GetShipAtPosition(int positionIndex)
    {
        if (m_currentFleetInfo?.ships == null) return null;

        foreach (var shipInfo in m_currentFleetInfo.ships)
        {
            if (shipInfo.positionIndex == positionIndex)
                return shipInfo;
        }
        return null;
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

    public bool GetModuleLevelUpCost(EModuleSubType subType, int moduleLevel, out long modulePointCost)
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

        int unlockCost    = m_dataTableConfig.gameSettings.moduleUnlockPrice;
        int remaining     = investedModulePoint - unlockCost;
        EModuleSubType currentSubType = GetFirstSubType(moduleType);

        while (currentSubType != EModuleSubType.none)
        {
            int maxLevel = GetMaxModuleLevel(currentSubType);

            for (int lv = 1; lv < maxLevel; lv++)
            {
                if (GetModuleLevelUpCost(currentSubType, lv, out long levelCost) == false) break;
                int cost = (int)levelCost;
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
            int gradeUpCost = (int)GetModuleResearchCost(nextSubType);
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

    #region Data Table Module Research ###############################################################
    public DataTableResearch m_dataTableResearch;

    private void LoadDataTableModuleResearch()
    {
        m_dataTableResearch = ResourceManager.Instance.Load<DataTableResearch>("DataTable/DataTableResearch");
        if (m_dataTableResearch == null)
            Debug.LogError("DataTableResearch is not exist");
        else
            Debug.Log("DataTableResearch loaded successfully");
    }

    public long GetModuleResearchCost(EModuleSubType subType)
    {
        if (m_dataTableResearch == null) return 0;
        return m_dataTableResearch.GetResearchCost(subType);
    }
    #endregion

    #region Data Table Tech Level ###############################################################
    public DataTableTechLevel m_dataTableTechLevel;

    private void LoadDataTableTechLevel()
    {
        m_dataTableTechLevel = ResourceManager.Instance.Load<DataTableTechLevel>("DataTable/DataTableTechLevel");
        if (m_dataTableTechLevel == null)
            Debug.LogError("DataTableTechLevel is not exist");
        else
            Debug.Log("DataTableTechLevel loaded successfully");
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