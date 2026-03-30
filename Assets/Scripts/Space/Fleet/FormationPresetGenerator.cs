// 에디터 전용 — Tools > Formation > Generate Presets 로 기본 preset asset 자동 생성
// EFormationType 추가 시 정의된 타입은 생성, 이미 존재하면 스킵
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class FormationPresetGenerator
{
    private const string SAVE_PATH = "Assets/Resources/Formation";

    [MenuItem("Tools/Formation/Generate Presets")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(SAVE_PATH))
            Directory.CreateDirectory(SAVE_PATH);

        int created = 0;
        int skipped = 0;

        // 정의된 타입은 명시 생성, 나머지 enum 값은 Linear와 동일한 기본값으로 생성
        CreateLinear(ref created, ref skipped);
        CreateCross(ref created, ref skipped);
        CreateX(ref created, ref skipped);
        CreateCircle(ref created, ref skipped);

        // EFormationType 중 위에서 처리되지 않은 값 → Linear 슬롯으로 기본 생성
        foreach (EFormationType type in System.Enum.GetValues(typeof(EFormationType)))
        {
            string path = AssetPath(type);
            if (AssetDatabase.LoadAssetAtPath<FormationPreset>(path) != null)
            {
                skipped++;
                continue;
            }
            // 아직 정의된 Create 메서드 없음 → Linear와 동일한 기본 슬롯으로 생성
            var preset = ScriptableObject.CreateInstance<FormationPreset>();
            preset.formationType = type;
            preset.parseType     = EFormationParseType.CubeGrid;
            preset.slots         = LinearSlots();
            AssetDatabase.CreateAsset(preset, path);
            created++;
            Debug.Log($"[FormationPresetGenerator] 기본(Linear) 슬롯으로 생성: {type}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FormationPresetGenerator] 완료 — 생성: {created}, 스킵(이미 존재): {skipped}");
    }

    static string AssetPath(EFormationType type) =>
        $"{SAVE_PATH}/Preset_{type}.asset";

    static FormationSlot[] LinearSlots() => new FormationSlot[]
    {
        new FormationSlot { positionIndex = 0, gridCoord = new Vector3Int( 0,  0, 0) },
        new FormationSlot { positionIndex = 1, gridCoord = new Vector3Int(+1,  0, 0) },
        new FormationSlot { positionIndex = 2, gridCoord = new Vector3Int(-1,  0, 0) },
        new FormationSlot { positionIndex = 3, gridCoord = new Vector3Int(+2,  0, 0) },
        new FormationSlot { positionIndex = 4, gridCoord = new Vector3Int(-2,  0, 0) },
        new FormationSlot { positionIndex = 5, gridCoord = new Vector3Int(+3,  0, 0) },
        new FormationSlot { positionIndex = 6, gridCoord = new Vector3Int(-3,  0, 0) },
        new FormationSlot { positionIndex = 7, gridCoord = new Vector3Int(+4,  0, 0) },
        new FormationSlot { positionIndex = 8, gridCoord = new Vector3Int(-4,  0, 0) },
    };

    // ────────────────────────────────────────────────────────────
    // Linear: 8 6 4 2 0 1 3 5 7  (홀수=우측, 짝수=좌측, z=0)
    // ────────────────────────────────────────────────────────────
    static void CreateLinear(ref int created, ref int skipped)
    {
        var preset = ScriptableObject.CreateInstance<FormationPreset>();
        preset.formationType = EFormationType.formation_type_linear_horizontal;
        preset.parseType     = EFormationParseType.CubeGrid;
        preset.slots         = LinearSlots();
        Save(preset, EFormationType.formation_type_linear_horizontal, ref created, ref skipped);
    }

    // ────────────────────────────────────────────────────────────
    // Cross: 대각 쌍 배치 (전방 우/좌 → 후방 우/좌 순으로 링 확장)
    //   6       5
    //     2   1
    //       0
    //     4   3
    //   8       7
    // ────────────────────────────────────────────────────────────
    static void CreateCross(ref int created, ref int skipped)
    {
        var preset = ScriptableObject.CreateInstance<FormationPreset>();
        preset.formationType = EFormationType.formation_type_cross;
        preset.parseType = EFormationParseType.CubeGrid;
        preset.slots = new FormationSlot[]
        {
            new FormationSlot { positionIndex = 0, gridCoord = new Vector3Int( 0,  0, 0) },
            new FormationSlot { positionIndex = 1, gridCoord = new Vector3Int(+1, +1, -1) },
            new FormationSlot { positionIndex = 2, gridCoord = new Vector3Int(-1, +1, -1) },
            new FormationSlot { positionIndex = 3, gridCoord = new Vector3Int(+1, -1, -1) },
            new FormationSlot { positionIndex = 4, gridCoord = new Vector3Int(-1, -1, -1) },
            new FormationSlot { positionIndex = 5, gridCoord = new Vector3Int(+2, +2, -2) },
            new FormationSlot { positionIndex = 6, gridCoord = new Vector3Int(-2, +2, -2) },
            new FormationSlot { positionIndex = 7, gridCoord = new Vector3Int(+2, -2, -2) },
            new FormationSlot { positionIndex = 8, gridCoord = new Vector3Int(-2, -2, -2) },
        };
        Save(preset, EFormationType.formation_type_cross, ref created, ref skipped);
    }

    // ────────────────────────────────────────────────────────────
    // X: Cross와 topology 동일, Z 스케일만 달라 보임
    //    → gridCoord는 Cross와 같되 z 격자 1칸을 더 넓게 쓰고 싶으면
    //      FormationPreview의 m_gridUnitSize를 진형별로 조절하거나
    //      추후 per-axis 스케일 필드 추가
    // ────────────────────────────────────────────────────────────
    static void CreateX(ref int created, ref int skipped)
    {
        var preset = ScriptableObject.CreateInstance<FormationPreset>();
        preset.formationType = EFormationType.formation_type_x;
        preset.parseType = EFormationParseType.CubeGrid;
        preset.slots = new FormationSlot[]
        {
            new FormationSlot { positionIndex = 0, gridCoord = new Vector3Int( 0,  0, 0) },
            new FormationSlot { positionIndex = 1, gridCoord = new Vector3Int(+1, +1, 1) },
            new FormationSlot { positionIndex = 2, gridCoord = new Vector3Int(-1, +1, 1) },
            new FormationSlot { positionIndex = 3, gridCoord = new Vector3Int(+1, -1, 1) },
            new FormationSlot { positionIndex = 4, gridCoord = new Vector3Int(-1, -1, 1) },
            new FormationSlot { positionIndex = 5, gridCoord = new Vector3Int(+2, +2, 2) },
            new FormationSlot { positionIndex = 6, gridCoord = new Vector3Int(-2, +2, 2) },
            new FormationSlot { positionIndex = 7, gridCoord = new Vector3Int(+2, -2, 2) },
            new FormationSlot { positionIndex = 8, gridCoord = new Vector3Int(-2, -2, 2) },
        };
        Save(preset, EFormationType.formation_type_x, ref created, ref skipped);
    }

    // ────────────────────────────────────────────────────────────
    // Circle: 함선 수별 각도 데이터
    //   0° = 전방, 90° = 우, 180° = 후방, 270° = 좌
    //   홀수 positionIndex = 오른쪽(x>0), 짝수 = 왼쪽(x<0)
    // ────────────────────────────────────────────────────────────
    static void CreateCircle(ref int created, ref int skipped)
    {
        var preset = ScriptableObject.CreateInstance<FormationPreset>();
        preset.formationType = EFormationType.formation_type_circle;
        preset.parseType = EFormationParseType.Circle;
        preset.circleLayouts = new CircleLayoutByCount[]
        {
            // 2척
            new CircleLayoutByCount { shipCount = 2, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f },
                new FormationSlot { positionIndex = 1, circleAngle =  90f },
            }},
            // 3척
            new CircleLayoutByCount { shipCount = 3, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f },
                new FormationSlot { positionIndex = 1, circleAngle =  90f },
                new FormationSlot { positionIndex = 2, circleAngle = 270f },
            }},
            // 4척
            new CircleLayoutByCount { shipCount = 4, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f },
                new FormationSlot { positionIndex = 1, circleAngle =  60f },
                new FormationSlot { positionIndex = 2, circleAngle = 300f },
                new FormationSlot { positionIndex = 3, circleAngle = 180f },
            }},
            // 5척
            new CircleLayoutByCount { shipCount = 5, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f },
                new FormationSlot { positionIndex = 1, circleAngle =  45f },
                new FormationSlot { positionIndex = 2, circleAngle = 315f },
                new FormationSlot { positionIndex = 3, circleAngle = 135f },
                new FormationSlot { positionIndex = 4, circleAngle = 225f },
            }},
            // 6척
            new CircleLayoutByCount { shipCount = 6, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f },
                new FormationSlot { positionIndex = 1, circleAngle =   0f },
                new FormationSlot { positionIndex = 2, circleAngle = 288f },
                new FormationSlot { positionIndex = 3, circleAngle =  72f },
                new FormationSlot { positionIndex = 4, circleAngle = 216f },
                new FormationSlot { positionIndex = 5, circleAngle = 144f },
            }},
            // 7척
            new CircleLayoutByCount { shipCount = 7, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f },
                new FormationSlot { positionIndex = 1, circleAngle =  30f },
                new FormationSlot { positionIndex = 2, circleAngle = 330f },
                new FormationSlot { positionIndex = 3, circleAngle =  90f },
                new FormationSlot { positionIndex = 4, circleAngle = 270f },
                new FormationSlot { positionIndex = 5, circleAngle = 150f },
                new FormationSlot { positionIndex = 6, circleAngle = 210f },
            }},
            // 8척
            new CircleLayoutByCount { shipCount = 8, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f   },
                new FormationSlot { positionIndex = 1, circleAngle =  25.7f },
                new FormationSlot { positionIndex = 2, circleAngle = 334.3f },
                new FormationSlot { positionIndex = 3, circleAngle =  77.1f },
                new FormationSlot { positionIndex = 4, circleAngle = 282.9f },
                new FormationSlot { positionIndex = 5, circleAngle = 128.6f },
                new FormationSlot { positionIndex = 6, circleAngle = 231.4f },
                new FormationSlot { positionIndex = 7, circleAngle = 180f   },
            }},
            // 9척
            new CircleLayoutByCount { shipCount = 9, slots = new FormationSlot[]
            {
                new FormationSlot { positionIndex = 0, circleAngle =   0f   },
                new FormationSlot { positionIndex = 1, circleAngle =  22.5f },
                new FormationSlot { positionIndex = 2, circleAngle = 337.5f },
                new FormationSlot { positionIndex = 3, circleAngle =  67.5f },
                new FormationSlot { positionIndex = 4, circleAngle = 292.5f },
                new FormationSlot { positionIndex = 5, circleAngle = 112.5f },
                new FormationSlot { positionIndex = 6, circleAngle = 247.5f },
                new FormationSlot { positionIndex = 7, circleAngle = 157.5f },
                new FormationSlot { positionIndex = 8, circleAngle = 202.5f },
            }},
        };
        Save(preset, EFormationType.formation_type_circle, ref created, ref skipped);
    }

    static void Save(FormationPreset preset, EFormationType type, ref int created, ref int skipped)
    {
        string path = AssetPath(type);
        if (AssetDatabase.LoadAssetAtPath<FormationPreset>(path) != null)
        {
            skipped++;
            return;
        }
        AssetDatabase.CreateAsset(preset, path);
        created++;
    }
}
#endif
