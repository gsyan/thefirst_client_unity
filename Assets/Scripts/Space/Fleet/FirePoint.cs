//------------------------------------------------------------------------------
using UnityEngine;

// Launcher가 인덱스로 적절한 FirePoint를 찾기 위한 컴포넌트
public class FirePoint : MonoBehaviour
{
    [SerializeField] private int m_index;

    public int Index => m_index;

    public void SetIndex(int index)
    {
        m_index = index;
    }
}
