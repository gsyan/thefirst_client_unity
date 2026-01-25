using System.Collections.Generic;
using UnityEngine;

public class ShieldVertex : MonoBehaviour
{
    public int index;
    public List<int> neighborIndices = new List<int>();
    public Vector3 GetPosition() => transform.position;
}
