// [DEV] TestScene 커스텀 에디터 — 모드별 Reload 버튼 + 필드 순서 지정
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TestScene))]
public class TestSceneEditor : Editor
{
    SerializedProperty m_mode;
    SerializedProperty m_shipInfos;
    SerializedProperty m_spawnFormation;
    SerializedProperty m_explosionPrefabs;

    private void OnEnable()
    {
        m_mode            = serializedObject.FindProperty("m_mode");
        m_shipInfos       = serializedObject.FindProperty("m_shipInfos");
        m_spawnFormation  = serializedObject.FindProperty("m_spawnFormation");
        m_explosionPrefabs = serializedObject.FindProperty("m_explosionPrefabs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // m_mode
        EditorGUILayout.PropertyField(m_mode);

        EditorGUILayout.Space(6);

        // Reload 버튼
        bool isPlaying = Application.isPlaying;
        GUI.enabled = isPlaying;

        ETestInputMode currentMode = (ETestInputMode)m_mode.enumValueIndex;
        string buttonLabel = currentMode == ETestInputMode.fleet ? "Reload Fleet" : "Reload Effects";

        if (GUILayout.Button(buttonLabel, GUILayout.Height(28)))
        {
            TestScene testScene = (TestScene)target;
            if (currentMode == ETestInputMode.fleet)
                testScene.RespawnMyFleet();
            else
                Debug.Log("[TestScene] Effect 리스트 갱신됨 — 다음 키 입력부터 적용");
        }

        GUI.enabled = true;

        if (isPlaying == false)
            EditorGUILayout.HelpBox("플레이 모드에서만 동작합니다.", MessageType.Info);

        EditorGUILayout.Space(6);

        // Fleet 섹션
        EditorGUILayout.PropertyField(m_shipInfos, true);
        EditorGUILayout.PropertyField(m_spawnFormation);

        EditorGUILayout.Space(4);

        // Effect 섹션
        EditorGUILayout.PropertyField(m_explosionPrefabs, true);

        serializedObject.ApplyModifiedProperties();
    }
}
