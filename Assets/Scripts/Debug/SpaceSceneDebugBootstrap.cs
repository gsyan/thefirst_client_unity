// 에디터 전용 — SpaceScene을 직접 실행할 때 더미 캐릭터/함대 데이터를 주입
using System.Collections.Generic;
using UnityEngine;

// 빌드 시 클래스가 사라지지 않도록 #if UNITY_EDITOR를 클래스 밖에 두지 않음.
// 대신 Awake에서 자기 파괴하여 missing script 경고를 방지.
public class SpaceSceneDebugBootstrap : MonoBehaviour
{
    private void Awake()
    {
#if !UNITY_EDITOR
        Destroy(gameObject);
        return;
#endif

#if UNITY_EDITOR
        if (DataManager.Instance.m_currentCommander != null)
            return;

        InjectDebugCommander();
        InjectDebugFleet();

        Debug.LogWarning("[DebugBootstrap] 에디터 직접 실행 감지 — 더미 캐릭터/함대 데이터 주입 완료");
#endif
    }

#if UNITY_EDITOR
    private void InjectDebugCommander()
    {
        var commanderInfo = new CommanderInfo
        {
            commanderId   = 1,
            commanderName = "debug_player",
            exp           = 999999,
            pvpPoint      = 0,
            clearedZones  = new List<string>(),
            nameChangeCount = 2,
            commandPowerMax = 120, // 서버 Commander.java 기본값(INT DEFAULT 120)과 동일하게 맞춤
        };
        DataManager.Instance.SetCommanderInfo(commanderInfo);
    }

    private void InjectDebugFleet()
    {
        if (DataManager.Instance.m_currentFleetInfo != null)
            return;

        var fleet = new FleetInfo
        {
            ships = new List<ShipInfo>
            {
                new ShipInfo { hullSubType = "h1_11100", isFront = true },
            },
        };

        DataManager.Instance.SetFleetData(fleet);
    }
#endif
}

