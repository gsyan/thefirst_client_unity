using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class UIAtlasTools
{
    private const string AtlasPath = "Assets/Resources/UIAtlas/UIAtlas.spriteatlasv2";

    [MenuItem("Tools/UIAtlas/Image 폴더와 Atlas 동기화")]
    static void SyncAtlasWithImageFolder()
    {
        string atlasAbsPath  = Path.Combine(Application.dataPath, "Resources/UIAtlas/UIAtlas.spriteatlasv2");
        string folderAbsPath = Path.Combine(Application.dataPath, "Resources/UIAtlas/Image");

        if (File.Exists(atlasAbsPath) == false)
        {
            Debug.LogError($"Atlas 파일 없음: {atlasAbsPath}");
            return;
        }

        var guidRegex   = new Regex(@"^guid: ([0-9a-f]+)", RegexOptions.Multiline);
        var folderGuids = new HashSet<string>(CollectFolderGuids(folderAbsPath, guidRegex));
        var entryRegex  = new Regex(@"    - \{fileID: \d+, guid: ([0-9a-f]+), type: \d+\}");

        var lines = new List<string>(File.ReadAllText(atlasAbsPath).Split('\n'));

        // packables 헤더 줄 탐색
        int headerLineIdx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimEnd() == "    packables:")
            {
                headerLineIdx = i;
                break;
            }
        }

        if (headerLineIdx < 0)
        {
            Debug.LogError("packables 섹션을 찾을 수 없습니다.");
            return;
        }

        // 기존 packable 줄 수집
        var entryLineIndices = new List<int>();
        for (int i = headerLineIdx + 1; i < lines.Count; i++)
        {
            if (entryRegex.IsMatch(lines[i]))
                entryLineIndices.Add(i);
            else if (lines[i].Length > 0 && lines[i][0] != ' ' && lines[i].Trim().Length > 0)
                break;
        }

        // 폴더에 없는 항목 → 제거 대상, 있는 항목 → 기존 GUID로 기록
        var existingGuids  = new HashSet<string>();
        var indicesToRemove = new HashSet<int>();
        int removed = 0;
        foreach (int li in entryLineIndices)
        {
            Match m = entryRegex.Match(lines[li]);
            if (m.Success == false) continue;
            string guid = m.Groups[1].Value;
            if (folderGuids.Contains(guid))
                existingGuids.Add(guid);
            else
            {
                indicesToRemove.Add(li);
                removed++;
            }
        }

        // 신규 추가 항목
        var toAdd = new List<string>();
        foreach (string guid in folderGuids)
        {
            if (existingGuids.Contains(guid) == false)
                toAdd.Add($"    - {{fileID: 2800000, guid: {guid}, type: 3}}");
        }

        if (removed == 0 && toAdd.Count == 0)
        {
            Debug.Log("Atlas가 이미 Image 폴더와 동기화되어 있습니다.");
            return;
        }

        // 제거 (뒤에서부터)
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (indicesToRemove.Contains(i))
                lines.RemoveAt(i);
        }

        // 헤더 재탐색 후 신규 항목 삽입
        int newHeaderIdx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimEnd() == "    packables:")
            {
                newHeaderIdx = i;
                break;
            }
        }

        if (newHeaderIdx >= 0)
        {
            for (int i = toAdd.Count - 1; i >= 0; i--)
                lines.Insert(newHeaderIdx + 1, toAdd[i]);
        }

        File.WriteAllText(atlasAbsPath, string.Join("\n", lines), System.Text.Encoding.UTF8);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"UIAtlas 동기화 완료 — 추가: {toAdd.Count}개, 제거: {removed}개.");
    }

    static List<string> CollectFolderGuids(string folderAbsPath, Regex guidRegex)
    {
        var guids = new List<string>();
        foreach (string meta in Directory.GetFiles(folderAbsPath, "*.png.meta", SearchOption.AllDirectories))
        {
            Match m = guidRegex.Match(File.ReadAllText(meta));
            if (m.Success)
                guids.Add(m.Groups[1].Value);
        }
        return guids;
    }
}
