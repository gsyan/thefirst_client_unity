using System.Collections.Generic;
using UnityEngine;

public class AirCraftPathPoint : MonoBehaviour
{
    public int index;
    public List<AirCraftPathPoint> neighbors;

    private void Awake()
    {
        // 게임 플레이 중에는 MeshRenderer 비활성화
        if (Application.isPlaying)
        {
            if (TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.enabled = false;
            }
        }
    }
}

