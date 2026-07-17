// Unity-MCP 패키지(개발자 로컬 전용 AI 에디터 툴)가 설치되어 있을 때만 컴파일 — 미설치 환경(Jenkins CI 등)에선
// UNITY_MCP_READY define 자체가 없어서 이 파일 전체가 컴파일 대상에서 빠짐 (Packages/UNITY_MCP_SETUP.md 참고)
#if UNITY_MCP_READY
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Runtime.Extensions;
using AIGD;
using UnityEngine;
using UnityEngine.EventSystems;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    [AiToolType]
    public partial class Tool_UISimulateClick
    {
        [AiTool("ui-simulate-click", Title = "UI / Simulate Click")]
        [Description("Simulates a real pointer click on a UI GameObject by dispatching a PointerEventData through the EventSystem, invoking IPointerDownHandler, IPointerUpHandler and IPointerClickHandler (including Button.onClick). Works in Play Mode only.")]
        public string Click
        (
            [Description("GameObjectRef of the UI element to click (Button, Toggle, etc). Prefer 'path' for reliability.")]
            GameObjectRef target
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!Application.isPlaying)
                    throw new InvalidOperationException("UI click simulation requires Play Mode.");

                var go = target.FindGameObject()
                    ?? throw new ArgumentException("Target GameObject not found.", nameof(target));

                if (EventSystem.current == null)
                    throw new InvalidOperationException("No active EventSystem found in the scene.");

                var pointerData = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, go.transform.position)
                };

                ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerClickHandler);

                return $"Clicked '{go.name}'.";
            });
        }
    }
}
#endif
