using UnityEngine;

public class LandMaskPainter : MonoBehaviour
{
    public Texture2D sourceTex;

    [Header("정규화 — 현재 범위를 0~255로 늘림")]
    [Range(0, 255)] public int inputMin = 11;
    [Range(0, 255)] public int inputMax = 211;

    [Header("값 이동 — 양수=육지 증가 / 음수=바다 증가")]
    [Range(-128, 128)] public int valueShift = 0;

    [Header("감마 보정 — <1 밝아짐(육지) / >1 어두워짐(바다)")]
    [Range(0.1f, 4f)] public float gamma = 1f;

    [HideInInspector] public Texture2D previewTex;
    [HideInInspector] public string statusMsg = "";
    [HideInInspector] public int[] histogram = new int[256];
    [HideInInspector] public bool hasHistogram = false;
}
