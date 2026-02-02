using System.Collections.Generic;
using UnityEngine;

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
}

// 화살표 방향
public enum EArrowDirection
{
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
    public EArrowDirection arrowDirection = EArrowDirection.Down;
    public bool highlightTarget = true;
    public Vector2 textBoxOffset = new Vector2(0, 100f);
    public Vector2 textBoxSize = Vector2.zero;      // (0,0)이면 기본값 사용
    public Vector2 textBoxPosition = Vector2.zero;  // (0,0)이 아니면 절대 위치 사용

    [Header("자동 진행")]
    public float autoNextDelay = 0f; // 0이면 수동 진행

    [Header("사전 액션")]
    public string preActionPanelName; // 스텝 시작 전 열 패널 (선택)

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
}
