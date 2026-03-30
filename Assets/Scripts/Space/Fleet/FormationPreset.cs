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

[Serializable]
public struct FormationSlot
{
    public int positionIndex;
    [Tooltip("CubeGrid 전용: (x=좌우, y=상하, z=전후) 정수 격자. 양수=오른쪽/위/앞, 음수=왼쪽/아래/뒤")]
    public Vector3Int gridCoord;
    [Tooltip("Circle 전용: 각도(도). 0=상(Y+), 90=우(X+), 180=하(Y-), 270=좌(X-)")]
    public float circleAngle;
}

[Serializable]
public class CircleLayoutByCount
{
    [Tooltip("함대 총 함선 수 (기함 포함)")]
    public int shipCount;
    public FormationSlot[] slots;
}

[CreateAssetMenu(fileName = "FormationPreset", menuName = "Game/FormationPreset")]
public class FormationPreset : ScriptableObject
{
    public EFormationType formationType;
    public EFormationParseType parseType;

    [Header("CubeGrid 전용")]
    [Tooltip("positionIndex 0 = 기함, 격자 (0,0) 고정")]
    public FormationSlot[] slots;

    [Header("Circle 전용")]
    [Tooltip("기함 기준 Z 오프셋 — 양수=전방 돌출, 음수=후방 후퇴")]
    public float circleZOffset;
    [Tooltip("shipCount 2~9 각각 슬롯 정의. shipCount 기준으로 검색")]
    public CircleLayoutByCount[] circleLayouts;

    // 런타임: 현재 함선 수에 맞는 Circle 레이아웃 반환
    public CircleLayoutByCount GetCircleLayout(int shipCount)
    {
        if (circleLayouts == null) return null;
        foreach (var layout in circleLayouts)
        {
            if (layout.shipCount == shipCount)
                return layout;
        }
        // 정확히 일치하는 count 없으면 가장 가까운 작은 값 반환
        CircleLayoutByCount best = null;
        foreach (var layout in circleLayouts)
        {
            if (layout.shipCount <= shipCount)
            {
                if (best == null || layout.shipCount > best.shipCount)
                    best = layout;
            }
        }
        return best;
    }
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
        var presets = UnityEngine.Resources.LoadAll<FormationPreset>("Formation");
        foreach (var p in presets)
        {
            if (p != null)
                s_map[p.formationType] = p;
        }
        UnityEngine.Debug.Log($"[FormationPresetDB] {s_map.Count}개 로드 완료");
    }
}
