// 함체 프리팹에 부착 — 에디터 툴로 메시 합치기 대상 오브젝트 지정 (하위 메시 재귀 수집)
using System.Collections.Generic;
using UnityEngine;

public class CombineMeshTarget : MonoBehaviour
{
    // 합칠 루트 오브젝트 목록 — 자식 포함 재귀적으로 MeshFilter를 수집해 합침
    public List<GameObject> m_combineTargets = new();
}
