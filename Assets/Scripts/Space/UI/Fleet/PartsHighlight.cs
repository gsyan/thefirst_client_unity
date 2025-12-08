using System.Collections.Generic;
using UnityEngine;

public class ModuleHighlight : MonoBehaviour
{
    private SpaceShip m_myShip;
    private ModuleBase m_partsBase;
    private Renderer[] m_renderers;
    private MaterialPropertyBlock m_propertyBlock;
    private bool m_isHighlighted = false;
    private bool m_isHovered = false;

    public ModuleBase ModuleBase => m_partsBase;

    public void InitializeModuleHighlight(SpaceShip ship, ModuleBase partsBase)
    {
        m_myShip = ship;
        m_partsBase = partsBase;

        if (transform.childCount <= 0) return;
        m_renderers = transform.GetChild(0).GetComponents<MeshRenderer>();

        m_propertyBlock = new MaterialPropertyBlock();
    }


    public void SetHighlighted(bool highlighted)
    {
        if (m_isHighlighted == highlighted) return;

        m_isHighlighted = highlighted;
        UpdateMaterial();
    }

    public void SetHovered(bool hovered)
    {
        if (m_isHovered == hovered) return;

        m_isHovered = hovered;
        UpdateMaterial();
    }

    private void UpdateMaterial()
    {
        // Reset all effects first
        m_propertyBlock.SetColor("_HighlightColor", Color.clear);
        m_propertyBlock.SetFloat("_HighlightIntensity", 0f);
        m_propertyBlock.SetColor("_GridColor", Color.clear);
        m_propertyBlock.SetFloat("_GridIntensity", 0f);

        if (m_isHighlighted)
        {
            // Selected: Show highlight + grid
            m_propertyBlock.SetColor("_HighlightColor", m_myShip.selectedColor);
            m_propertyBlock.SetFloat("_HighlightIntensity", 1.0f);
            m_propertyBlock.SetColor("_GridColor", Color.cyan);
            m_propertyBlock.SetFloat("_GridIntensity", 2.0f);
        }
        else if (m_isHovered)
        {
            // Hovered: Show only grid on this parts only
            m_propertyBlock.SetColor("_GridColor", Color.green);
            m_propertyBlock.SetFloat("_GridIntensity", 1.5f);
        }

        // Set grid properties
        m_propertyBlock.SetFloat("_GridThickness", 1.0f);//1.0f
        m_propertyBlock.SetFloat("_GridSpacing", 5.0f);//5.0f
        m_propertyBlock.SetFloat("_GridAnimationSpeed", 2.0f);

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