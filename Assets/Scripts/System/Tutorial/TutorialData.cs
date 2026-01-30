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
    public string targetUIId;       // 대상 UI 이름
    public string targetPanelName;  // 대상이 속한 패널

    [Header("표시 옵션")]
    public bool showArrow = true;
    public EArrowDirection arrowDirection = EArrowDirection.Down;
    public bool highlightTarget = true;
    public Vector2 textBoxOffset = new Vector2(0, 100f);

    [Header("자동 진행")]
    public float autoNextDelay = 0f; // 0이면 수동 진행

    [Header("사전 액션")]
    public string preActionPanelName; // 스텝 시작 전 열 패널 (선택)
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
