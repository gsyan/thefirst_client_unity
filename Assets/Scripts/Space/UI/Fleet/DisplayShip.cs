// using System.Collections.Generic;
// using UnityEngine;

// public class DisplayShip : MonoBehaviour
// {
//     [Header("Visualization")]
//     public Material highlightMaterial;
//     public Color selectedColor = Color.yellow;
//     public Color hoverColor = Color.cyan;

//     // Private fields
//     private DisplayFleet m_displayFleet;
//     private SpaceShip m_spaceShip;
//     private List<ModuleHighlight> m_moduleHighlights = new List<ModuleHighlight>();
//     private ModuleBase m_currentSelectedModule = null;

//     public void InitializeDisplayShip(DisplayFleet displayFleet, SpaceShip spaceShip)
//     {
//         m_displayFleet = displayFleet;
//         m_spaceShip = spaceShip;
//         SetupModuleHighlighting();
//     }

//     private void SetupModuleHighlighting()
//     {
//         // Setup highlighting for parts bodies
//         foreach (ModuleBody body in m_spaceShip.m_moduleBodyList)
//         {
//             if (body != null)
//             {
//                 SetupModuleHighlight(body);

//                 // Setup highlighting for weapons
//                 foreach (ModuleWeapon weapon in body.m_weapons)
//                 {
//                     if (weapon != null)
//                         SetupModuleHighlight(weapon);
//                 }

//                 // Setup highlighting for engines
//                 foreach (ModuleEngine engine in body.m_engines)
//                 {
//                     if (engine != null)
//                         SetupModuleHighlight(engine);
//                 }
//             }
//         }
//     }

//     private void SetupModuleHighlight(ModuleBase partsBase)
//     {
//         // Add highlighting component
//         ModuleHighlight highlight = partsBase.gameObject.AddComponent<ModuleHighlight>();
//         //highlight.InitializeModuleHighlight(this, partsBase);
//         m_moduleHighlights.Add(highlight);
//     }

//     private Bounds CalculatePartsBounds(ModuleBase partsBase)
//     {
//         Bounds bounds = new Bounds(partsBase.transform.position, Vector3.one);

//         // Include all child renderers
//         Renderer[] renderers = partsBase.GetComponentsInChildren<Renderer>();
//         bool hasRenderers = false;

//         foreach (var renderer in renderers)
//         {
//             if (renderer != null && renderer.enabled)
//             {
//                 if (!hasRenderers)
//                 {
//                     bounds = renderer.bounds;
//                     hasRenderers = true;
//                 }
//                 else
//                 {
//                     bounds.Encapsulate(renderer.bounds);
//                 }
//             }
//         }

//         // Ensure minimum size for interaction
//         if (!hasRenderers || bounds.size.magnitude < 1f)
//         {
//             bounds.center = partsBase.transform.position;
//             bounds.size = Vector3.one * 1.5f;
//         }

//         return bounds;
//     }

//     public void OnClicked(Vector3 hitPoint, Collider hitCollider = null)
//     {
//         ModuleBase clickedParts = null;
//         if (hitCollider != null)
//         {
//             clickedParts = hitCollider.GetComponent<ModuleBase>();
//             if (clickedParts == null)
//                 clickedParts = hitCollider.GetComponentInParent<ModuleBase>();
//         }

//         if (clickedParts != null)
//             SelectParts(clickedParts);        
//     }

//     public void SelectParts(ModuleBase partsBase)
//     {
//         if (m_displayFleet == null) return;
//         if (m_displayFleet.m_enableModuleSelection == false) return;
        
//         m_displayFleet.ClearAllSelections();
            

//         m_currentSelectedModule = partsBase;
//         UpdateHighlighting();

//         if (m_displayFleet != null)
//             m_displayFleet.OnModuleClicked(m_spaceShip, partsBase);
//     }

//     public void ClearSelection()
//     {
//         m_currentSelectedModule = null;
//         UpdateHighlighting();
//     }

//     private void UpdateHighlighting()
//     {
//         foreach (var highlight in m_moduleHighlights)
//         {
//             if (highlight != null)
//             {
//                 bool isSelected = (highlight.ModuleBase == m_currentSelectedModule);
//                 highlight.SetHighlighted(isSelected);
//             }
//         }
//     }

//     public void OnModuleHover(ModuleBase partsBase, bool isHovering)
//     {
//         if (m_displayFleet.m_enableModuleSelection == false) return;
//         // Find the highlight component for this parts
//         var highlight = m_moduleHighlights.Find(h => h != null && h.ModuleBase == partsBase);
//         if (highlight != null)
//             highlight.SetHovered(isHovering);
//     }
// }
