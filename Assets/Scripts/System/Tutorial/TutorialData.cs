using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 튜토리얼 트리거 타입
public enum ETutorialTrigger
{
    UIClick,    // 특정 UI 클릭
    AnyClick,   // 아무 곳이나 클릭
    AutoNext,   // 자동 진행
    Custom      // 커스텀 조건
}

// 커스텀 조건 타입
public enum ETutorialConditionType
{
    None,
    CameraRotationChanged,    // 카메라 누적 회전량 체크
    CameraZoomChanged,        // 카메라 줌 인/아웃 변화량 체크
    ModuleSelected,           // 아무 모듈 선택
    ModuleSelectedCount,      // 서로 다른 모듈 N개 선택
    SpecificModuleSelected,   // 특정 모듈 선택
    CinematicOpeningBattle,   // 튜토리얼 오프닝 관전 전투 연출 시작 (TutorialCinematicController.SpawnOpeningBattle)
    ShipArrivedAtFormation,   // TutorialManager.SetPendingNewShip으로 등록된 함선이 대형 자리에 도착할 때까지 대기
    EscapeShipDistanceFromFlagship, // 지크프리트 기함 뒤에서 탈출 함선을 스폰하고, conditionThreshold 거리만큼 멀어질 때까지 대기
    EnemyWave1, // step2 — 5개 함대([7,3,3] 구성), 10초 간격 스폰, 전멸까지 대기
    EnemyWave2, // step4 — 10개 함대([9,5,5,3,3] 구성), 5초 간격 스폰. 애초에 전멸이 불가능한 물량 — 다음 스텝 전환은 FlagshipHealthBelowPercent가 별도로 감시
    FlagshipHealthBelowPercent, // 내 함대 기함 체력 비율이 conditionThreshold(0~1) 이하로 떨어질 때까지 대기
}

// 화살표 방향 — Auto면 TutorialArrow가 화면 여유 공간을 보고 자동 결정, 그 외는 강제 지정
public enum EArrowDirection
{
    Auto,
    Up,
    Down,
    Left,
    Right
}

// 개별 튜토리얼 스텝
[System.Serializable]
public class TutorialStep
{
    [Header("기본 정보")]
    public string stepId;
    [TextArea(2, 4)]
    public string message;
    public ETutorialTrigger triggerType = ETutorialTrigger.UIClick;

    [Header("UI 타겟팅")]
    public string targetPanelName;  // 대상이 속한 패널
    public string targetUIId;       // 대상 UI 이름
    

    [Header("표시 옵션")]
    public bool showArrow = true;
    public EArrowDirection arrowDirection = EArrowDirection.Auto;
    public bool hasHole = true;
    public bool dimBackground = true; // false면 대상 UI 없이도 화면 전체 어둡게 하지 않음 (대기 스텝 등에 사용)
    public Vector2 textBoxOffset = new Vector2(0, 100f);
    public Vector2 textBoxSize = Vector2.zero;      // (0,0)이면 기본값 사용
    public Vector2 textBoxPosition = Vector2.zero;  // (0,0)이 아니면 절대 위치 사용

    [Header("자동 진행")]
    public float autoNextDelay = 0f; // 0이면 수동 진행

    [Header("사전 액션")]
    public string preActionPanelName; // 스텝 시작 전 열 패널 (선택)
    public string preActionTabName;   // 스텝 시작 전 전환할 TabSystem 탭 이름 (선택, targetUIId가 탭 안에 있을 때 사용)

    [Header("커스텀 조건 (triggerType이 Custom일 때 사용)")]
    public ETutorialConditionType conditionType = ETutorialConditionType.None;
    public float conditionThreshold = 90f;      // 카메라 회전량(도) 또는 기타 수치
    public int conditionCount = 3;              // 모듈 선택 횟수 등
    public EModuleType targetModuleType;        // 특정 모듈 ID (SpecificModuleSelected용)
}

// 튜토리얼 데이터 (ScriptableObject)
[CreateAssetMenu(fileName = "Tutorial_New", menuName = "Custom/TutorialData")]
public class TutorialData : ScriptableObject
{
    [Header("튜토리얼 정보")]
    public string tutorialId;
    public string tutorialName;
    public int priority = 0; // 낮을수록 먼저 실행

