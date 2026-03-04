#if UNITY_EDITOR
// 금지어 테이블 Editor: Reset to Default 버튼으로 기본값 복원
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableForbiddenWords))]
public class DataTableForbiddenWordsEditor : Editor
{
    private DataTableForbiddenWords m_target;

    private void OnEnable()
    {
        m_target = (DataTableForbiddenWords)target;
    }

    public override void OnInspectorGUI()
    {
        if (m_target == null) return;

        serializedObject.Update();

        EditorGUILayout.LabelField("Forbidden Words Table", EditorStyles.largeLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset to Default", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Reset 확인",
                $"현재 목록을 초기화하고 기본 금지어 {DataTableForbiddenWords.DefaultBannedWords.Length}개로 덮어씁니다.\n계속하시겠습니까?",
                "확인", "취소") == true)
            {
                Undo.RecordObject(m_target, "Reset ForbiddenWords to Default");
                m_target.ResetToDefault();
                EditorUtility.SetDirty(m_target);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
