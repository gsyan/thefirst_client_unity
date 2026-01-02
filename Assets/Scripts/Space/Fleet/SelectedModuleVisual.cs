using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SelectedModuleVisual : MonoBehaviour
{
    private SpaceShip m_myShip;
    private ModuleBase m_partsBase;
    private Renderer[] m_renderers;
    private MaterialPropertyBlock m_propertyBlock;
    [SerializeField] private bool m_isSelected = false;
    private float m_calculatedGridSpacing = 3.0f;

    public ModuleBase ModuleBase => m_partsBase;

    public void InitializeSelectedModuleVisual(SpaceShip ship, ModuleBase partsBase)
    {
        m_myShip = ship;
        m_partsBase = partsBase;

        if (transform.childCount <= 0) return;
        m_renderers = transform.GetChild(0).GetComponents<MeshRenderer>();

        m_propertyBlock = new MaterialPropertyBlock();

        // Calculate appropriate grid spacing based on object size
        CalculateGridSpacing();
    }

    private void CalculateGridSpacing()
    {
        if (m_renderers == null || m_renderers.Length == 0) return;

        // Get mesh UV bounds to determine actual UV range
        MeshFilter meshFilter = m_renderers[0].GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            m_calculatedGridSpacing = 0.3f; // Default fallback
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;

        // Calculate UV range
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0)
        {
            m_calculatedGridSpacing = 0.3f; // Default fallback
            return;
        }

        Vector2 uvMin = uvs[0];
        Vector2 uvMax = uvs[0];

        foreach (Vector2 uv in uvs)
        {
            uvMin = Vector2.Min(uvMin, uv);
            uvMax = Vector2.Max(uvMax, uv);
        }

        // UV range (how many times UV repeats)
        Vector2 uvRange = uvMax - uvMin;
        float maxUVRange = Mathf.Max(uvRange.x, uvRange.y);

        // Target: 3-5 grid lines across the object
        // If UV is 0~1 (range=1), spacing should be ~0.25 (4 grids)
        // If UV is 0~5 (range=5), spacing should be ~1.25 (4 grids)
        m_calculatedGridSpacing = maxUVRange / 4.0f;
        m_calculatedGridSpacing = Mathf.Clamp(m_calculatedGridSpacing, 0.1f, 10.0f);
    }


    public void SetSelected(bool selected)
    {
        if (m_isSelected == selected) return;

        m_isSelected = selected;
        UpdateMaterial();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (m_propertyBlock == null)
        {
            if (transform.childCount > 0)
            {
                m_renderers = transform.GetChild(0).GetComponents<MeshRenderer>();
                m_propertyBlock = new MaterialPropertyBlock();
            }
        }
        // 에디터 모드에서 매 프레임 MaterialPropertyBlock 적용
        if (!Application.isPlaying && m_renderers != null && m_propertyBlock != null)
        {
            UpdateMaterial();
        }
    }
#endif

    private void UpdateMaterial()
    {
        // Reset all effects first
        m_propertyBlock.SetColor("_GridColor", Color.clear);
        m_propertyBlock.SetFloat("_GridIntensity", 0f);

        if (m_isSelected)
        {
            // Selected: Show grid only
            m_propertyBlock.SetColor("_GridColor", Color.cyan);
            m_propertyBlock.SetFloat("_GridIntensity", 1.0f);
        }
        
        // Set grid properties
        m_propertyBlock.SetFloat("_GridThickness", 1.0f);
        m_propertyBlock.SetFloat("_GridSpacing", m_calculatedGridSpacing);
        m_propertyBlock.SetFloat("_GridAnimationSpeed", 1.0f);

        // Apply to this parts' renderers only
        foreach (var renderer in m_renderers)
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(m_propertyBlock);
            }
        }
    }
}