    [Header("스텝 목록")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    private static readonly string[] CSV_HEADER = new string[]
    {
        "stepId", "message", "triggerType", "targetPanelName", "targetUIId",
        "showArrow", "arrowDirection", "hasHole",
        "textBoxOffsetX", "textBoxOffsetY", "textBoxSizeX", "textBoxSizeY", "textBoxPositionX", "textBoxPositionY",
        "autoNextDelay", "preActionPanelName", "preActionTabName",
        "conditionType", "conditionThreshold", "conditionCount", "targetModuleType", "dimBackground"
    };

    public string ExportToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CSV_HEADER));

        foreach (TutorialStep step in steps)
        {
            string[] fields = new string[]
            {
                CsvEscape(step.stepId),
                CsvEscape(step.message),
                CsvEscape(step.triggerType.ToString()),
                CsvEscape(step.targetPanelName),
                CsvEscape(step.targetUIId),
                CsvEscape(step.showArrow.ToString()),
                CsvEscape(step.arrowDirection.ToString()),
                CsvEscape(step.hasHole.ToString()),
                CsvEscape(step.textBoxOffset.x.ToString()),
                CsvEscape(step.textBoxOffset.y.ToString()),
                CsvEscape(step.textBoxSize.x.ToString()),
                CsvEscape(step.textBoxSize.y.ToString()),
                CsvEscape(step.textBoxPosition.x.ToString()),
                CsvEscape(step.textBoxPosition.y.ToString()),
                CsvEscape(step.autoNextDelay.ToString()),
                CsvEscape(step.preActionPanelName),
                CsvEscape(step.preActionTabName),
                CsvEscape(step.conditionType.ToString()),
                CsvEscape(step.conditionThreshold.ToString()),
                CsvEscape(step.conditionCount.ToString()),
                CsvEscape(step.targetModuleType.ToString()),
                CsvEscape(step.dimBackground.ToString())
            };
            sb.AppendLine(string.Join(",", fields));
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
        return sb.ToString();
    }

    public void ImportFromCsv(string csv)
    {
        List<List<string>> rows = ParseCsv(csv);
        if (rows.Count == 0) return;

        List<TutorialStep> imported = new List<TutorialStep>();
        for (int i = 1; i < rows.Count; i++)
        {
            List<string> cols = rows[i];
            if (cols.Count < CSV_HEADER.Length) continue;
            if (string.IsNullOrEmpty(cols[0])) continue;

            TutorialStep step = new TutorialStep();
            step.stepId = cols[0];
            step.message = cols[1];
            System.Enum.TryParse(cols[2], out step.triggerType);
            step.targetPanelName = cols[3];
            step.targetUIId = cols[4];
            bool.TryParse(cols[5], out step.showArrow);
            System.Enum.TryParse(cols[6], out step.arrowDirection);
            bool.TryParse(cols[7], out step.hasHole);

            float offsetX, offsetY, sizeX, sizeY, posX, posY, autoNextDelay, conditionThreshold;
            float.TryParse(cols[8], out offsetX);
            float.TryParse(cols[9], out offsetY);
            float.TryParse(cols[10], out sizeX);
            float.TryParse(cols[11], out sizeY);
            float.TryParse(cols[12], out posX);
            float.TryParse(cols[13], out posY);
            step.textBoxOffset = new Vector2(offsetX, offsetY);
            step.textBoxSize = new Vector2(sizeX, sizeY);
            step.textBoxPosition = new Vector2(posX, posY);

            float.TryParse(cols[14], out autoNextDelay);
            step.autoNextDelay = autoNextDelay;
            step.preActionPanelName = cols[15];
            step.preActionTabName = cols[16];

            System.Enum.TryParse(cols[17], out step.conditionType);
            float.TryParse(cols[18], out conditionThreshold);
            step.conditionThreshold = conditionThreshold;
            int.TryParse(cols[19], out step.conditionCount);
            System.Enum.TryParse(cols[20], out step.targetModuleType);
            bool.TryParse(cols[21], out step.dimBackground);

            imported.Add(step);
        }

        steps = imported;
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private static string CsvEscape(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        bool needsQuote = field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;
        if (needsQuote == false) return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    // 따옴표로 감싼 필드 안의 줄바꿈/쉼표를 지원하는 CSV 파서
    private static List<List<string>> ParseCsv(string csv)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> currentRow = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (inQuotes == true)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\r')
                {
                    // 다음 \n에서 처리
                }
                else if (c == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }
}
