// 진형 배치 데이터 ScriptableObject — CubeGrid(정수 격자) / Circle(각도) 두 가지 파싱 타입 지원
// FormationPresetDB: 전역 단일 맵, Resources/Formation/ 에서 lazy 로드
using System;
using System.Collections.Generic;
using UnityEngine;

public enum EFormationParseType
{
    CubeGrid,   // 정수 격자 좌표 → 함선 사이즈 누적 간격으로 변환
    Circle,     // 각도(도) + 반지름 → 원주 위 위치로 변환
}

public enum EZPlacement
{
    Center,   // 기함 중심/앞단/뒷단 기준 고정 레이어 (함선 크기 무시)
    Forward,  // 기함 앞단 기준 bounds-based 전진 누적
    Backward, // 기함 뒷단 기준 bounds-based 후퇴 누적 (음수 방향)
}

[Serializable]
public struct FormationSlot
{
    public int positionIndex;
    [Tooltip("CubeGrid 전용: (x=좌우, y=상하) 정수 격자. 양수=오른쪽/위, 음수=왼쪽/아래")]
    public Vector3Int gridCoord;
    [Tooltip("Circle 전용: 각도(도). 0=상(Y+), 90=우(X+), 180=하(Y-), 270=좌(X-)")]
    public float circleAngle;
}


[CreateAssetMenu(fileName = "FormationPreset", menuName = "Game/FormationPreset")]
public class FormationPreset : ScriptableObject
{
    public EFormationType formationType;
    public EFormationParseType parseType;

    [Header("CubeGrid 전용")]
    [Tooltip("축별 함선 간 최소 여백 (x=좌우, y=상하, z=전후) / Circle의 경우 x=반지름 간격, z=Z 오프셋")]
    public Vector3 gridGap = new(1f, 1f, 1f);
    [Tooltip("Z축 배치 방식: Center=고정 레이어, Forward=앞단 누적, Backward=뒷단 누적")]
    public EZPlacement zPlacement;
    [Tooltip("Forward/Backward 전용\ntrue: center = cursor+gap+half (자신 반폭 포함, 기본)\nfalse: center = cursor+gap (자신 반폭 미포함)")]
    public bool zIncludeHalfSize = true;
    [Tooltip("positionIndex 0 = 기함, 격자 (0,0) 고정")]
    public FormationSlot[] slots;

    [Header("UI")]
    [Tooltip("전술 버튼에 표시할 아이콘 이미지")]
    public Sprite formationIcon;

    [Header("밸런스 — 인덱스 0~3 = 진형단계 1~4 (3척/5척/7척/9척)")]
    [Tooltip("공격력 배율 delta. 보너스=양수(0.25~1.0), 패널티=음수(-0.10~-0.25), 효과없음=0")]
    public float[] attackMultiplierPerStep = { 0f, 0f, 0f, 0f };
    [Tooltip("피격 데미지 차감 비율. 0=없음, 0.2~0.5=차감")]
    public float[] defenseReductionPerStep = { 0f, 0f, 0f, 0f };
    [Tooltip("회복력 배율 delta. 보너스=양수(0.25~1.0), 패널티=음수(-0.10~-0.25), 효과없음=0")]
    public float[] repairMultiplierPerStep = { 0f, 0f, 0f, 0f };
}

// 전역 진형 프리셋 맵 — Resources/Formation/ 에서 lazy 로드, 모든 함대가 공유
public static class FormationPresetDB
{
    private static Dictionary<EFormationType, FormationPreset> s_map;

    public static FormationPreset Get(EFormationType type)
    {
        if (s_map == null) Load();
        s_map.TryGetValue(type, out var preset);
        return preset;
    }

    private static void Load()
    {
        s_map = new Dictionary<EFormationType, FormationPreset>();
        var presets = ResourceManager.Instance.LoadAll<FormationPreset>("Formation");
        foreach (var p in presets)
        {
            if (p != null)
                s_map[p.formationType] = p;
        }
        //Debug.Log($"[FormationPresetDB] {s_map.Count}개 로드 완료");
    }
}
