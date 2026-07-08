using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(TutorialData))]
public class TutorialDataEditor : Editor
{
    private const string CSV_FOLDER = "Assets/Resources/DataTable/Tutorial/csv";

    private SerializedProperty m_tutorialId;
    private SerializedProperty m_tutorialName;
    private SerializedProperty m_priority;
    private SerializedProperty m_isHideSkipButton;
    private SerializedProperty m_steps;

    private bool[] m_foldouts = new bool[0];

    private void OnEnable()
    {
        m_tutorialId = serializedObject.FindProperty("tutorialId");
        m_tutorialName = serializedObject.FindProperty("tutorialName");
        m_priority = serializedObject.FindProperty("priority");
        m_isHideSkipButton = serializedObject.FindProperty("isHideSkipButton");
        m_steps = serializedObject.FindProperty("steps");
    }

    private void DrawCsvButtons()
    {
        TutorialData tutorialData = (TutorialData)target;
        string csvPath = CSV_FOLDER + "/datatable_tutorial_" + tutorialData.tutorialId + ".csv";

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Export / Import", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(csvPath, EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import CSV"))
        {
            if (File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("실패", $"파일이 없습니다:\n{csvPath}", "OK");
            }
            else
            {
                string csv = File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                Undo.RecordObject(tutorialData, "Import Tutorial CSV");
                tutorialData.ImportFromCsv(csv);
                EditorUtility.SetDirty(tutorialData);
                AssetDatabase.SaveAssets();
                UpdateFoldouts();
                EditorUtility.DisplayDialog("완료", $"CSV Import 완료\n{tutorialData.steps.Count}개 스텝 로드됨", "OK");
            }
        }

        if (GUILayout.Button("Export CSV"))
        {
            File.WriteAllText(csvPath, tutorialData.ExportToCsv(), System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("완료", $"CSV Export 완료:\n{csvPath}", "OK");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void UpdateFoldouts()
    {
        if (m_steps == null) return;

        int count = m_steps.arraySize;
        if (m_foldouts.Length != count)
        {
            bool[] newFoldouts = new bool[count];
            for (int i = 0; i < Mathf.Min(m_foldouts.Length, count); i++)
                newFoldouts[i] = m_foldouts[i];
            m_foldouts = newFoldouts;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        UpdateFoldouts();

        DrawCsvButtons();

        // 기본 정보
        EditorGUILayout.PropertyField(m_tutorialId);
        EditorGUILayout.PropertyField(m_tutorialName);
        EditorGUILayout.PropertyField(m_priority);
        EditorGUILayout.PropertyField(m_isHideSkipButton);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("스텝 목록", EditorStyles.boldLabel);

        // 맨 위에 삽입 버튼
        DrawInsertButton(0);

        // 스텝 리스트
        for (int i = 0; i < m_steps.arraySize; i++)
        {
            DrawStepItem(i);
            DrawInsertButton(i + 1);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawInsertButton(int insertIndex)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+ Insert Step", GUILayout.Width(100), GUILayout.Height(18)))
        {
            m_steps.InsertArrayElementAtIndex(Mathf.Max(0, insertIndex - 1));

            // insertIndex 위치로 이동
            if (insertIndex > 0 && insertIndex < m_steps.arraySize)
            {
                m_steps.MoveArrayElement(insertIndex - 1, insertIndex);
            }

            // 새 요소 초기화
            int newIndex = Mathf.Min(insertIndex, m_steps.arraySize - 1);
            var newStep = m_steps.GetArrayElementAtIndex(newIndex);
            newStep.FindPropertyRelative("stepId").stringValue = "step_new_" + newIndex;
            newStep.FindPropertyRelative("message").stringValue = "";

            serializedObject.ApplyModifiedProperties();
            UpdateFoldouts();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStepItem(int index)
    {
        if (index >= m_steps.arraySize) return;

        SerializedProperty step = m_steps.GetArrayElementAtIndex(index);
        string stepId = step.FindPropertyRelative("stepId").stringValue;
        string message = step.FindPropertyRelative("message").stringValue;

        // 미리보기 텍스트
        string preview = string.IsNullOrEmpty(message) ? "(empty)" : message;
        if (preview.Length > 40) preview = preview.Substring(0, 40) + "...";
        preview = preview.Replace("\n", " ").Replace("\r", "");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 헤더
        EditorGUILayout.BeginHorizontal();

        if (index < m_foldouts.Length)
            m_foldouts[index] = EditorGUILayout.Foldout(m_foldouts[index], "[" + index + "] " + stepId, true);

        GUILayout.FlexibleSpace();

        // 위로 이동
        EditorGUI.BeginDisabledGroup(index == 0);
        if (GUILayout.Button("▲", GUILayout.Width(25)))
        {
            m_steps.MoveArrayElement(index, index - 1);
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUI.EndDisabledGroup();

        // 아래로 이동
        EditorGUI.BeginDisabledGroup(index == m_steps.arraySize - 1);
        if (GUILayout.Button("▼", GUILayout.Width(25)))
        {
            m_steps.MoveArrayElement(index, index + 1);
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUI.EndDisabledGroup();

        // 복제
        if (GUILayout.Button("◇", GUILayout.Width(25)))
        {
            m_steps.InsertArrayElementAtIndex(index);
            var duplicated = m_steps.GetArrayElementAtIndex(index + 1);
            duplicated.FindPropertyRelative("stepId").stringValue = stepId + "_copy";
            serializedObject.ApplyModifiedProperties();
            UpdateFoldouts();
        }

        // 삭제
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("Delete Step", "[" + index + "] " + stepId + " 를 삭제하시겠습니까?", "삭제", "취소"))
            {
                m_steps.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                UpdateFoldouts();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }

        EditorGUILayout.EndHorizontal();

        // 미리보기 (접혀있을 때)
        bool isFolded = index < m_foldouts.Length && m_foldouts[index];
        if (!isFolded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(preview, EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        // 상세 내용
        if (isFolded)
        {
            EditorGUI.indentLevel++;

            var iter = step.Copy();
            var end = step.GetEndProperty();
            iter.NextVisible(true);

            do
            {
                if (SerializedProperty.EqualContents(iter, end)) break;
                EditorGUILayout.PropertyField(iter, true);
            }
            while (iter.NextVisible(false));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }
}
