// ModuleSlot 커스텀 인스펙터 - 슬롯 정보 편집 및 카메라 목표값 리셋 버튼 제공
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModuleSlot))]
public class ModuleSlotEditor : Editor
{
    SerializedProperty m_moduleSlotInfo;
    SerializedProperty m_cameraRotationY;
    SerializedProperty m_cameraRotationX;
    SerializedProperty m_cameraZoom;
    SerializedProperty m_missileEjectSpeed;

    private const float DEFAULT_YAW_FRONT = 0f;
    private const float DEFAULT_YAW_REAR = 180f;
    private const float DEFAULT_YAW_VALUE = 30f;
    private const float DEFAULT_PITCH_THRESHOLD = 0.2f;
    private const float DEFAULT_PITCH_UP = 25f;
    private const float DEFAULT_PITCH_DOWN = -25f;
    private const float DEFAULT_ZOOM = 20f;

    private void OnEnable()
    {
        m_moduleSlotInfo = serializedObject.FindProperty("m_moduleSlotInfo");
        m_cameraRotationY = serializedObject.FindProperty("m_cameraRotationY");
        m_cameraRotationX = serializedObject.FindProperty("m_cameraRotationX");
        m_cameraZoom = serializedObject.FindProperty("m_cameraZoom");
        m_missileEjectSpeed = serializedObject.FindProperty("m_missileEjectSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Module Type
        var moduleTypeProp = m_moduleSlotInfo.FindPropertyRelative("moduleType");
        EditorGUILayout.PropertyField(moduleTypeProp);

        // Module Slot Index
        var slotIndexProp = m_moduleSlotInfo.FindPropertyRelative("slotIndex");
        EditorGUILayout.PropertyField(slotIndexProp);

        // 미사일 슬롯일 때만 Eject Speed 표시
        var moduleType = (EModuleType)moduleTypeProp.intValue;
        if (moduleType == EModuleType.missile)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Missile Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_missileEjectSpeed, new GUIContent("Eject Speed"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Target", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(m_cameraRotationY, new GUIContent("Rotation Y (Yaw)"));
        EditorGUILayout.PropertyField(m_cameraRotationX, new GUIContent("Rotation X (Pitch)"));
        EditorGUILayout.PropertyField(m_cameraZoom, new GUIContent("Zoom"));

        if (GUILayout.Button("Capture from Scene View"))
        {
            CaptureFromSceneView((ModuleSlot)target);
        }

        if (GUILayout.Button("Reset to Default"))
        {
            ResetCameraValues((ModuleSlot)target);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CaptureFromSceneView(ModuleSlot slot)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[ModuleSlotEditor] 활성화된 SceneView가 없음");
            return;
        }

        // SceneView 카메라 forward → CameraController 구면좌표계 역산
        // CameraController: camera = target + (sin(Y)*cos(X), sin(X), cos(Y)*cos(X)) * zoom
        // → forward = (-sin(Y)*cos(X), -sin(X), -cos(Y)*cos(X))
        Vector3 fwd = sceneView.camera.transform.rotation * Vector3.forward;
        float rotX = -Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
        float rotY = Mathf.Atan2(-fwd.x, -fwd.z) * Mathf.Rad2Deg;
        float zoom = Vector3.Distance(sceneView.camera.transform.position, sceneView.pivot);

        Undo.RecordObject(slot, "Capture Camera from Scene View");
        slot.m_cameraRotationY = Mathf.Round(rotY * 10f) / 10f;
        slot.m_cameraRotationX = Mathf.Round(rotX * 10f) / 10f;
        slot.m_cameraZoom = Mathf.Round(zoom * 10f) / 10f;

        EditorUtility.SetDirty(slot);
    }

    private void ResetCameraValues(ModuleSlot slot)
    {
        // 부모 계층에서 ModuleBody 탐색
        ModuleBody body = slot.GetComponentInParent<ModuleBody>();
        if (body == null)
        {
            Debug.LogWarning("[ModuleSlotEditor] 부모에서 ModuleBody를 찾을 수 없음");
            return;
        }

        Vector3 dir = slot.transform.position - body.transform.position;
        if (dir.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning("[ModuleSlotEditor] 슬롯과 바디 위치가 동일함");
            return;
        }

        Undo.RecordObject(slot, "Reset Camera Values");

        // Yaw: 전/후 기준값 + (전후 부호 * 좌우 부호 * 오프셋)으로 4분면 계산
        bool isFront = dir.z >= 0f;
        float yawBase = isFront ? DEFAULT_YAW_FRONT : DEFAULT_YAW_REAR;
        float yawSign = (isFront ? 1f : -1f) * (dir.x <= 0f ? -1f : 1f);
        slot.m_cameraRotationY = yawBase + yawSign * DEFAULT_YAW_VALUE;

        // Pitch: 수직 위치 기반으로 두 단계만 구분
        Vector3 dirNorm = dir.normalized;
        slot.m_cameraRotationX = dirNorm.y < -DEFAULT_PITCH_THRESHOLD ? DEFAULT_PITCH_DOWN
                               : DEFAULT_PITCH_UP;

        slot.m_cameraZoom = DEFAULT_ZOOM;

        EditorUtility.SetDirty(slot);
    }

    private EModuleSubType DrawFilteredSubTypePopup(EModuleType moduleType, EModuleSubType currentSubType)
    {
        // 타입에 맞는 SubType만 필터링
        var filteredSubTypes = new System.Collections.Generic.List<EModuleSubType>();
        filteredSubTypes.Add(EModuleSubType.none);

        int typeValue = (int)moduleType;
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.none) continue;

            int subTypeValue = (int)subType;
            if (subTypeValue / 1000 == typeValue)
            {
                filteredSubTypes.Add(subType);
            }
        }

        // 현재 선택된 SubType이 필터링된 목록에 없으면 None으로 변경
        if (filteredSubTypes.Contains(currentSubType) == false)
        {
            currentSubType = EModuleSubType.none;
        }

        int currentIndex = filteredSubTypes.IndexOf(currentSubType);
        string[] displayNames = new string[filteredSubTypes.Count];
        for (int i = 0; i < filteredSubTypes.Count; i++)
        {
            displayNames[i] = filteredSubTypes[i].ToString();
        }

        int newIndex = EditorGUILayout.Popup("Module Sub Type", currentIndex, displayNames);
        return filteredSubTypes[newIndex];
    }
}
